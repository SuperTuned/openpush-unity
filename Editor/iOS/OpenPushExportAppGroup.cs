#if UNITY_IOS
using System.Collections.Generic;
using System.Linq;
using OpenPushSDK.Core;
using UnityEditor.Build;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The App Group an export puts on the app and the extension when the host chose one: the group named in
    /// <see cref="OpenPushSettings.AppGroupIdentifier"/>, or, with <see cref="OpenPushSettings.AppGroupSource"/>
    /// Reused and no name, the group the app and its existing extension already share. Null means none was
    /// chosen and the service extension uses group.&lt;bundle id&gt;.openpush. Shared by the capability step and
    /// the extension step, so both always put the same group on both ends.
    /// </summary>
    internal static class OpenPushExportAppGroup
    {
        internal static string Configured(OpenPushSettings settings, string root)
        {
            if (settings == null) return null;
            // Whitespace-only is the same non-answer as empty: a field someone tabbed through.
            if (!string.IsNullOrWhiteSpace(settings.AppGroupIdentifier)) return settings.AppGroupIdentifier.Trim();
            if (settings.AppGroupSource != OpenPushPlumbingSource.Reused) return null;
            var export = OpenPushXcodeExport.Scan(root);
            var group = OpenPushAppGroupReuse.Resolve(
                export?.AppGroups ?? new List<string>(),
                (export?.HostServiceExtensions ?? Enumerable.Empty<OpenPushXcodeExtension>()).Select(e => (IReadOnlyList<string>)e.Groups),
                out var problem);
            if (group == null)
                throw new BuildFailedException("OpenPush: " + problem + " Choose the group in Window > OpenPush > Setup, "
                                             + "or set AppGroupSource to New.");
            return group;
        }
    }
}
#endif
