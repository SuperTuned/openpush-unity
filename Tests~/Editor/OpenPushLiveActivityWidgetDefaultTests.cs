using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    /// Who decides whether an iOS export gets the Live Activity widget: the settings asset, whatever value an
    /// existing one holds; a project without one gets no widget, unless an earlier export already has it.
    public class OpenPushLiveActivityWidgetDefaultTests
    {
        private const string App = "com.example.game";
        private const string Widget = "OpenPushLiveActivityWidget";
        private IosExportFixture _export;

        [SetUp]
        public void SetUp() => _export = new IosExportFixture(App);

        [TearDown]
        public void TearDown() => _export.Dispose();

        [Test]
        public void AnExportWithNoSettingsAssetHasNoWidgetTarget()
        {
            UnityEngine.Resources.StandIn = null;
            _export.Export();
            Assert.IsNull(_export.Project.TargetGuidByName(Widget));
            Assert.IsFalse(Directory.Exists(Path.Combine(_export.Root, Widget)), "no widget sources copied");
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.Contains("no Live Activity widget was added")),
                          "the build log says how to turn it on");
        }

        [Test]
        public void AnExistingAssetThatTurnsTheWidgetOnStillGetsIt()
        {
            // A game whose asset says 1 keeps its widget on upgrade.
            _export.Settings.InjectLiveActivityWidget = true;
            _export.Export();
            Assert.IsNotNull(_export.Project.TargetGuidByName(Widget));
            Assert.IsTrue(File.Exists(Path.Combine(_export.Root, Widget, "OpenPushDefaultWidget.swift")));
        }

        [Test]
        public void AnExistingAssetThatTurnsTheWidgetOffStillGetsNone()
        {
            _export.Settings.InjectLiveActivityWidget = false;
            _export.Export();
            Assert.IsNull(_export.Project.TargetGuidByName(Widget));
        }

        [Test]
        public void AnAppendExportWithoutAnAssetKeepsTheWidgetAnEarlierExportAdded()
        {
            UnityEngine.Resources.StandIn = null;
            var project = _export.Project;
            project.Targets[Widget] = "guid-" + Widget;
            project.WriteToFile(_export.ProjectPath);

            _export.Export();

            Assert.AreEqual("guid-" + Widget, _export.Project.TargetGuidByName(Widget));
            Assert.IsFalse(UnityEngine.Debug.Logged.Any(l => l.Contains("no Live Activity widget was added")),
                           "OpenPush's own widget target is still kept up to date");
        }
    }
}
