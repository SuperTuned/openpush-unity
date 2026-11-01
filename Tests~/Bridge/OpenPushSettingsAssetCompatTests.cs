using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// An upgrade never changes what an existing settings asset does. Unity builds the object (field
    /// initializers run) and then sets only the fields the asset file holds, so a field added after an
    /// asset was made takes its initializer: that initializer must be the behaviour the asset had before
    /// the field existed. New assets get today's defaults from CreateWithCurrentDefaults.
    public class OpenPushSettingsAssetCompatTests
    {
        private const string Header =
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
            "  m_ObjectHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 146e553ef78448a18ba577d1bb54e0f6, type: 3}\n" +
            "  m_Name: OpenPushSettings\n  m_EditorClassIdentifier:\n";

        // Shaped like a settings asset made with an early build: key, app id and the widget switch only.
        private const string EarliestAsset = Header +
            "  SdkKey: op_sdk_example\n  AppId: example-app\n  InjectLiveActivityWidget: 0\n";

        // Made with a build that had the Android delivery switch but no launch URL setting (Automatic chosen).
        private const string AutomaticAssetWithoutLaunchUrls = Header +
            "  SdkKey: op_sdk_example\n  AppId: example-app\n  AppGroupIdentifier:\n  InjectServiceExtension: 1\n" +
            "  InjectLiveActivityWidget: 1\n  AndroidPushDelivery: 0\n  AndroidFirebaseProjectId: example\n" +
            "  ReloginWithoutAuthHash: 1\n";

        // A project that owns FCM and never re-saved its asset after the launch URL setting arrived.
        private const string ForwardAssetWithoutLaunchUrls = Header +
            "  SdkKey: op_sdk_example\n  AppId: example-app\n  InjectLiveActivityWidget: 0\n  AndroidPushDelivery: 1\n";

        /// Unity's load: construct, then apply exactly the fields the file holds.
        internal static OpenPushSettings Load(string yaml)
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<OpenPushSettings>();
            var body = yaml.Split('\n').SkipWhile(l => !l.StartsWith("  m_EditorClassIdentifier")).Skip(1);
            foreach (var line in body.Where(l => l.StartsWith("  ") && l.Contains(':')))
            {
                var key = line.Substring(2, line.IndexOf(':') - 2);
                var value = line.Substring(line.IndexOf(':') + 1).Trim();
                var field = Field(key) ?? throw new AssertionException("the asset names an unknown field " + key);
                field.SetValue(settings, Parse(field.FieldType, value));
            }
            return settings;
        }

        /// Unity's save: every serialized field, in declaration order.
        internal static string Save(OpenPushSettings settings)
        {
            var lines = Serialized().Select(f => "  " + f.Name + ": " + Format(f.GetValue(settings)));
            return Header + string.Join("\n", lines) + "\n";
        }

        private static IEnumerable<FieldInfo> Serialized()
            => typeof(OpenPushSettings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.IsPublic || f.GetCustomAttribute<UnityEngine.SerializeField>() != null)
                .OrderBy(f => f.MetadataToken);

        private static FieldInfo Field(string name) => Serialized().FirstOrDefault(f => f.Name == name);

        private static object Parse(Type type, string value)
        {
            if (type == typeof(string)) return value;
            if (type == typeof(bool)) return value == "1";
            if (type == typeof(int)) return int.Parse(value);
            if (type.IsEnum) return Enum.ToObject(type, int.Parse(value));
            throw new NotSupportedException(type.Name);
        }

        private static string Format(object value)
            => value is bool b ? (b ? "1" : "0") : value is Enum e ? Convert.ToInt32(e).ToString() : value?.ToString() ?? "";

        [Test]
        public void AnAssetWithoutTheLaunchUrlSettingOpensLaunchUrls()
        {
            // Opening a tapped push's launch URL is the default whether or not the asset holds the field:
            // 1.1.6 to 1.2.1 assets that never stored it already opened URLs, and earlier ones now do too.
            foreach (var yaml in new[] { EarliestAsset, AutomaticAssetWithoutLaunchUrls, ForwardAssetWithoutLaunchUrls })
                Assert.IsTrue(Load(yaml).OpenLaunchUrls, "launch URLs open natively unless the asset turns it off");
        }

        [Test]
        public void AnAssetWithoutTheDeliverySwitchReadsAsAutomatic()
        {
            var settings = Load(EarliestAsset);
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, settings.AndroidPushDelivery,
                            "OpenPush owns FCM, as it did on 1.1.5 to 1.2.1: the SDK's FCM service must not be dropped");
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, settings.Snapshot().AndroidPushDelivery);
        }

        [Test]
        public void ValuesAnAssetHoldsAlwaysWin()
        {
            var automatic = Load(AutomaticAssetWithoutLaunchUrls);
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, automatic.AndroidPushDelivery);
            Assert.IsTrue(automatic.InjectLiveActivityWidget);
            Assert.AreEqual("example", automatic.AndroidFirebaseProjectId);
            var forward = Load(ForwardAssetWithoutLaunchUrls);
            Assert.AreEqual(OpenPushAndroidPushDelivery.Forward, forward.AndroidPushDelivery);
            Assert.IsFalse(forward.InjectLiveActivityWidget);
            Assert.IsFalse(Load(ForwardAssetWithoutLaunchUrls.Replace("  AndroidPushDelivery: 1\n",
                                                                       "  AndroidPushDelivery: 1\n  OpenLaunchUrls: 0\n")).OpenLaunchUrls,
                           "an asset that turned launch URLs off keeps them off");
        }

        [Test]
        public void FieldsThatAlreadyExistedKeepTheirInitializers()
        {
            var settings = Load(EarliestAsset);
            Assert.IsTrue(settings.InjectServiceExtension);
            Assert.AreEqual(OpenPushIosExtensionMode.Inject, settings.IosExtensionMode);
            Assert.AreEqual(OpenPushPlumbingSource.New, settings.AppGroupSource);
            Assert.IsTrue(settings.ReloginWithoutAuthHash);
            Assert.AreEqual(0, settings.SettingsVersion, "an unversioned asset reads as version 0");
        }

        [Test]
        public void ANewAssetGetsTodaysDefaultsAndKeepsThemThroughASave()
        {
            var created = OpenPushSettings.CreateWithCurrentDefaults();
            Assert.IsTrue(created.OpenLaunchUrls);
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, created.AndroidPushDelivery);
            Assert.IsFalse(created.InjectLiveActivityWidget, "Live Activity widget off until turned on");
            Assert.IsTrue(created.InjectServiceExtension);
            Assert.AreEqual(OpenPushSettings.CurrentSettingsVersion, created.SettingsVersion);

            var saved = Save(created);
            StringAssert.Contains("  settingsVersion: 1\n", saved);
            var reloaded = Load(saved);
            Assert.IsTrue(reloaded.OpenLaunchUrls);
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, reloaded.AndroidPushDelivery);
            Assert.IsFalse(reloaded.InjectLiveActivityWidget);
            Assert.AreEqual(OpenPushSettings.CurrentSettingsVersion, reloaded.SettingsVersion);
        }

        [Test]
        public void AnOldAssetResavedByUnityKeepsItsEarlierBehaviour()
        {
            // Editing any field makes Unity write every field: the missing ones are written as their initializers.
            var resaved = Load(Save(Load(EarliestAsset)));
            Assert.IsTrue(resaved.OpenLaunchUrls);
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, resaved.AndroidPushDelivery);
            Assert.IsFalse(resaved.InjectLiveActivityWidget);
        }

        [Test]
        public void TheShippedSampleAssetStatesEveryFieldItReliesOn()
        {
            var path = Path.Combine(PackageRoot(), "Samples~", "FeatureLab", "OpenPushFeatureLab", "Resources", "OpenPushSettings.asset");
            var sample = Load(File.ReadAllText(path));
            Assert.AreEqual(OpenPushAndroidPushDelivery.Automatic, sample.AndroidPushDelivery);
            Assert.IsTrue(sample.OpenLaunchUrls);
            Assert.IsTrue(sample.InjectLiveActivityWidget, "the sample shows Live Activities");
            Assert.AreEqual(OpenPushSettings.CurrentSettingsVersion, sample.SettingsVersion);
        }

        private static string PackageRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            // package.json is also copied next to the test assembly, so look for the package's own Runtime folder.
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Runtime", "Unity"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "package root not found above the test directory");
            return dir.FullName;
        }
    }
}
