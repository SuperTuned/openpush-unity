using System.Collections.Generic;
using System.IO;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>Adds EDM4U from OpenUPM to Packages/manifest.json (never a .unitypackage download).</summary>
    internal static class OpenPushEdm4uManifest
    {
        internal const string ManifestPath = "Packages/manifest.json";
        internal const string Registry = "https://package.openupm.com";
        internal const string PackageName = "com.google.external-dependency-manager";
        internal const string Version = "1.2.189";

        internal static bool HasEdm4u(string manifestJson)
            => OpenPushJson.TryParse(manifestJson, out var parsed) && parsed is Dictionary<string, object> root
            && root.TryGetValue("dependencies", out var deps) && deps is Dictionary<string, object> map && map.ContainsKey(PackageName);

        /// <summary>
        /// Adds EDM4U to Packages/manifest.json in place. An unparseable manifest is left byte for byte
        /// as it is (rewriting it would drop every other dependency) and <paramref name="error"/> says why.
        /// </summary>
        internal static bool AddEdm4uToFile(string path, out string error)
        {
            if (!TryAddEdm4u(File.ReadAllText(path), out var updated))
            {
                error = path + " could not be parsed as JSON, so OpenPush left it unchanged. Fix the file, or add the OpenUPM scoped registry ("
                    + Registry + ", scope " + PackageName + ") and \"" + PackageName + "\": \"" + Version + "\" by hand.";
                return false;
            }
            File.WriteAllText(path, updated);
            error = null;
            return true;
        }

        /// <summary>The manifest unchanged when it cannot be parsed; see <see cref="TryAddEdm4u"/>.</summary>
        internal static string AddEdm4u(string manifestJson)
            => TryAddEdm4u(manifestJson, out var updated) ? updated : manifestJson;

        /// <summary>False, with <paramref name="updated"/> null, when the manifest is not a JSON object.</summary>
        internal static bool TryAddEdm4u(string manifestJson, out string updated)
        {
            updated = null;
            if (!OpenPushJson.TryParse(manifestJson, out var parsed) || !(parsed is Dictionary<string, object> root))
                return false;
            if (!(root.TryGetValue("scopedRegistries", out var regs) && regs is List<object> registries))
            { registries = new List<object>(); root["scopedRegistries"] = registries; }
            Dictionary<string, object> openUpm = null;
            foreach (var entry in registries)
                if (entry is Dictionary<string, object> r && r.TryGetValue("url", out var url) && (url as string)?.TrimEnd('/') == Registry)
                    openUpm = r;
            if (openUpm == null)
            {
                openUpm = new Dictionary<string, object> { ["name"] = "package.openupm.com", ["url"] = Registry, ["scopes"] = new List<object>() };
                registries.Add(openUpm);
            }
            if (!(openUpm.TryGetValue("scopes", out var sc) && sc is List<object> scopes)) { scopes = new List<object>(); openUpm["scopes"] = scopes; }
            if (!scopes.Contains(PackageName)) scopes.Add(PackageName);
            if (!(root.TryGetValue("dependencies", out var d) && d is Dictionary<string, object> deps)) { deps = new Dictionary<string, object>(); root["dependencies"] = deps; }
            if (!deps.ContainsKey(PackageName)) deps[PackageName] = Version;
            updated = OpenPushJson.WriteValueMap(root);
            return true;
        }
    }
}
