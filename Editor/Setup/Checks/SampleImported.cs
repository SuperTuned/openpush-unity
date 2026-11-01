using System.IO;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Whether the Feature Lab sample has been imported. Optional, but it is the
    /// fastest way to see permission, subscription, identity, tags and Live
    /// Updates working against a real app before writing any code.
    /// </summary>
    public sealed class SampleImported : OpenPushSetupCheck
    {
        public override int Order => 60;
        public override string Title => "Feature Lab sample (optional)";

        // Package Manager imports a sample under Assets/Samples/<display name>/
        // <version>/<sample name>, so the version segment is a wildcard.
        private const string Root = "Assets/Samples/OpenPush";
        private const string SampleFolder = "Feature Lab";

        private static string ImportedPath()
        {
            if (!Directory.Exists(Root)) return null;
            foreach (var version in Directory.GetDirectories(Root))
            {
                var candidate = Path.Combine(version, SampleFolder);
                if (Directory.Exists(candidate)) return candidate.Replace('\\', '/');
            }
            return null;
        }

        public override bool Passes() => ImportedPath() != null;

        public override string Detail
        {
            get
            {
                var imported = ImportedPath();
                return imported != null
                    ? "Imported at " + imported + "."
                    : "Not imported. Optional — the SDK works without it. Import opens "
                    + "Package Manager on the OpenPush package; the sample is under Samples.";
            }
        }

        public override bool CanFix => !Passes();
        public override string FixLabel => "Import";

        public override void Fix()
        {
            if (Passes()) return;
            UnityEditor.PackageManager.UI.Window.Open(OpenPushSetupAssets.PackageName);
        }
    }
}
