using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>Everything removing a previous push SDK touches, listed before anything is touched.</summary>
    internal sealed class OpenPushRemovalPlan
    {
        public string DisplayName;
        /// <summary>Packages/manifest.json dependencies and scoped-registry scopes to drop.</summary>
        public List<string> Packages = new List<string>(), Scopes = new List<string>();
        /// <summary>Files and folders to delete, relative to the project root (their .meta files go too).</summary>
        public List<string> Paths = new List<string>();
        /// <summary>Paths kept on purpose (icons the project reuses), with why.</summary>
        public List<string> Kept = new List<string>();
        /// <summary>Game scripts using the SDK's API: reported, never edited.</summary>
        public List<string> Scripts = new List<string>();
        /// <summary>The asmdefs those scripts belong to, which need the compatibility assembly referenced.</summary>
        public List<string> Asmdefs = new List<string>();

        public bool Empty => Packages.Count == 0 && Paths.Count == 0;

        /// <summary>Every path the dialog lists, one per line.</summary>
        internal IEnumerable<string> Lines()
        {
            foreach (var p in Packages) yield return "Packages/manifest.json: dependency " + p;
            foreach (var s in Scopes) yield return "Packages/manifest.json: scoped-registry scope " + s;
            foreach (var p in Paths) yield return p;
        }
    }

    /// <summary>A push SDK OpenPush can remove from a Unity project. Implementations live in Editor/Compat.</summary>
    internal interface IOpenPushVendorRemoval
    {
        string DisplayName { get; }
        /// <summary>The compatibility assembly that lets the SDK's call sites compile against OpenPush.</summary>
        string CompatAssembly { get; }
        /// <summary>
        /// What to remove from the project at <paramref name="root"/> (its Packages/manifest.json and folders).
        /// <paramref name="reuseIcons"/> keeps the notification icons the project reuses.
        /// </summary>
        OpenPushRemovalPlan Plan(string root, bool reuseIcons);
    }

    /// <summary>
    /// Carries out a removal plan: the manifest edit goes through the same parse-or-leave-alone JSON path as
    /// the EDM4U fix (an unparseable manifest is never rewritten), then each path is deleted with its .meta.
    /// </summary>
    internal static class OpenPushVendorRemover
    {
        /// <summary>False, with <paramref name="updated"/> null, when the manifest is not a JSON object.</summary>
        internal static bool TryRemovePackages(string manifestJson, IEnumerable<string> packages, IEnumerable<string> scopes, out string updated)
        {
            updated = null;
            if (!OpenPushJson.TryParse(manifestJson, out var parsed) || !(parsed is Dictionary<string, object> root)) return false;
            if (root.TryGetValue("dependencies", out var d) && d is Dictionary<string, object> deps)
                foreach (var package in packages) deps.Remove(package);
            if (root.TryGetValue("scopedRegistries", out var r) && r is List<object> registries)
            {
                foreach (var registry in registries.OfType<Dictionary<string, object>>().ToList())
                {
                    if (!(registry.TryGetValue("scopes", out var s) && s is List<object> list)) continue;
                    var before = list.Count;
                    list.RemoveAll(scope => scopes.Contains(scope as string));
                    // A registry the removal emptied serves nothing any more.
                    if (before > 0 && list.Count == 0) registries.Remove(registry);
                }
            }
            updated = OpenPushSetupFile.Pretty(OpenPushJson.WriteValueMap(root)) + "\n";
            return true;
        }

        /// <summary>
        /// Removes what the plan lists. <paramref name="delete"/> deletes one project-relative path (the editor
        /// moves Assets paths to the trash). Returns one line per thing done, or throws before touching
        /// anything when the manifest cannot be parsed.
        /// </summary>
        internal static List<string> Apply(string root, OpenPushRemovalPlan plan, Action<string> delete)
        {
            var done = new List<string>();
            if (plan.Packages.Count > 0 || plan.Scopes.Count > 0)
            {
                var manifest = Path.Combine(root, "Packages", "manifest.json");
                if (!TryRemovePackages(File.ReadAllText(manifest), plan.Packages, plan.Scopes, out var updated))
                    throw new InvalidOperationException("Packages/manifest.json could not be parsed as JSON, so nothing was removed. "
                                                      + "Fix the file and try again.");
                File.WriteAllText(manifest, updated);
                done.Add("Packages/manifest.json: removed " + string.Join(", ", plan.Packages.Concat(plan.Scopes)));
            }
            foreach (var path in plan.Paths)
            {
                delete(path);
                done.Add("removed " + path);
            }
            return done;
        }

        /// <summary>Deletes a project-relative file or folder and its .meta (the plain-file fallback).</summary>
        internal static void DeleteFromDisk(string root, string path)
        {
            var full = Path.Combine(root, path);
            if (Directory.Exists(full)) Directory.Delete(full, true);
            else if (File.Exists(full)) File.Delete(full);
            if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
        }

        /// <summary>Adds an assembly reference to an asmdef, parse-or-leave-alone. False when it cannot be parsed.</summary>
        internal static bool TryAddReference(string asmdefJson, string assembly, string guidReference, out string updated)
        {
            updated = null;
            if (!OpenPushJson.TryParse(asmdefJson, out var parsed) || !(parsed is Dictionary<string, object> root)) return false;
            if (!(root.TryGetValue("references", out var r) && r is List<object> references))
                root["references"] = references = new List<object>();
            if (!references.Contains(assembly) && (guidReference == null || !references.Contains(guidReference)))
                references.Add(guidReference ?? assembly);
            updated = OpenPushSetupFile.Pretty(OpenPushJson.WriteValueMap(root)) + "\n";
            return true;
        }
    }
}
