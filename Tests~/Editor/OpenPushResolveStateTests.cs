using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushResolveStateTests
    {
        private static OpenPushResolveRecord R(string version, string path, string hash)
            => new OpenPushResolveRecord { PackageVersion = version, PackagePath = path, DependenciesHash = hash };

        // RF5: the PackageCache folder hash changes on every upgrade and EDM4U's patched
        // settingsTemplate.gradle still names the old folder.
        [Test]
        public void PackageVersionChangeRequiresForcedResolve()
        {
            var before = R("1.2.2", "Library/PackageCache/ai.openpush.unity@e80514a94688", "h");
            Assert.IsTrue(OpenPushResolveState.NeedsForcedResolve(before, R("1.2.3", "Library/PackageCache/ai.openpush.unity@0f1e2d3c4b5a", "h")));
            Assert.IsTrue(OpenPushResolveState.NeedsForcedResolve(before, R("1.2.2", "Library/PackageCache/ai.openpush.unity@0f1e2d3c4b5a", "h")));
            Assert.IsTrue(OpenPushResolveState.NeedsForcedResolve(before, R("1.2.2", before.PackagePath, "changed")));
            Assert.IsFalse(OpenPushResolveState.NeedsForcedResolve(before, R("1.2.2", before.PackagePath, "h")));
        }

        [Test]
        public void NoRecordMeansResolve()
            => Assert.IsTrue(OpenPushResolveState.NeedsForcedResolve(null, R("1.2.2", "p", "h")));

        [Test]
        public void RecordRoundTripsAndToleratesGarbage()
        {
            var r = R("1.2.2", "Packages/ai.openpush.unity", "abc");
            var back = OpenPushResolveState.Parse(OpenPushResolveState.Serialize(r));
            Assert.IsFalse(OpenPushResolveState.NeedsForcedResolve(back, r));
            Assert.IsNull(OpenPushResolveState.Parse("not a record"));
            Assert.IsNull(OpenPushResolveState.Parse(null));
        }
    }
}
