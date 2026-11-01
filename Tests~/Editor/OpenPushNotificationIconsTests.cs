using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushNotificationIconsTests
    {
        private const string Lib = "Assets/Plugins/Android/Config.androidlib/src/main/res/";

        private static OpenPushNotificationIconReport Report(params string[] paths)
            => OpenPushNotificationIcons.Inspect(OpenPushAndroidPushDelivery.Automatic, paths);

        [Test]
        public void OpenPushNamesAreFound()
        {
            var report = Report(Lib + "drawable-xxhdpi/ic_stat_openpush_default.png",
                                Lib + "drawable-xxxhdpi/ic_openpush_large_icon_default.png");
            Assert.IsTrue(report.Passes);
            Assert.AreEqual(OpenPushIconSource.OpenPush, report.Small);
            Assert.AreEqual(OpenPushIconSource.OpenPush, report.Large);
            StringAssert.Contains("ic_stat_openpush_default", report.Detail);
        }

        [Test]
        public void AMigratedProjectsIconsAreUsedAsIs()
        {
            var report = Report(Lib + "drawable-mdpi/" + OpenPushMigratedIconNames.StatusIcon + ".png",
                                Lib + "drawable-xxxhdpi/" + OpenPushMigratedIconNames.LargeIcon + ".png");
            Assert.IsTrue(report.Passes);
            Assert.AreEqual(OpenPushIconSource.Migrated, report.Small);
            Assert.AreEqual(OpenPushIconSource.Migrated, report.Large);
            StringAssert.Contains(OpenPushMigratedIconNames.StatusIcon, report.Detail);
            StringAssert.Contains("ic_stat_openpush_default", report.Detail, "says what to rename to, whenever convenient");
        }

        [Test]
        public void OpenPushNamesWinOverMigratedOnes()
        {
            var report = Report(Lib + "drawable/" + OpenPushMigratedIconNames.StatusIcon + ".png",
                                Lib + "drawable/ic_stat_openpush_default.xml");
            Assert.AreEqual(OpenPushIconSource.OpenPush, report.Small);
        }

        [Test]
        public void NoStatusIconFailsAndExplainsTheWhiteSquare()
        {
            var report = Report(Lib + "drawable-xxxhdpi/ic_openpush_large_icon_default.png");
            Assert.IsFalse(report.Passes);
            Assert.AreEqual(OpenPushIconSource.None, report.Small);
            StringAssert.Contains("white", report.Detail);
            StringAssert.Contains("ic_stat_openpush_default", report.Detail);
        }

        [Test]
        public void AMissingLargeIconIsOptional()
        {
            var report = Report(Lib + "drawable-hdpi/ic_stat_openpush_default.png");
            Assert.IsTrue(report.Passes);
            Assert.AreEqual(OpenPushIconSource.None, report.Large);
            StringAssert.Contains("optional", report.Detail);
        }

        [Test]
        public void OnlyFilesInAnAndroidResourceFolderCount()
        {
            var report = Report("Assets/Art/ic_stat_openpush_default.png",
                                Lib + "values/ic_stat_openpush_default.xml",
                                Lib + "drawable-xxhdpi/ic_stat_openpush_default_old.png",
                                "Assets/Plugins/Android/res/mipmap-hdpi/ic_stat_openpush_default.webp");
            Assert.AreEqual(OpenPushIconSource.OpenPush, report.Small, "the mipmap in a res folder counts");
            Assert.AreEqual(OpenPushIconSource.None,
                            Report("Assets/Art/ic_stat_openpush_default.png", Lib + "values/ic_stat_openpush_default.xml").Small);
        }

        [Test]
        public void HostOwnedDeliveryPassesWithoutLooking()
        {
            var report = OpenPushNotificationIcons.Inspect(OpenPushAndroidPushDelivery.HostOwned, new string[0]);
            Assert.IsTrue(report.Passes);
            StringAssert.Contains("Forward", report.Detail);
        }
    }
}
