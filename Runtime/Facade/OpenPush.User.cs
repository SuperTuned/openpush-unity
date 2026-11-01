using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    public static partial class OpenPush
    {
        private static readonly PushSubscriptionAccessor Accessor = new PushSubscriptionAccessor();

        /// <summary>Device-level user properties, owned by the native SDK.</summary>
        public static class User
        {
            public static void AddTag(string key, string value)
            {
                RequireInitialized("User.AddTag");
                if (string.IsNullOrEmpty(key)) { Log(OpenPushLogLevel.Warning, "⚠ AddTag ignored — empty key"); return; }
                AddTags(new Dictionary<string, string> { [key] = value ?? "" });
            }

            public static void AddTags(IReadOnlyDictionary<string, string> tags)
            {
                RequireInitialized("User.AddTags");
                if (tags == null || tags.Count == 0) return;
                Platform.User.AddTags(tags);
            }

            public static void RemoveTag(string key) => RemoveTags(key);

            public static Dictionary<string, string> GetTags() => Platform.User.GetTags() ?? new Dictionary<string, string>();

            public static void RemoveTags(params string[] keys)
            {
                RequireInitialized("User.RemoveTags");
                if (keys == null || keys.Length == 0) return;
                Platform.User.RemoveTags(keys);
            }

            public static PushSubscriptionAccessor PushSubscription => Accessor;

            public static void AddAlias(string label, string id)
            {
                RequireInitialized("User.AddAlias");
                if (string.IsNullOrEmpty(label)) { Log(OpenPushLogLevel.Warning, "⚠ AddAlias ignored — empty label"); return; }
                AddAliases(new Dictionary<string, string> { [label] = id ?? "" });
            }

            public static void AddAliases(IReadOnlyDictionary<string, string> aliases)
            {
                RequireInitialized("User.AddAliases");
                if (aliases == null || aliases.Count == 0) return;
                Platform.User.AddAliases(aliases);
            }

            public static void RemoveAlias(string label) => RemoveAliases(label);

            public static void RemoveAliases(params string[] labels)
            {
                RequireInitialized("User.RemoveAliases");
                if (labels == null || labels.Length == 0) return;
                Platform.User.RemoveAliases(labels);
            }

            public static string ExternalId => Platform.User.ExternalId;

            /// <summary>
            /// Adds <paramref name="number"/> as this user's SMS number. It is normalised as the server
            /// does it (every character but digits and <c>+</c> removed, a leading <c>00</c> read as
            /// <c>+</c>) and must then be E.164, <c>+</c> and 8 to 15 digits, the first not <c>0</c>:
            /// anything else returns false, logs why and sends nothing. A new number is added <b>not verified</b> and receives nothing until
            /// a code sent to it with <see cref="Verify"/> is approved. The native SDK persists the write
            /// and retries it like a tag, for the user logged in at the time of the call.
            /// </summary>
            public static bool AddSms(string number)
                => WritePhone("User.AddSms", number, normalized => Platform.User.AddSms(normalized));

            /// <summary>Opts <paramref name="number"/> out of SMS, on the same terms as <see cref="AddSms"/>.</summary>
            public static bool RemoveSms(string number)
                => WritePhone("User.RemoveSms", number, normalized => Platform.User.RemoveSms(normalized));

            /// <summary>Adds <paramref name="number"/> as this user's WhatsApp number, on the same terms as <see cref="AddSms"/>.</summary>
            public static bool AddWhatsApp(string number)
                => WritePhone("User.AddWhatsApp", number, normalized => Platform.User.AddWhatsApp(normalized));

            /// <summary>Opts <paramref name="number"/> out of WhatsApp, on the same terms as <see cref="AddSms"/>.</summary>
            public static bool RemoveWhatsApp(string number)
                => WritePhone("User.RemoveWhatsApp", number, normalized => Platform.User.RemoveWhatsApp(normalized));

            /// <summary>The one path the four phone writes take: initialized, then E.164, then the platform.</summary>
            private static bool WritePhone(string member, string number, Action<string> write)
            {
                RequireInitialized(member);
                var normalized = OpenPushPhoneNumber.Normalize(number);
                if (normalized == null)
                {
                    Log(OpenPushLogLevel.Warning, "⚠ " + member + " ignored — " + OpenPushPhoneNumber.InvalidMessage);
                    return false;
                }
                write(normalized);
                return true;
            }

            /// <summary>
            /// Adds <paramref name="address"/> as this user's email address. It is trimmed and lower-cased
            /// and must then be at most 254 characters with one <c>@</c>, something before it and a dot in
            /// the domain: anything else returns false, logs why and sends nothing. The address is
            /// subscribed at once, unless this app's suppression list has it (it unsubscribed, bounced or
            /// complained): then it is stored but never emailed. The native SDK persists the write and
            /// retries it like a tag, for the user logged in at the time of the call.
            /// </summary>
            public static bool AddEmail(string address)
                => WriteEmail("User.AddEmail", address, normalized => Platform.User.AddEmail(normalized));

            /// <summary>
            /// Removes the subscription for <paramref name="address"/>, on the same terms as
            /// <see cref="AddEmail"/>. It is not an unsubscribe: no suppression is written, and a later
            /// <see cref="AddEmail"/> subscribes the address again.
            /// </summary>
            public static bool RemoveEmail(string address)
                => WriteEmail("User.RemoveEmail", address, normalized => Platform.User.RemoveEmail(normalized));

            /// <summary>The one path the two email writes take: initialized, then the address check, then the platform.</summary>
            private static bool WriteEmail(string member, string address, Action<string> write)
            {
                RequireInitialized(member);
                var normalized = OpenPushEmailAddress.Normalize(address);
                if (normalized == null)
                {
                    Log(OpenPushLogLevel.Warning, "⚠ " + member + " ignored — " + OpenPushEmailAddress.InvalidMessage);
                    return false;
                }
                write(normalized);
                return true;
            }

            /// <summary>
            /// The account this device belongs to changed: a Login, a Logout, or the registration that
            /// assigns the subscription id. Never fires while both halves are unknown.
            /// </summary>
            public static event Action<OpenPushUserState> Changed
            {
                add => Hub.UserChanged.Subscribe(value);
                remove => Hub.UserChanged.Unsubscribe(value);
            }
        }

        /// <summary>This device's subscription as a control. OptOut stops sends without deleting it.</summary>
        public static class PushSubscription
        {
            public static void OptIn() { RequireInitialized("PushSubscription.OptIn"); Platform.PushSubscription.OptIn(); }
            public static void OptOut() { RequireInitialized("PushSubscription.OptOut"); Platform.PushSubscription.OptOut(); }

            /// <summary>OS permission, a provider token, and not opted out.</summary>
            public static bool OptedIn => Platform.PushSubscription.OptedIn;

            public static event Action<PushSubscriptionState> Changed
            {
                add => Hub.SubscriptionCurrent.Subscribe(value);
                remove => Hub.SubscriptionCurrent.Unsubscribe(value);
            }

            /// <summary>The same moments as <see cref="Changed"/>, with the state that was replaced (null on the first).</summary>
            public static event Action<PushSubscriptionState, PushSubscriptionState> ChangedWithPrevious
            {
                add
                {
                    if (value == null) return;
                    var wrapper = SubscriptionHandlers.Add(value, change => value(change.Previous, change.Current));
                    if (wrapper != null) Hub.SubscriptionChanged.Subscribe(wrapper);
                }
                remove
                {
                    var wrapper = SubscriptionHandlers.Remove(value);
                    if (wrapper != null) Hub.SubscriptionChanged.Unsubscribe(wrapper);
                }
            }
        }
    }
}
