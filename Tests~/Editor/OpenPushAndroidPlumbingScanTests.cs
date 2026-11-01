using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushAndroidPlumbingScanTests
    {
        private const string Manifest =
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.example.game\">\n" +
            "  <application>\n" +
            "    <service android:name=\".push.GameMessagingService\" android:exported=\"false\">\n" +
            "      <intent-filter><action android:name=\"com.google.firebase.MESSAGING_EVENT\" /></intent-filter>\n" +
            "    </service>\n" +
            "    <service android:name=\"ai.openpush.android.fcm.OpenPushMessagingService\">\n" +
            "      <intent-filter><action android:name=\"com.google.firebase.MESSAGING_EVENT\" /></intent-filter>\n" +
            "    </service>\n" +
            "    <service android:name=\"com.example.game.SyncService\" />\n" +
            "  </application>\n" +
            "</manifest>";

        private const string Java = "package com.example.game.push;\n\npublic class GameMessagingService extends FirebaseMessagingService {\n}\n";
        private const string Kotlin = "package com.example.other\n\nclass OtherService : com.google.firebase.messaging.FirebaseMessagingService() {\n}\n";

        [Test]
        public void ServicesOnMessagingEventAreFoundAndOpenPushsIsNot()
        {
            CollectionAssert.AreEqual(new[] { "com.example.game.push.GameMessagingService", "ai.openpush.android.fcm.OpenPushMessagingService" },
                                      OpenPushAndroidPlumbingScan.MessagingServices(Manifest));
            Assert.AreEqual("com.example.game.push.GameMessagingService", OpenPushAndroidPlumbingScan.MessagingServiceClass(Java));
            Assert.AreEqual("com.example.other.OtherService", OpenPushAndroidPlumbingScan.MessagingServiceClass(Kotlin));
            Assert.IsNull(OpenPushAndroidPlumbingScan.MessagingServiceClass("class Foo extends Service {}"));
        }

        [Test]
        public void FindingsMarkServicesWhoseSourceIsInAssetsAsEditable()
        {
            var findings = OpenPushAndroidPlumbingScan.Findings(
                new[] { ("Assets/Plugins/Android/Game.androidlib/AndroidManifest.xml", Manifest) },
                new[] { ("Assets/Plugins/Android/Game.androidlib/src/GameMessagingService.java", Java) },
                new[] { "Assets/google-services.json", "Assets/Plugins/Android/Game.androidlib/res/drawable-mdpi/ic_stat_game.png",
                        "Assets/Plugins/Android/Game.androidlib/res/drawable-hdpi/ic_stat_game.png",
                        "Assets/Plugins/Android/OpenPushIcons.androidlib/res/drawable/ic_stat_openpush_default.png" },
                firebaseUnityMessaging: true);

            var services = findings.Where(f => f.Kind == OpenPushSetupFinding.FcmService).ToList();
            Assert.AreEqual(2, services.Count);
            Assert.IsTrue(services[0].Editable);
            Assert.AreEqual("Assets/Plugins/Android/Game.androidlib/src/GameMessagingService.java", services[0].Detail("source_file"));
            Assert.AreEqual(OpenPushAndroidPlumbingScan.FirebaseUnityService, services[1].Id);
            Assert.IsFalse(services[1].Editable);
            Assert.AreEqual("Assets/google-services.json", findings.Single(f => f.Kind == OpenPushSetupFinding.FirebaseApp).Id);
            var icon = findings.Single(f => f.Kind == OpenPushSetupFinding.NotificationIcon);
            Assert.AreEqual("ic_stat_game", icon.Id);
            Assert.AreEqual(2, icon.Strings("files").Count);
        }

        [Test]
        public void ABrokenManifestFindsNothing()
            => Assert.IsEmpty(OpenPushAndroidPlumbingScan.MessagingServices("<manifest"));
    }
}
