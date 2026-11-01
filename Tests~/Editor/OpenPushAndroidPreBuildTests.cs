using System;
using System.IO;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace OpenPushSDK.Tests
{
    /// <summary>
    /// A single-shot batch build (-batchmode -quit -executeMethod) never runs the [InitializeOnLoad]
    /// delayCall, so the pre-build hook writes the EDM4U dependency file (and resolves) itself.
    /// </summary>
    public class OpenPushAndroidPreBuildTests
    {
        private string _previous, _project;

        [SetUp]
        public void SetUp()
        {
            _previous = Directory.GetCurrentDirectory();
            _project = Path.Combine(Path.GetTempPath(), "openpush-prebuild-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_project);
            Directory.SetCurrentDirectory(_project);
            OpenPushEdm4u.StandInLoaded = false;
            OpenPushEdm4u.StandInResolves = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.SetCurrentDirectory(_previous);
            Directory.Delete(_project, true);
        }

        private static BuildReport Report(BuildTarget target)
        {
            var report = new BuildReport();
            report.summary.platform = target;
            return report;
        }

        [Test]
        public void AndroidBuildWritesTheDependencyFileWhenAbsent()
        {
            new OpenPushAndroidPreBuild().OnPreprocessBuild(Report(BuildTarget.Android));
            Assert.IsTrue(File.Exists(OpenPushAndroidDependencies.OutputPath));
            StringAssert.Contains(OpenPushAndroidDependenciesXml.Build(OpenPushAndroidPushDelivery.Automatic),
                File.ReadAllText(OpenPushAndroidDependencies.OutputPath));
        }

        [Test]
        public void AndroidBuildResolvesWhenEdm4uIsLoaded()
        {
            OpenPushEdm4u.StandInLoaded = true;
            new OpenPushAndroidPreBuild().OnPreprocessBuild(Report(BuildTarget.Android));
            Assert.AreEqual(1, OpenPushEdm4u.StandInResolves);
        }

        [Test]
        public void OtherTargetsAreLeftAlone()
        {
            OpenPushEdm4u.StandInLoaded = true;
            new OpenPushAndroidPreBuild().OnPreprocessBuild(Report(BuildTarget.iOS));
            Assert.IsFalse(File.Exists(OpenPushAndroidDependencies.OutputPath));
            Assert.AreEqual(0, OpenPushEdm4u.StandInResolves);
        }

        [Test]
        public void RunsBeforeEdm4uOwnPreBuildStep() => Assert.Less(new OpenPushAndroidPreBuild().callbackOrder, 0);
    }
}
