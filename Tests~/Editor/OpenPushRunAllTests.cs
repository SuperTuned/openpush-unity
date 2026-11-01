using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushRunAllTests
    {
        /// A checklist whose rows a test can wire to each other: a fix flips its own row and maybe others.
        private sealed class Checklist
        {
            public readonly Dictionary<string, bool> Passing = new Dictionary<string, bool>();
            public readonly List<string> Order = new List<string>();
            public readonly Dictionary<string, System.Action> Fixes = new Dictionary<string, System.Action>();
            public readonly List<string> Ran = new List<string>();
            public int Evaluations;

            public void Row(string title, bool passing, System.Action fix = null)
            {
                Order.Add(title);
                Passing[title] = passing;
                if (fix != null) Fixes[title] = () => { Ran.Add(title); fix(); };
            }

            public IReadOnlyList<OpenPushRunRow> Evaluate()
            {
                Evaluations++;
                return Order.Select(t => new OpenPushRunRow
                {
                    Title = t, Passed = Passing[t], Detail = t + " detail", Auto = Fixes.TryGetValue(t, out var f) ? f : null,
                }).ToList();
            }
        }

        [Test]
        public void FixesInOrderReEvaluatingBetweenFixesAndStopsAtTheFirstRowThatCannotFixItself()
        {
            var c = new Checklist();
            c.Row("settings", false, () => c.Passing["settings"] = true);
            // Only fixable once settings exist: re-evaluation shows the change.
            c.Row("resources", false, () => c.Passing["resources"] = c.Passing["settings"]);
            c.Row("sdk key", false);
            c.Row("edm4u", false, () => c.Passing["edm4u"] = true);
            var fixedRows = new List<string>();

            var end = OpenPushRunAllPlanner.RunToEnd(c.Evaluate, fixedRows);

            CollectionAssert.AreEqual(new[] { "settings", "resources" }, c.Ran);
            Assert.AreEqual(OpenPushRunStep.Stop, end.Step);
            Assert.AreEqual("sdk key", end.Row.Title);
            StringAssert.Contains("sdk key detail", end.Reason);
            Assert.AreEqual(3, c.Evaluations, "every row re-read before each step");
            Assert.IsFalse(c.Ran.Contains("edm4u"), "nothing after the stop runs");
        }

        [Test]
        public void APackageStillLoadingMakesTheEditorWaitAndTheBatchRunAskForASecondPass()
        {
            var rows = new[] { new OpenPushRunRow { Title = "EDM4U", Passed = true, Pending = true },
                               new OpenPushRunRow { Title = "resolved", Passed = false, Auto = () => { } } };
            var next = OpenPushRunAllPlanner.Next(rows, new HashSet<string>());
            Assert.AreEqual(OpenPushRunStep.Wait, next.Step, "nothing below a loading package is judged");
            var end = OpenPushRunAllPlanner.RunToEnd(() => rows, new List<string>());
            Assert.AreEqual(OpenPushRunStep.Stop, end.Step);
            StringAssert.Contains("Run RunAll once more", end.Reason);
        }

        [Test]
        public void AFixThatDoesNotHelpIsTriedOnceThenNamed()
        {
            var c = new Checklist();
            c.Row("resolve", false, () => { });
            var end = OpenPushRunAllPlanner.RunToEnd(c.Evaluate, new List<string>());
            Assert.AreEqual(1, c.Ran.Count);
            StringAssert.Contains("its fix ran but the row still fails", end.Reason);
        }

        [Test]
        public void TheReportAndFirstRunMarkerAreFieldsOfTheirOwnThatSurviveRewrites()
        {
            var file = OpenPushSetupFile.Parse("{\"schema\":\"openpush-setup/1\",\"choices\":{\"icons\":{\"mode\":\"new\"}}}");
            file.SetFlag("setup_window_shown", true);
            file.SetChecks(new[] { new KeyValuePair<string, (bool, string)>("SDK key", (false, "Empty.")) });
            var again = OpenPushSetupFile.Parse(file.ToJson());
            Assert.IsTrue(again.Flag("setup_window_shown"));
            Assert.IsTrue(again.HasChoice("icons"));
            StringAssert.Contains("\"title\": \"SDK key\"", again.ToJson());
        }

        [Test]
        public void AllPassingIsDone()
        {
            var c = new Checklist();
            c.Row("a", true);
            Assert.AreEqual(OpenPushRunStep.Done, OpenPushRunAllPlanner.RunToEnd(c.Evaluate, new List<string>()).Step);
        }
    }

    public class OpenPushSetupPasteTests
    {
        private const string Key = "op_sdk_mygame_0123456789abcdef";

        [TestCase("my-game " + Key)]
        [TestCase(Key + " my-game")]
        [TestCase("my-game\n" + Key + "\n")]
        [TestCase("my-game, " + Key)]
        [TestCase("App ID: my-game\nSDK key: " + Key)]
        [TestCase("SDK key: " + Key + "\r\nApp ID: my-game")]
        [TestCase("{\"app_id\":\"my-game\",\"sdk_key\":\"" + Key + "\"}")]
        [TestCase("{\"appId\":\"my-game\",\"sdkKey\":\"" + Key + "\",\"other\":1}")]
        [TestCase("openpush://setup?app=my-game&sdk=" + Key)]
        [TestCase("  openpush://setup?sdk=" + Key + "&app=my-game  ")]
        public void ReadsEveryConsoleShape(string text)
        {
            Assert.IsTrue(OpenPushSetupPaste.TryParse(text, out var values, out var problem), problem);
            Assert.AreEqual("my-game", values.AppId);
            Assert.AreEqual(Key, values.SdkKey);
            Assert.IsNull(values.Server);
        }

        [Test]
        public void TheLinkAndJsonCarryAServer()
        {
            Assert.IsTrue(OpenPushSetupPaste.TryParse("openpush://setup?app=a&sdk=b&server=https%3A%2F%2Fpush.example.com%2F", out var link, out _));
            Assert.AreEqual("https://push.example.com", link.Server);
            Assert.IsTrue(OpenPushSetupPaste.TryParse("{\"app\":\"a\",\"sdk\":\"b\",\"server\":\"https://x.test\"}", out var json, out _));
            Assert.AreEqual("https://x.test", json.Server);
        }

        [TestCase("")]
        [TestCase("just-one-value")]
        [TestCase("one two three")]
        [TestCase("{\"app_id\":\"a\"}")]
        [TestCase("openpush://setup?app=a")]
        [TestCase("https://example.com/setup?app=a&sdk=b")]
        [TestCase("{ broken")]
        [TestCase("hello world")]
        [TestCase("my-game op_sdk_othergame_0123456789abcdef")]
        [TestCase("Note: my-game " + "op_sdk_mygame_0123456789abcdef")]
        [TestCase("my-game sdk-dev")]
        public void AnythingElseIsRefused(string text)
        {
            Assert.IsFalse(OpenPushSetupPaste.TryParse(text, out var values, out var problem));
            Assert.IsNull(values);
            Assert.IsNotEmpty(problem);
        }
    }

    public class OpenPushGoogleServicesTests
    {
        private const string Json = @"{
  ""project_info"": { ""project_number"": ""123"", ""project_id"": ""game-prod"" },
  ""client"": [
    { ""client_info"": { ""mobilesdk_app_id"": ""1:123:android:aaa"", ""android_client_info"": { ""package_name"": ""com.example.other"" } },
      ""api_key"": [ { ""current_key"": ""KEY-OTHER"" } ] },
    { ""client_info"": { ""mobilesdk_app_id"": ""1:123:android:bbb"", ""android_client_info"": { ""package_name"": ""com.example.game"" } },
      ""api_key"": [ { ""current_key"": ""KEY-GAME"" } ] }
  ]
}";

        [Test]
        public void PicksTheClientForTheAppsPackage()
        {
            Assert.IsTrue(OpenPushGoogleServices.TryRead(Json, "com.example.game", out var values, out var problem), problem);
            Assert.AreEqual("game-prod", values.ProjectId);
            Assert.AreEqual("1:123:android:bbb", values.ApplicationId);
            Assert.AreEqual("KEY-GAME", values.ApiKey);
        }

        [Test]
        public void NoMatchingClientIsNotGuessed()
        {
            Assert.IsFalse(OpenPushGoogleServices.TryRead(Json, "com.example.missing", out var values, out var problem));
            Assert.IsNull(values);
            StringAssert.Contains("com.example.other, com.example.game", problem);
            Assert.IsFalse(OpenPushGoogleServices.TryRead("nope", "x", out _, out _));
        }
    }
}
