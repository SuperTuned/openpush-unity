namespace OpenPushSDK.Editor
{
    public sealed class Edm4uResolved : OpenPushSetupCheck
    {
        public override int Order => 61;
        public override string Title => "Android dependencies resolved";
        private static readonly OpenPushStampedCache<(bool Loaded, bool Resolved)> Cache = new OpenPushStampedCache<(bool, bool)>();

        /// Walks the loaded assemblies, reads the last resolve's record and hashes the dependencies file, so
        /// it is cached until an asset changes or either file is written (a resolve records itself).
        private static (bool Loaded, bool Resolved) Current() => Cache.Get(() =>
        {
            var loaded = OpenPushEdm4u.Loaded;
            return (loaded, loaded && !OpenPushResolveState.NeedsForcedResolve(OpenPushEdm4u.Last(), OpenPushEdm4u.Current()));
        }, (OpenPushProjectChangeStamp.WriteTicks(OpenPushResolveState.StatePath), OpenPushProjectChangeStamp.WriteTicks(OpenPushAndroidDependencies.OutputPath)));

        public override bool Passes() => Current().Resolved;
        public override string Detail => !Current().Loaded
            ? "Install EDM4U first (row above)."
            : Passes() ? "Resolved for this package version and delivery mode."
            : "Not resolved since the OpenPush package or its delivery mode changed. The Gradle settings template may still point at an old package folder.";
        public override bool FixesItself => true;
        public override bool CanFix => Current().Loaded && !Passes();
        public override string FixLabel => "Resolve";
        public override void Fix() { OpenPushAndroidDependencies.WriteNow(); OpenPushEdm4u.ForceResolve(); }
    }
}
