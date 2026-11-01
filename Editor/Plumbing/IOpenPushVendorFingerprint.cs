using System.Collections.Generic;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// What the Unity project offers a fingerprint to look at, so fingerprints stay free of AssetDatabase.
    /// </summary>
    internal interface IOpenPushUnityProjectView
    {
        string AppBundleId { get; }
        bool HasPackage(string name);
        bool HasAssetFolder(string path);
    }

    /// <summary>
    /// A known push SDK. Detection is generic and never needs one; a fingerprint only names the vendor of
    /// what was found (which makes TakeOver the default for its extension) and knows the names its own
    /// build step will create, before any export exists. Implementations live in Editor/Compat.
    /// </summary>
    internal interface IOpenPushVendorFingerprint
    {
        /// <summary>The vendor id written to <c>found[].vendor</c>.</summary>
        string Vendor { get; }

        /// <summary>True when an extension's source files are this vendor's template, not studio code.</summary>
        bool ClaimsServiceExtension(IEnumerable<string> sourceTexts);

        /// <summary>True when the group is the one this vendor names after the app.</summary>
        bool ClaimsAppGroup(string group, string appBundleId);

        /// <summary>
        /// What this vendor's editor build step will add at export, when its package is in the project:
        /// the extension, its bundle id and the App Group, from the vendor's own constants.
        /// </summary>
        IEnumerable<OpenPushSetupFinding> InUnityProject(IOpenPushUnityProjectView project);
    }
}
