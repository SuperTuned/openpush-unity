using UnityEditor;
namespace OpenPushSDK.Editor
{
    public sealed class AndroidSdkLevels : OpenPushSetupCheck
    {
        public override int Order => 62;
        public override string Title => "Android API levels";
        public override bool Passes() => OpenPushAndroidChecks.SdkLevelsOk((int)PlayerSettings.Android.minSdkVersion, (int)PlayerSettings.Android.targetSdkVersion);
        public override string Detail => Passes()
            ? "Minimum API 24+, target API 34+ (Unity 2022.3 compiles against the target level)."
            : "OpenPush needs Minimum API Level 24 and Target API Level 34 or Automatic: androidx.startup 1.2.0 and androidx.browser 1.8.0 require compileSdk 34.";
        public override bool FixesItself => true;
        public override bool CanFix => !Passes();
        public override void Fix()
        {
            if ((int)PlayerSettings.Android.minSdkVersion < OpenPushAndroidChecks.MinSdk) PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            var target = (int)PlayerSettings.Android.targetSdkVersion;
            if (target != 0 && target < OpenPushAndroidChecks.TargetSdk) PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)OpenPushAndroidChecks.TargetSdk;
        }
    }
}
