using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushAndroidChecksTests
    {
        [TestCase("2022.3.14f1", true)]
        [TestCase("2022.3.0f1", true)]
        [TestCase("6000.0.54f1", true)]
        [TestCase("2023.2.1f1", true)]
        [TestCase("2022.2.9f1", false)]
        [TestCase("2021.3.40f1", false)]
        [TestCase("garbage", false)]
        public void UnityFloorIs20223(string version, bool ok) => Assert.AreEqual(ok, OpenPushAndroidChecks.UnityVersionSupported(version));

        [TestCase(24, 34, true)]
        [TestCase(26, 0, true)]
        [TestCase(23, 34, false)]
        [TestCase(24, 33, false)]
        public void SdkLevels(int min, int target, bool ok) => Assert.AreEqual(ok, OpenPushAndroidChecks.SdkLevelsOk(min, target));

        private static readonly string[] JsonOnly = { "Assets/google-services.json" };

        [Test]
        public void FirebaseConfigJsonAloneFailsAndSaysTheMetaDataIsThePath()
        {
            // A plain Unity export applies no google-services Gradle plugin, so the JSON alone never
            // produces the default FirebaseApp the native FCM module reuses.
            Assert.IsFalse(OpenPushAndroidChecks.FirebaseConfigOk(OpenPushAndroidPushDelivery.Automatic, JsonOnly, null, null, null));
            var problem = OpenPushAndroidChecks.FirebaseConfigProblem(OpenPushAndroidPushDelivery.Automatic, JsonOnly, null, null, null);
            StringAssert.Contains("google-services.json", problem);
            StringAssert.Contains("project id, application id and API key", problem);
            StringAssert.Contains("Firebase Unity", problem);
        }

        [Test]
        public void FirebaseConfigJsonCountsAlongsideFirebaseUnity()
        {
            Assert.IsTrue(OpenPushAndroidChecks.FirebaseConfigOk(OpenPushAndroidPushDelivery.Automatic,
                new[] { "Assets/google-services.json", "Assets/Plugins/Android/FirebaseApp.androidlib" }, null, null, null));
            Assert.IsTrue(OpenPushAndroidChecks.FirebaseConfigOk(OpenPushAndroidPushDelivery.Automatic,
                new[] { "Assets/google-services.json", "Assets/Plugins/Android/res/values/google-services.xml" }, null, null, null));
            Assert.IsNull(OpenPushAndroidChecks.FirebaseConfigProblem(OpenPushAndroidPushDelivery.Automatic,
                new[] { "Assets/google-services.json", "Assets/Plugins/Android/FirebaseApp.androidlib" }, null, null, null));
        }

        [Test]
        public void FirebaseConfigThreeValuesPass()
        {
            Assert.IsTrue(OpenPushAndroidChecks.FirebaseConfigOk(OpenPushAndroidPushDelivery.Automatic, new string[0], "p", "1:2:android:3", "k"));
            Assert.IsFalse(OpenPushAndroidChecks.FirebaseConfigOk(OpenPushAndroidPushDelivery.Automatic, new string[0], "p", "", "k"));
            StringAssert.Contains("project id, application id and API key",
                OpenPushAndroidChecks.FirebaseConfigProblem(OpenPushAndroidPushDelivery.Automatic, new string[0], null, null, null));
            Assert.IsTrue(OpenPushAndroidChecks.FirebaseConfigOk(OpenPushAndroidPushDelivery.HostOwned, new string[0], null, null, null));
        }

        [Test]
        public void Edm4uManifestEditLeavesAnUnparseableManifestUnchanged()
        {
            const string broken = "{\"dependencies\":{\"com.unity.ugui\":\"1.0.0\",}";
            Assert.IsFalse(OpenPushEdm4uManifest.TryAddEdm4u(broken, out _));
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openpush-manifest-" + System.Guid.NewGuid().ToString("N") + ".json");
            System.IO.File.WriteAllText(path, broken);
            try
            {
                var before = System.IO.File.ReadAllBytes(path);
                Assert.IsFalse(OpenPushEdm4uManifest.AddEdm4uToFile(path, out var error));
                StringAssert.Contains("could not be parsed", error);
                CollectionAssert.AreEqual(before, System.IO.File.ReadAllBytes(path));
            }
            finally { System.IO.File.Delete(path); }
        }

        [Test]
        public void Edm4uManifestFileEditWritesAParseableManifest()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openpush-manifest-" + System.Guid.NewGuid().ToString("N") + ".json");
            System.IO.File.WriteAllText(path, "{\"dependencies\":{\"com.unity.ugui\":\"1.0.0\"}}");
            try
            {
                Assert.IsTrue(OpenPushEdm4uManifest.AddEdm4uToFile(path, out var error));
                Assert.IsNull(error);
                var text = System.IO.File.ReadAllText(path);
                Assert.IsTrue(OpenPushEdm4uManifest.HasEdm4u(text));
                StringAssert.Contains("\"com.unity.ugui\":\"1.0.0\"", text);
            }
            finally { System.IO.File.Delete(path); }
        }

        [Test]
        public void Edm4uManifestEditAddsRegistryScopeAndDependencyOnce()
        {
            const string manifest = "{\"dependencies\":{\"com.unity.ugui\":\"1.0.0\"}}";
            Assert.IsFalse(OpenPushEdm4uManifest.HasEdm4u(manifest));
            var edited = OpenPushEdm4uManifest.AddEdm4u(manifest);
            Assert.IsTrue(OpenPushEdm4uManifest.HasEdm4u(edited));
            StringAssert.Contains("\"url\":\"https://package.openupm.com\"", edited);
            StringAssert.Contains("\"com.google.external-dependency-manager\":\"1.2.189\"", edited);
            StringAssert.Contains("\"com.unity.ugui\":\"1.0.0\"", edited);
            Assert.AreEqual(edited, OpenPushEdm4uManifest.AddEdm4u(edited));
        }

        [Test]
        public void Edm4uManifestEditKeepsAnExistingOpenUpmRegistryAndAddsTheScope()
        {
            const string manifest = "{\"scopedRegistries\":[{\"name\":\"package.openupm.com\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.example\"]}],\"dependencies\":{}}";
            var edited = OpenPushEdm4uManifest.AddEdm4u(manifest);
            StringAssert.Contains("\"com.example\"", edited);
            StringAssert.Contains("\"com.google.external-dependency-manager\"", edited);
            Assert.AreEqual(1, edited.Split("package.openupm.com\",\"url").Length - 1);
        }
    }
}
