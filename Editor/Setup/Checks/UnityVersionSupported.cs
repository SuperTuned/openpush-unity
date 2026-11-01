using UnityEngine;
namespace OpenPushSDK.Editor
{
    public sealed class UnityVersionSupported : OpenPushSetupCheck
    {
        public override int Order => 5;
        public override string Title => "Unity 2022.3 or newer";
        public override bool Passes() => OpenPushAndroidChecks.UnityVersionSupported(Application.unityVersion);
        public override string Detail => Passes()
            ? "Unity " + Application.unityVersion + " is supported."
            : "Unity " + Application.unityVersion + " is below the 2022.3 floor: its Android Gradle plugin cannot build the OpenPush natives. Upgrade to 2022.3 LTS or newer.";
    }
}
