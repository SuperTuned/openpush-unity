using System;

namespace OpenPushSDK.Editor
{
    internal sealed class OpenPushResolveRecord
    {
        public string PackageVersion, PackagePath, DependenciesHash;
    }

    /// <summary>
    /// Whether EDM4U must re-resolve: a package upgrade moves the package to a new
    /// Library/PackageCache/ai.openpush.unity@hash folder, and the settings template EDM4U patched
    /// still names the old one. Stored per machine under Library/, so a teammate pulling an upgrade
    /// resolves too.
    /// </summary>
    internal static class OpenPushResolveState
    {
        internal const string StatePath = "Library/OpenPushResolveState.txt";

        internal static bool NeedsForcedResolve(OpenPushResolveRecord last, OpenPushResolveRecord current)
            => last == null || current == null
            || last.PackageVersion != current.PackageVersion
            || last.PackagePath != current.PackagePath
            || last.DependenciesHash != current.DependenciesHash;

        internal static string Serialize(OpenPushResolveRecord r)
            => "version=" + r.PackageVersion + "\npath=" + r.PackagePath + "\ndeps=" + r.DependenciesHash + "\n";

        internal static OpenPushResolveRecord Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string version = null, path = null, deps = null;
            foreach (var line in text.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var key = line.Substring(0, i); var value = line.Substring(i + 1).TrimEnd('\r');
                if (key == "version") version = value; else if (key == "path") path = value; else if (key == "deps") deps = value;
            }
            return version == null || path == null || deps == null ? null
                : new OpenPushResolveRecord { PackageVersion = version, PackagePath = path, DependenciesHash = deps };
        }
    }
}
