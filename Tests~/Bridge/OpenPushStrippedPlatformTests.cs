using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// <summary>
    /// A device player whose platform assembly the managed linker stripped falls back to the stub
    /// silently; Initialize must say so at Error level.
    /// </summary>
    public class OpenPushStrippedPlatformTests
    {
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            OpenPush.ResetForTests();
            OpenPushPlatformRegistry.ResetForTests();
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = "pk_asset" };
            _log = new List<string>();
            OpenPush.OnLog = _log.Add;
        }

        [TearDown]
        public void TearDown()
        {
            OpenPush.ResetForTests();
            OpenPushPlatformRegistry.ResetForTests();
        }

        [Test]
        public void DeviceBuildOnTheStubLogsTheStrippedPlatformAssemblyAtError()
        {
            OpenPushHost.DevicePlatformAssembly = "OpenPush.Android";
            OpenPush.LogLevel = OpenPushSDK.Core.OpenPushLogLevel.Error;
            OpenPush.Initialize("app-1");
            Assert.That(_log, Has.Some.Contains("OpenPush.Android").And.Contains("stripped").And.Contains("AlwaysLinkAssembly"));
        }

        [Test]
        public void HostInstallReportsTheStrippedAssemblyToTheUnityConsole()
        {
            // OnLog defaults to a no-op, so the Unity host also reports through Debug.LogError.
            var console = new List<string>();
            OpenPushHost.ReportError = console.Add;
            OpenPushHost.DevicePlatformAssembly = "OpenPush.iOS";
            OpenPush.ReportStrippedPlatform();
            Assert.That(console, Has.Exactly(1).Contains("OpenPush.iOS").And.Contains("stripped"));
        }

        [Test]
        public void HostInstallStaysQuietWhenThePlatformRegistered()
        {
            var console = new List<string>();
            OpenPushHost.ReportError = console.Add;
            OpenPushHost.DevicePlatformAssembly = "OpenPush.Android";
            OpenPushPlatformRegistry.Set(new FakePlatform());
            OpenPush.ReportStrippedPlatform();
            Assert.That(console, Is.Empty);
        }

        [Test]
        public void EditorAndDesktopOnTheStubStayQuiet()
        {
            OpenPush.Initialize("app-1");
            Assert.That(_log, Has.None.Contains("stripped"));
        }

        [Test]
        public void DeviceBuildWithItsPlatformRegisteredStaysQuiet()
        {
            OpenPushHost.DevicePlatformAssembly = "OpenPush.iOS";
            OpenPushPlatformRegistry.Set(new FakePlatform());
            OpenPush.Initialize("app-1");
            Assert.That(_log, Has.None.Contains("stripped"));
        }
    }
}
