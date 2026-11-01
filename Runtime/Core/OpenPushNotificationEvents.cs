using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenPushSDK.Core
{
    /// <summary>A notification the user tapped.</summary>
    public sealed class OpenPushClickEvent
    {
        public string MessageId;
        public IReadOnlyDictionary<string, string> Data;

        /// <summary>
        /// The composed notification behind the tap, with the wire keys already
        /// named. Never null; <see cref="Data"/> stays the raw payload.
        /// </summary>
        public OpenPushNotification Notification;

        /// <summary>
        /// The id of the action button that was tapped, or null when the user
        /// tapped the notification itself. iOS fills this in from the response
        /// identifier; on Android the host's renderer supplies it as an
        /// <c>op_action_id</c> extra on the action's PendingIntent.
        /// </summary>
        public string ActionId;

        /// <summary>
        /// The URL the message opens (<c>deep_link</c>), or null when it carries
        /// none. This is the message's URL whether or not an action button was
        /// tapped; a button's own link is <see cref="ActionUrl"/>.
        /// </summary>
        public string Url;

        /// <summary>
        /// The tapped button's own link (its <c>url</c> in the composer), or null for a tap on the
        /// notification itself or on a button without one. While the SDK opens launch URLs
        /// (<c>OpenPushSettings.OpenLaunchUrls</c>, on by default) it opens this link in place of
        /// <see cref="Url"/>; a button without a link opens <see cref="Url"/> as before.
        /// </summary>
        public string ActionUrl;

        /// <summary>
        /// True when the SDK already opened a URL for this tap — <see cref="ActionUrl"/> when the
        /// tapped button has one, <see cref="Url"/> otherwise — so a handler should not call
        /// <c>Application.OpenURL</c> with it again. Turn the SDK's opening off with
        /// <c>OpenPushSettings.OpenLaunchUrls</c> to route every URL yourself.
        /// </summary>
        public bool LaunchUrlOpened;
    }

    /// <summary>
    /// A background (data-only) push: one sent with <c>op_background</c> = <c>"1"</c> and no title or
    /// body. It is never displayed, and the SDK has already posted its <c>received</c> receipt.
    /// Android only; iOS background pushes use <c>content-available</c> and are not raised here.
    /// </summary>
    public sealed class OpenPushBackgroundMessage
    {
        public string MessageId;

        /// <summary>The delivered data map, untouched: the composer's custom data is in it.</summary>
        public IReadOnlyDictionary<string, string> Data;

        /// <summary>
        /// The push read into named fields. Never null; <see cref="OpenPushNotification.IsBackground"/>
        /// is true.
        /// </summary>
        public OpenPushNotification Notification;
    }

    /// <summary>
    /// A notification that arrived while the app was in the foreground. Handlers run on the Unity
    /// main thread; when they return, the SDK answers the native side once: displayed unless a
    /// handler called <see cref="PreventDefault"/>. <see cref="Display"/> after a prevented answer
    /// presents it later where the platform can (iOS, within the native 25 second window).
    /// </summary>
    public sealed class OpenPushForegroundEvent
    {
        public string MessageId;
        public IReadOnlyDictionary<string, string> Data;
        /// <summary>
        /// The composed notification, with the wire keys already named — what a
        /// host presenting its own UI needs to draw it. Never null.
        /// </summary>
        public OpenPushNotification Notification;
        public bool DefaultPrevented { get; private set; }

        /// <summary>The native decision id; null for events the host raised through ProcessForeground.</summary>
        internal string CompletionId;
        internal Action<string, bool> Completer;
        internal Action<string> Displayer;

        private int _finished;
        private int _displayed;
        private volatile bool _displayRequested;

        internal bool Finished => Volatile.Read(ref _finished) == 1;

        public void PreventDefault()
        {
            if (Finished) return;
            DefaultPrevented = true;
        }

        /// <summary>
        /// Presents this notification after a <see cref="PreventDefault"/> held it back — the "show it
        /// once my cutscene ends" case. Called while the handlers run, it simply cancels the prevent.
        /// Later, it asks the native SDK to present the held notification: on iOS within the native
        /// 25 second window; a no-op for events the host raised through ProcessForeground (no native
        /// decision to reopen) and on platforms without deferred display. At most once per event.
        /// </summary>
        public void Display()
        {
            if (!Finished) { _displayRequested = true; return; }
            if (!DefaultPrevented || CompletionId == null) return;
            if (Interlocked.Exchange(ref _displayed, 1) == 1) return;
            Displayer?.Invoke(CompletionId);
        }

        internal void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) == 1) return;
            var display = !DefaultPrevented || _displayRequested;
            if (_displayRequested) DefaultPrevented = false;
            if (CompletionId != null) Completer?.Invoke(CompletionId, display);
        }
    }
}
