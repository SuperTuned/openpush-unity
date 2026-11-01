using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// Turns the one (kind, json) stream both native bridges emit into typed events. Runs on the
    /// native callback thread; never throws back into native code.
    /// </summary>
    internal sealed class OpenPushNativeEventRouter
    {
        private readonly Action<string, bool> _completeForeground;
        private readonly Action<string> _displayForeground;
        private volatile PushSubscriptionState _last;

        internal OpenPushNativeEventRouter(Action<string, bool> completeForeground, Action<string> displayForeground)
        {
            _completeForeground = completeForeground ?? ((_, __) => { });
            _displayForeground = displayForeground ?? (_ => { });
        }

        public event Action<OpenPushLogLevel, string> Log;
        public event Action Registered;
        public event Action<string> RegistrationFailed;
        public event Action<string> IdentityVerificationRequired;
        public event Action<PushSubscriptionState, PushSubscriptionState> SubscriptionChanged;
        public event Action<bool> PermissionChanged;
        public event Action<OpenPushClickEvent> Clicked;
        public event Action<OpenPushForegroundEvent> ForegroundWillDisplay;
        public event Action<OpenPushBackgroundMessage> BackgroundMessage;
        public event Action<string, string> InAppLifecycle;
        public event Action<string, OpenPushInAppClick> InAppClicked;
        public event Action<long, Dictionary<string, object>> Result;

        public PushSubscriptionState LastSubscription => _last;

        public void RaiseLog(OpenPushLogLevel level, string message) => Safe(() => Log?.Invoke(level, message));

        public void Route(string kind, string json)
        {
            try { Dispatch(kind, OpenPushWire.Parse(json) ?? new Dictionary<string, object>()); }
            catch (Exception e) { RaiseLog(OpenPushLogLevel.Error, "✗ OpenPush could not handle native event '" + kind + "': " + e.Message); }
        }

        private void Dispatch(string kind, Dictionary<string, object> o)
        {
            switch (kind)
            {
                case "log":
                    var message = OpenPushWire.Str(o, "message") ?? "";
                    RaiseLog(OpenPushLogClassifier.Classify(message), message);
                    return;
                case "registered": Safe(() => Registered?.Invoke()); return;
                case "registration_failed": Safe(() => RegistrationFailed?.Invoke(OpenPushWire.Str(o, "message"))); return;
                case "identity_verification_required":
                    Safe(() => IdentityVerificationRequired?.Invoke(OpenPushWire.Str(o, "external_id"))); return;
                case "push_subscription":
                    var change = PushSubscriptionChangeWire.From(o);
                    var current = change?.current?.ToPublic() ?? new PushSubscriptionState();
                    var previous = change?.previous?.ToPublic();
                    _last = current;
                    Safe(() => SubscriptionChanged?.Invoke(previous, current));
                    return;
                case "permission": Safe(() => PermissionChanged?.Invoke(OpenPushWire.Bool(o, "granted"))); return;
                case "click":
                    var click = ClickEventWire.From(o)?.ToPublic();
                    if (click != null) Safe(() => Clicked?.Invoke(click));
                    return;
                case "foreground":
                    var foreground = ForegroundEventWire.From(o)?.ToPublic(_completeForeground, _displayForeground);
                    if (foreground == null) return;
                    if (ForegroundWillDisplay == null) { foreground.Finish(); return; }
                    Safe(() => ForegroundWillDisplay.Invoke(foreground));
                    return;
                case "background":
                    var background = BackgroundMessageWire.From(o)?.ToPublic();
                    if (background != null) Safe(() => BackgroundMessage?.Invoke(background));
                    return;
                case "iam_lifecycle":
                    var lifecycle = InAppLifecycleWire.From(o);
                    Safe(() => InAppLifecycle?.Invoke(lifecycle.message_id, lifecycle.phase));
                    return;
                case "iam_click":
                    var iam = InAppClickWire.From(o);
                    Safe(() => InAppClicked?.Invoke(iam.message_id, iam.ToPublic()));
                    return;
                case "result":
                    var result = ResultWire.From(o);
                    Safe(() => Result?.Invoke(result.correlation_id, result.payload));
                    return;
                default:
                    RaiseLog(OpenPushLogLevel.Verbose, "• unknown native event '" + kind + "' ignored");
                    return;
            }
        }

        private static void Safe(Action action)
        {
            try { action(); } catch (Exception) { /* a host handler must never unwind into native code */ }
        }
    }
}
