using System.Linq;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Unity's <c>ProjectCapabilityManager.AddAppGroups</c> replaces the entitlements file's
    /// <c>com.apple.security.application-groups</c> array with the groups it is given. Every caller hands it
    /// this list instead: the groups the file already has plus OpenPush's, or null when the file already has
    /// OpenPush's group and nothing needs writing. A host's other groups (and the profile that carries them)
    /// are never dropped.
    /// </summary>
    internal static class OpenPushAppGroupEntitlements
    {
        internal const string Key = "com.apple.security.application-groups";

        internal static string[] Merged(string root, string entitlements, string group)
        {
            var existing = OpenPushPlist.Strings(OpenPushPlist.Read(OpenPushXcodeExport.Full(root, entitlements)), Key);
            return existing.Contains(group) ? null : existing.Concat(new[] { group }).ToArray();
        }
    }
}
