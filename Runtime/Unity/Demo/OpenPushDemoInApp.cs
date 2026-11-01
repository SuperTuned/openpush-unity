using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    internal static class OpenPushDemoInApp
    {
        internal static void Show()
        {
            var message = OpenPushDemoInAppFixture.Message();
            OpenPush.Hub.InAppLifecycle.Raise(new OpenPushInAppLifecycle(message.Id, "willDisplay"));
            OpenPushIamPresenter.Shared.Show(message, click =>
            {
                OpenPush.Hub.InAppClicked.Raise(new OpenPushInAppClicked(message.Id, click));
                if (click != null && click.Closes)
                {
                    OpenPushIamPresenter.Shared.Dismiss();
                    OpenPush.Hub.InAppLifecycle.Raise(new OpenPushInAppLifecycle(message.Id, "didDismiss"));
                }
            });
            OpenPush.Hub.InAppLifecycle.Raise(new OpenPushInAppLifecycle(message.Id, "didDisplay"));
        }
    }
}
