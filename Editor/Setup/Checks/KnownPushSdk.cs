using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenPushSDK.Core;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// A known push SDK still installed beside OpenPush. Remove lists every package and path it will take out
    /// and asks before touching anything; the manifest edit never rewrites a file it cannot parse. The SDK's
    /// notification icons follow the Existing push setup answer (Reuse keeps them). Game code that calls the
    /// SDK is reported, never edited; the compatibility assembly can be referenced from its asmdefs so a
    /// using-line change is the only edit left.
    /// </summary>
    public sealed class KnownPushSdk : OpenPushSetupCheck
    {
        public override int Order => 2;
        public override string Title => "Previous push SDK";
        public override bool Passes() => true;
        public override string Detail => "No previous push SDK package or files found.";

        private static string Root => Directory.GetCurrentDirectory();

        private static bool ReuseIcons => OpenPushSetupAssets.Loadable()?.IconsSource == OpenPushPlumbingSource.Reused;

        private static readonly OpenPushStampedCache<List<(IOpenPushVendorRemoval Removal, OpenPushRemovalPlan Plan, List<string> Missing)>> Plans =
            new OpenPushStampedCache<List<(IOpenPushVendorRemoval, OpenPushRemovalPlan, List<string>)>>();

        /// Each removal's plan and the asmdefs missing the compat reference, recomputed only when assets change,
        /// Packages/manifest.json is written or the icons answer changes.
        private static List<(IOpenPushVendorRemoval Removal, OpenPushRemovalPlan Plan, List<string> Missing)> Current()
        {
            var reuseIcons = ReuseIcons;
            return Plans.Get(() => OpenPushVendorRemovals.All.Select(r =>
            {
                var plan = r.Plan(Root, reuseIcons);
                return (r, plan, MissingCompat(r, plan));
            }).ToList(), (reuseIcons, OpenPushProjectChangeStamp.WriteTicks(OpenPushEdm4uManifest.ManifestPath)));
        }

        public override IReadOnlyList<OpenPushSetupItem> Items()
        {
            var items = new List<OpenPushSetupItem>();
            foreach (var (removal, plan, compatMissing) in Current())
            {
                if (plan.Empty && compatMissing.Count == 0) continue;
                var detail = new List<string>();
                if (!plan.Empty)
                    detail.Add(plan.DisplayName + " is still installed (" + string.Join(", ", plan.Packages.Concat(plan.Paths)) + "). "
                             + "Remove lists everything it deletes and asks first.");
                if (plan.Kept.Count > 0) detail.Add("Kept: " + string.Join(", ", plan.Kept) + ".");
                if (plan.Scripts.Count > 0)
                    detail.Add(plan.Scripts.Count + " script" + (plan.Scripts.Count == 1 ? "" : "s") + " call its API ("
                             + string.Join(", ", plan.Scripts.Take(5)) + (plan.Scripts.Count > 5 ? ", …" : "") + "). They are not edited: "
                             + "change their using line to OpenPushSDK.Compat (see MIGRATING in the package).");
                if (compatMissing.Count > 0)
                    detail.Add("Use compat references " + removal.CompatAssembly + " from " + string.Join(", ", compatMissing) + ".");
                var buttons = new List<KeyValuePair<string, System.Action>>();
                if (!plan.Empty) buttons.Add(OpenPushSetupItem.Button("Remove " + plan.DisplayName, () => Remove(removal)));
                if (compatMissing.Count > 0) buttons.Add(OpenPushSetupItem.Button("Use compat", () => ReferenceCompat(removal, compatMissing)));
                items.Add(new OpenPushSetupItem("Previous push SDK: " + plan.DisplayName, string.Join(" ", detail), plan.Empty, buttons.ToArray()));
            }
            return items.Count > 0 ? items : null;
        }

        private static List<string> MissingCompat(IOpenPushVendorRemoval removal, OpenPushRemovalPlan plan)
        {
            var guid = CompatGuid(removal);
            return plan.Asmdefs.Select(ResolveAsmdef).Where(a => a != null).Distinct()
                .Where(a => File.Exists(a) && !File.ReadAllText(a).Contains("\"" + removal.CompatAssembly + "\"")
                            && (guid == null || !File.ReadAllText(a).Contains(guid)))
                .ToList();
        }

        /// <summary>An asmdef path as is; an .asmref resolved to the asmdef it extends (by GUID or by name).</summary>
        private static string ResolveAsmdef(string path)
        {
            if (!path.EndsWith(".asmref")) return path;
            var reference = OpenPushSDK.Core.OpenPushJson.TryGetStringField(File.ReadAllText(path), "reference");
            if (string.IsNullOrEmpty(reference)) return null;
            if (reference.StartsWith("GUID:")) return NullIfEmpty(AssetDatabase.GUIDToAssetPath(reference.Substring(5)));
            return AsmdefNamed(reference);
        }

        private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>The asmdef whose file is exactly <paramref name="name"/>.asmdef (FindAssets matches substrings).</summary>
        private static string AsmdefNamed(string name)
            => AssetDatabase.FindAssets(name + " t:AssemblyDefinitionAsset").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);

        private static string CompatGuid(IOpenPushVendorRemoval removal)
        {
            var path = AsmdefNamed(removal.CompatAssembly);
            return path == null ? null : "GUID:" + AssetDatabase.AssetPathToGUID(path);
        }

        private static void Remove(IOpenPushVendorRemoval removal)
        {
            var plan = removal.Plan(Root, ReuseIcons);
            var message = "OpenPush will remove " + plan.DisplayName + ":\n\n" + string.Join("\n", plan.Lines())
                + (plan.Kept.Count > 0 ? "\n\nKept: " + string.Join("\n", plan.Kept) : "")
                + (plan.Scripts.Count > 0 ? "\n\nThese scripts use its API and will not compile until they use OpenPush (they are not "
                   + "edited):\n" + string.Join("\n", plan.Scripts) : "")
                + "\n\nFiles under Assets go to the trash; the rest are deleted.";
            if (!EditorUtility.DisplayDialog("Remove " + plan.DisplayName, message, "Remove", "Cancel")) return;
            var done = OpenPushVendorRemover.Apply(Root, plan, path =>
            {
                if (!(path.StartsWith("Assets/") && AssetDatabase.MoveAssetToTrash(path)))
                    OpenPushVendorRemover.DeleteFromDisk(Root, path);
            });
            Debug.Log("OpenPush: " + string.Join("; ", done));
            AssetDatabase.Refresh();
            Client.Resolve();
            OpenPushExistingPlumbing.Invalidate();
        }

        private static void ReferenceCompat(IOpenPushVendorRemoval removal, IEnumerable<string> asmdefs)
        {
            var guid = CompatGuid(removal);
            foreach (var asmdef in asmdefs)
            {
                var text = File.ReadAllText(asmdef);
                if (!OpenPushVendorRemover.TryAddReference(text, removal.CompatAssembly, text.Contains("\"GUID:") ? guid : null, out var updated))
                {
                    Debug.LogError("OpenPush: " + asmdef + " could not be parsed as JSON, so it was left unchanged. Add "
                                 + removal.CompatAssembly + " to its references by hand.");
                    continue;
                }
                File.WriteAllText(asmdef, updated);
                AssetDatabase.ImportAsset(asmdef);
            }
        }
    }
}
