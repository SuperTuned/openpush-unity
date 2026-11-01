using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;
namespace OpenPushSDK.Editor
{
    public sealed class Edm4uInstalled : OpenPushSetupCheck
    {
        private const string Manifest = OpenPushEdm4uManifest.ManifestPath;
        public override int Order => 60;
        public override string Title => "External Dependency Manager (Android)";

        private sealed class State
        {
            public bool Loaded, InManifest, Unparseable;
        }

        private static readonly OpenPushStampedCache<State> Cache = new OpenPushStampedCache<State>();

        /// <summary>
        /// Whether EDM4U is loaded (a walk over every loaded assembly) and named in Packages/manifest.json (a
        /// read and parse), cached until an asset changes, packages are registered or the manifest is
        /// written. Loading EDM4U reloads the domain, which starts the cache again.
        /// </summary>
        private static State Current() => Cache.Get(() =>
        {
            var text = File.Exists(Manifest) ? File.ReadAllText(Manifest) : null;
            return new State
            {
                Loaded = OpenPushEdm4u.Loaded,
                InManifest = text != null && OpenPushEdm4uManifest.HasEdm4u(text),
                Unparseable = text != null && !OpenPushEdm4uManifest.TryAddEdm4u(text, out _),
            };
        }, OpenPushProjectChangeStamp.WriteTicks(Manifest));

        public override bool Passes() => Current().Loaded || Current().InManifest;
        public override string Detail => Passes()
            ? "Installed. It resolves the vendored OpenPush natives and their dependencies into the Gradle build."
            : Current().Unparseable
            ? "Missing, and Packages/manifest.json could not be parsed as JSON, so Fix will not touch it. Repair the file, then add EDM4U " + OpenPushEdm4uManifest.Version + " from OpenUPM."
            : "Missing. OpenPush resolves its Android libraries through EDM4U " + OpenPushEdm4uManifest.Version + " from OpenUPM. Fix adds the scoped registry and the package to Packages/manifest.json.";
        public override bool FixesItself => true;
        // Added to the manifest, but Package Manager has not loaded it yet.
        public override bool FixPending => !Current().Loaded && Passes();
        public override bool CanFix => !Passes();
        public override string FixLabel => "Add EDM4U";
        public override void Fix()
        {
            if (!OpenPushEdm4uManifest.AddEdm4uToFile(Manifest, out var error))
            {
                Debug.LogError("OpenPush: " + error);
                return;
            }
            // The manifest is not an asset: say so to the rows that read it rather than wait for the resolve.
            OpenPushProjectChangeStamp.Bump();
            Client.Resolve();
        }
    }
}
