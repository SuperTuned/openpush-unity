using System;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>Every event the facade exposes, each a channel delivered on the main thread; all but Foreground buffer.</summary>
    internal sealed class OpenPushEventHub
    {
        public readonly OpenPushEventChannel<OpenPushLogLine> Log;
        public readonly OpenPushEventChannel<bool> Registered;
        public readonly OpenPushEventChannel<string> RegistrationFailed;
        public readonly OpenPushEventChannel<string> IdentityVerificationRequired;
        public readonly OpenPushEventChannel<OpenPushUserState> UserChanged;
        public readonly OpenPushEventChannel<PushSubscriptionState> SubscriptionCurrent;
        public readonly OpenPushEventChannel<OpenPushSubscriptionChange> SubscriptionChanged;
        public readonly OpenPushEventChannel<bool> PermissionChanged;
        public readonly OpenPushEventChannel<OpenPushClickEvent> Clicked;
        public readonly OpenPushEventChannel<OpenPushForegroundEvent> Foreground;
        public readonly OpenPushEventChannel<OpenPushBackgroundMessage> BackgroundMessage;
        public readonly OpenPushEventChannel<OpenPushInAppLifecycle> InAppLifecycle;
        public readonly OpenPushEventChannel<OpenPushInAppClicked> InAppClicked;

        internal OpenPushEventHub(Func<IOpenPushDispatcher> dispatcher, Action<string> onHandlerError)
        {
            Log = new OpenPushEventChannel<OpenPushLogLine>(dispatcher, _ => { });
            Registered = new OpenPushEventChannel<bool>(dispatcher, onHandlerError);
            RegistrationFailed = new OpenPushEventChannel<string>(dispatcher, onHandlerError);
            IdentityVerificationRequired = new OpenPushEventChannel<string>(dispatcher, onHandlerError);
            UserChanged = new OpenPushEventChannel<OpenPushUserState>(dispatcher, onHandlerError);
            SubscriptionCurrent = new OpenPushEventChannel<PushSubscriptionState>(dispatcher, onHandlerError);
            SubscriptionChanged = new OpenPushEventChannel<OpenPushSubscriptionChange>(dispatcher, onHandlerError);
            PermissionChanged = new OpenPushEventChannel<bool>(dispatcher, onHandlerError);
            Clicked = new OpenPushEventChannel<OpenPushClickEvent>(dispatcher, onHandlerError);
            // The one synchronous decision: answered once handlers return, or at once (displayed) when
            // nobody listens. Unbuffered: a late replay could no longer change the answer.
            Foreground = new OpenPushEventChannel<OpenPushForegroundEvent>(dispatcher, onHandlerError, e => e?.Finish(), buffered: false);
            // Buffered like Clicked: a background push that lands between Initialize and the first +=
            // is replayed to that handler rather than lost.
            BackgroundMessage = new OpenPushEventChannel<OpenPushBackgroundMessage>(dispatcher, onHandlerError);
            InAppLifecycle = new OpenPushEventChannel<OpenPushInAppLifecycle>(dispatcher, onHandlerError);
            InAppClicked = new OpenPushEventChannel<OpenPushInAppClicked>(dispatcher, onHandlerError);
        }

        internal void Reset()
        {
            Log.Reset(); Registered.Reset(); RegistrationFailed.Reset(); IdentityVerificationRequired.Reset();
            UserChanged.Reset(); SubscriptionCurrent.Reset(); SubscriptionChanged.Reset(); PermissionChanged.Reset();
            Clicked.Reset(); Foreground.Reset(); BackgroundMessage.Reset(); InAppLifecycle.Reset(); InAppClicked.Reset();
        }
    }
}
