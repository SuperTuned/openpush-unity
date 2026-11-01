using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// Names a game's project or build already holds and an upgrade must not move: the settings script's GUID
    /// (every OpenPushSettings.asset points at it), and the Java classes a game's keep rules and the C# layer
    /// reach by name over JNI.
    public class OpenPushStableNamesTests
    {
        private static string PackageRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Runtime", "Unity"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "package root not found above the test directory");
            return dir.FullName;
        }

        private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { PackageRoot() }.Concat(path).ToArray()));

        private static readonly string[] Java = { "Runtime", "Plugins", "Android", "OpenPushUnity.androidlib", "src", "main", "java", "ai", "openpush", "unity" };

        [Test]
        public void TheSettingsScriptKeepsTheGuidEveryExistingAssetReferences()
        {
            StringAssert.Contains("guid: 146e553ef78448a18ba577d1bb54e0f6\n", Read("Runtime", "Unity", "OpenPushSettings.cs.meta"));
            StringAssert.Contains("guid: 146e553ef78448a18ba577d1bb54e0f6",
                Read("Samples~", "FeatureLab", "OpenPushFeatureLab", "Resources", "OpenPushSettings.asset"));
        }

        [Test]
        public void TheAndroidCredentialStoreKeepsItsJavaNameAndCalls()
        {
            var source = Read(Java.Concat(new[] { "OpenPushCredentialStore.java" }).ToArray());
            StringAssert.StartsWith("package ai.openpush.unity;", source);
            StringAssert.Contains("public final class OpenPushCredentialStore", source);
            StringAssert.Contains("public static boolean save(Context context, String service, String value)", source);
            StringAssert.Contains("public static String load(Context context, String service)", source);
            StringAssert.Contains("public static void wipe(Context context, String service)", source);
            StringAssert.Contains("\"ai.openpush.unity.OpenPushCredentialStore\"", Read("Runtime", "Unity", "DeviceCredentialStore.cs"));
            StringAssert.Contains("-keep class ai.openpush.unity.OpenPushCredentialStore { *; }",
                Read("Runtime", "Plugins", "Android", "OpenPushUnity.androidlib", "consumer-proguard.pro"));
        }

        [Test]
        public void TheJavaAdapterClassesKeepTheNamesTheCSharpLayerCalls()
        {
            foreach (var name in new[] { "OpenPushUnityBridge", "OpenPushUnityListener", "OpenPushUnityCallback", "OpenPushUnityCompletion" })
            {
                var source = Read(Java.Concat(new[] { name + ".java" }).ToArray());
                Assert.IsTrue(Regex.IsMatch(source, @"public (final class|interface) " + name + @"\b"), name);
            }
            StringAssert.Contains("\"ai.openpush.unity.OpenPushUnityBridge\"", Read("Runtime", "Platforms", "Android", "AndroidBridgeClass.cs"));
        }
    }
}
