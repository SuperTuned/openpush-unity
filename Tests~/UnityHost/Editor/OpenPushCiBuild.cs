using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Batchmode entry points for scripts/build-*-export.sh. Copied into a throwaway host project; never shipped.
public static class OpenPushCiBuild
{
    private static string Output => Path.Combine(Environment.GetEnvironmentVariable("OPENPUSH_EXPORT_DIR") ?? Path.GetFullPath(".."), "Build");

    public static void BuildAndroid()
    {
        try
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // OPENPUSH_MIN_SDK (default 24): 26 proves the build against natives still declaring minSdk 26.
            var minSdk = int.TryParse(Environment.GetEnvironmentVariable("OPENPUSH_MIN_SDK"), out var level) ? level : 24;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)minSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "ai.openpush.cihost");
            EditorUserBuildSettings.buildAppBundle = false;
            var minify = Environment.GetEnvironmentVariable("OPENPUSH_MINIFY") == "1";
            PlayerSettings.Android.minifyRelease = minify;
            EnsureSettings();
            OpenPushSDK.Editor.OpenPushAndroidDependencies.WriteNow();
            AssetDatabase.Refresh();
            if (!ForceResolve()) Fail("EDM4U resolve failed");
            var scene = CreateScene();
            Directory.CreateDirectory(Output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene }, target = BuildTarget.Android,
                locationPathName = Path.Combine(Output, "openpush-host.apk"),
                options = minify ? BuildOptions.None : BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded) Fail("Android build " + report.summary.result);
            Debug.Log("OpenPushCiBuild: Android build succeeded");
        }
        catch (Exception e) { Fail(e.ToString()); }
    }

    /// OPENPUSH_IOS_SDK=device|simulator (default simulator). EnsureSettings leaves InjectServiceExtension at
    /// its default (true), so the export also proves the extension links OpenPushCore + OpenPushNSE.
    public static void BuildIOS()
    {
        try
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "ai.openpush.cihost");
            PlayerSettings.iOS.targetOSVersionString = "16.0";
            PlayerSettings.iOS.sdkVersion = Environment.GetEnvironmentVariable("OPENPUSH_IOS_SDK") == "device"
                ? iOSSdkVersion.DeviceSDK : iOSSdkVersion.SimulatorSDK;
            EnsureSettings();
            var scene = CreateScene();
            var output = Path.Combine(Output, "ios");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = new[] { scene }, target = BuildTarget.iOS, locationPathName = output, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) Fail("iOS export " + report.summary.result);
            Debug.Log("OpenPushCiBuild: iOS export succeeded");
        }
        catch (Exception e) { Fail(e.ToString()); }
    }

    /// The spec 9.3 manual-signing export: Player Settings sign manually, the extension identifiers and
    /// profiles are set, and a host build step (ManualSigningHostGroups, order 0) leaves two App Groups on the
    /// app first. Export only (no profiles exist here); scripts/build-ios-manual-signing-export.sh asserts the
    /// pbxproj and entitlements.
    public static void BuildIOSManualSigning()
    {
        try
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "ai.openpush.cihost");
            PlayerSettings.iOS.targetOSVersionString = "16.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.appleDeveloperTeamID = "ABCDE12345";
            PlayerSettings.iOS.iOSManualProvisioningProfileID = "CI Host AppStore";
            EnsureSettings();
            var settings = AssetDatabase.LoadAssetAtPath<OpenPushSDK.OpenPushSettings>("Assets/Resources/OpenPushSettings.asset");
            settings.AppGroupIdentifier = "group.ai.openpush.cihost.legacy";
            settings.ServiceExtensionBundleIdentifier = "ai.openpush.cihost.LegacyNotificationServiceExtension";
            settings.ServiceExtensionProvisioningProfile = "CI Host NSE AppStore";
            settings.LiveActivityWidgetBundleIdentifier = "ai.openpush.cihost.Widgets";
            settings.LiveActivityWidgetProvisioningProfile = "CI Host Widget AppStore";
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            var scene = CreateScene();
            var output = Path.Combine(Output, "ios-manual");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = new[] { scene }, target = BuildTarget.iOS, locationPathName = output, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) Fail("iOS manual-signing export " + report.summary.result);
            Debug.Log("OpenPushCiBuild: iOS manual-signing export succeeded");
        }
        catch (Exception e) { Fail(e.ToString()); }
    }

    internal static void EnsureSettings()
    {
        Directory.CreateDirectory("Assets/Resources");
        const string path = "Assets/Resources/OpenPushSettings.asset";
        if (AssetDatabase.LoadAssetAtPath<OpenPushSDK.OpenPushSettings>(path) != null) return;
        var settings = ScriptableObject.CreateInstance<OpenPushSDK.OpenPushSettings>();
        settings.SdkKey = "ci-sdk-key";
        settings.AppId = "ci-app";
        settings.AndroidFirebaseProjectId = "ci-project";
        settings.AndroidFirebaseApplicationId = "1:000000000000:android:0000000000000000";
        settings.AndroidFirebaseApiKey = "ci-api-key";
        AssetDatabase.CreateAsset(settings, path);
        AssetDatabase.SaveAssets();
    }

    internal static string CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("OpenPush");
        var bootstrap = go.AddComponent<OpenPushSDK.OpenPushBootstrap>();
        bootstrap.AppId = "ci-app";
        const string path = "Assets/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    private static bool ForceResolve()
    {
        // EDM4U copies the Gradle templates into this folder and fails when it does not exist.
        Directory.CreateDirectory("Assets/Plugins/Android");
        var resolver = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GooglePlayServices.PlayServicesResolver", false)).FirstOrDefault(t => t != null);
        var method = resolver?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
        return method != null && method.Invoke(null, new object[] { true }) is bool ok && ok;
    }

    internal static void Fail(string message)
    {
        Debug.LogError("OpenPushCiBuild: " + message);
        EditorApplication.Exit(1);
    }
}

/// A host build step that already put two App Groups on the app before OpenPush's steps run (they must survive).
public sealed class ManualSigningHostGroups : UnityEditor.Build.IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS || !report.summary.outputPath.EndsWith("ios-manual")) return;
        const string relative = "Unity-iPhone/Unity-iPhone.entitlements";
        File.WriteAllText(Path.Combine(report.summary.outputPath, relative),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
          + "<plist version=\"1.0\"><dict><key>com.apple.security.application-groups</key><array>"
          + "<string>group.ai.openpush.cihost.shared</string><string>group.ai.openpush.cihost.analytics</string></array></dict></plist>\n");
        var path = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(report.summary.outputPath);
        var project = new UnityEditor.iOS.Xcode.PBXProject(); project.ReadFromFile(path);
        project.SetBuildProperty(project.GetUnityMainTargetGuid(), "CODE_SIGN_ENTITLEMENTS", relative);
        project.WriteToFile(path);
    }
}
