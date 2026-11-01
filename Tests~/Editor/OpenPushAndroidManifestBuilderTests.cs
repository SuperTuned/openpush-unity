using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushAndroidManifestBuilderTests
    {
        private static OpenPushAndroidManifestInput Input(OpenPushAndroidPushDelivery delivery) => new OpenPushAndroidManifestInput
        {
            AppId = "app-1", SdkKey = "pk_1", FirebaseProjectId = "proj", FirebaseApplicationId = "1:2:android:3",
            FirebaseApiKey = "example-api-key", Delivery = delivery,
        };

        [Test]
        public void AutomaticWritesKeysAndFirebaseMetaData()
        {
            var xml = OpenPushAndroidManifestBuilder.Build(Input(OpenPushAndroidPushDelivery.Automatic));
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.app_id\" android:value=\"app-1\" />", xml);
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.sdk_key\" android:value=\"pk_1\" />", xml);
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.fcm.project_id\" android:value=\"proj\" />", xml);
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.fcm.application_id\" android:value=\"1:2:android:3\" />", xml);
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.fcm.api_key\" android:value=\"example-api-key\" />", xml);
            StringAssert.Contains("android.permission.POST_NOTIFICATIONS", xml);
            StringAssert.DoesNotContain("tools:node=\"remove\"", xml);
        }

        // RF5
        [Test]
        public void HostOwnedRemovesTheMessagingService()
        {
            var xml = OpenPushAndroidManifestBuilder.Build(Input(OpenPushAndroidPushDelivery.HostOwned));
            StringAssert.Contains("<service android:name=\"ai.openpush.android.fcm.OpenPushMessagingService\" tools:node=\"remove\" />", xml);
            StringAssert.DoesNotContain("ai.openpush.fcm.project_id", xml);
        }

        [Test]
        public void ForwardIsHostOwnedAndAlsoWritesTheNativeForwardValue()
        {
            Assert.AreEqual(OpenPushAndroidPushDelivery.HostOwned, OpenPushAndroidPushDelivery.Forward, "HostOwned settings keep their value");
            var xml = OpenPushAndroidManifestBuilder.Build(Input(OpenPushAndroidPushDelivery.Forward));
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.fcm.delivery\" android:value=\"forward\" />", xml);
            // Kept for hosts whose Gradle build still merges an FCM module with its own messaging service in.
            StringAssert.Contains("tools:node=\"remove\"", xml);
            StringAssert.DoesNotContain("ai.openpush.fcm.delivery", OpenPushAndroidManifestBuilder.Build(Input(OpenPushAndroidPushDelivery.Automatic)));
        }

        [Test]
        public void LaunchUrlsAreOpenedUnlessTheSettingTurnsThemOff()
        {
            var opened = OpenPushAndroidManifestBuilder.Build(Input(OpenPushAndroidPushDelivery.Automatic));
            StringAssert.DoesNotContain("ai.openpush.suppress_launch_urls", opened);

            var input = Input(OpenPushAndroidPushDelivery.Automatic);
            input.SuppressLaunchUrls = true;
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.suppress_launch_urls\" android:value=\"true\" />",
                                  OpenPushAndroidManifestBuilder.Build(input));
        }

        [Test]
        public void EmptyValuesAreOmittedAndValuesAreXmlEscaped()
        {
            var xml = OpenPushAndroidManifestBuilder.Build(new OpenPushAndroidManifestInput { AppId = "a&b\"c", Delivery = OpenPushAndroidPushDelivery.Automatic });
            StringAssert.Contains("android:value=\"a&amp;b&quot;c\"", xml);
            StringAssert.DoesNotContain("ai.openpush.sdk_key", xml);
            StringAssert.DoesNotContain("ai.openpush.fcm.api_key", xml);
        }
    }
}
