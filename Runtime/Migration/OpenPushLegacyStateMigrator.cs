using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    internal sealed class OpenPushLegacyMigrationContext
    {
        public IOpenPushLegacyPrefs Prefs;
        public IOpenPushPlatform Platform;
        public string AppId;
        public bool HostOwnedAndroid;
        public bool ReloginWithoutAuthHash = true;
        public Action<string> IdentityVerificationRequired;
        /// <summary>The native answer to the migrated re-login (external id, result), so the host-facing user state can follow it.</summary>
        public Action<string, OpenPushIdentityResult> LoginDone;
        public Action<OpenPushLogLevel, string> Log;
        /// <summary>Taken before the native initialize; null means read the prefs now (Android, tests).</summary>
        public OpenPushLegacySnapshot Snapshot;
        /// <summary>
        /// True on iOS: PlayerPrefs is the native SDK's own store and the legacy key names are the
        /// native ones, so after the native initialize those keys belong to the native SDK.
        /// </summary>
        public bool NativeSharesPrefs;
    }

    /// <summary>The legacy C# layer's state as it stood before the native SDK started.</summary>
    internal sealed class OpenPushLegacySnapshot
    {
        public readonly string Token, SessionJson;
        public OpenPushLegacySnapshot(string token, string sessionJson) { Token = token; SessionJson = sessionJson; }
    }

    internal sealed class OpenPushMigrationResult
    {
        public bool Ran, SessionRead, OptedOut, Relogged, VerificationRequired, TokenRegistered;
        public int Tags, Aliases;
        public string ExternalId;
    }

    /// <summary>
    /// One-shot move of the state an earlier OpenPush SDK build's C# layer kept in PlayerPrefs into
    /// the native SDK after its first initialize. On iOS the state is captured before that
    /// initialize (see <see cref="Capture"/>), because the native SDK
    /// reads and rewrites the same NSUserDefaults keys the legacy C# layer used. The device
    /// credential needs no move: the host keeps passing its DeviceCredentialStore closure, so the
    /// server keeps the same subscription. In-app dismissals are not migrated.
    /// </summary>
    internal static class OpenPushLegacyStateMigrator
    {
        internal const string DoneKey = "openpush.migrated.1.1.5";
        internal const string TokenKey = "openpush.deviceToken";
        internal const string SessionKey = "openpush.deviceToken.session";
        internal const string EventsKey = "openpush.deviceToken.events";
        internal const string IamKeyPrefix = "openpush.iam.";
        /// <summary>Unity-owned home of the legacy session on iOS, off the key the native SDK owns.</summary>
        internal const string MovedSessionKey = "openpush.unity.legacy.session";

        /// <summary>
        /// Reads the legacy state before the native initialize; null once migration is done. With a
        /// shared store the Unity-format session moves to a Unity-owned key first, so the native SDK
        /// starts on a clean key and a failed migration can retry from the copy next launch. The
        /// token stays: it is the same APNs token the native SDK caches there. Never throws.
        /// </summary>
        internal static OpenPushLegacySnapshot Capture(IOpenPushLegacyPrefs prefs, bool nativeSharesPrefs,
                                                       Action<OpenPushLogLevel, string> log = null)
        {
            if (prefs == null) return null;
            try
            {
                if (prefs.GetInt(DoneKey, 0) == 1) return null;
                var token = prefs.GetString(TokenKey);
                if (!nativeSharesPrefs) return new OpenPushLegacySnapshot(token, prefs.GetString(SessionKey));
                var moved = prefs.GetString(MovedSessionKey);
                if (string.IsNullOrEmpty(moved))
                {
                    moved = prefs.GetString(SessionKey);
                    if (!string.IsNullOrEmpty(moved))
                    {
                        prefs.SetString(MovedSessionKey, moved);
                        prefs.DeleteKey(SessionKey);
                        prefs.Save();
                    }
                }
                return new OpenPushLegacySnapshot(token, moved);
            }
            catch (Exception e)
            {
                try { log?.Invoke(OpenPushLogLevel.Error, "✗ could not read the earlier OpenPush SDK state; migration will retry on the next launch: " + e.Message); } catch (Exception) { }
                return null;
            }
        }

        internal static OpenPushMigrationResult Run(OpenPushLegacyMigrationContext c)
        {
            var result = new OpenPushMigrationResult();
            var sink = c.Log;
            Action<OpenPushLogLevel, string> log = (level, message) =>
            {
                try { sink?.Invoke(level, message); } catch (Exception) { }
            };
            if (c?.Prefs == null || c.Platform == null) return result;
            try
            {
                if (c.Prefs.GetInt(DoneKey, 0) == 1) return result;
                var snapshot = c.Snapshot ?? Capture(c.Prefs, c.NativeSharesPrefs);
                if (snapshot == null) return result;
                var token = snapshot.Token;
                var sessionJson = snapshot.SessionJson;

                if (c.HostOwnedAndroid && !string.IsNullOrEmpty(token))
                {
                    c.Platform.Core.Register(token, null);
                    result.TokenRegistered = true;
                }

                if (!string.IsNullOrEmpty(sessionJson))
                {
                    if (OpenPushJson.TryParseStringMap(sessionJson, out var entries)) Replay(c, entries, result);
                    else log(OpenPushLogLevel.Warning, "⚠ OpenPush could not read the earlier OpenPush SDK session; tags, aliases and identity were not migrated.");
                }

                // On iOS the token and session keys are the native SDK's by now; only Unity-owned keys go.
                if (!c.NativeSharesPrefs)
                {
                    c.Prefs.DeleteKey(TokenKey);
                    c.Prefs.DeleteKey(SessionKey);
                }
                c.Prefs.DeleteKey(MovedSessionKey);
                c.Prefs.DeleteKey(EventsKey);
                if (!string.IsNullOrEmpty(c.AppId)) c.Prefs.DeleteKey(IamKeyPrefix + c.AppId);
                c.Prefs.SetInt(DoneKey, 1);
                c.Prefs.Save();
                result.Ran = true;
                log(OpenPushLogLevel.Info, "✓ migrated earlier OpenPush SDK state to the native SDK");
            }
            catch (Exception e)
            {
                result.Ran = false;
                log(OpenPushLogLevel.Error, "✗ earlier OpenPush SDK state migration failed; it will retry on the next launch: " + e.Message);
            }
            return result;
        }

        private static void Replay(OpenPushLegacyMigrationContext c, Dictionary<string, string> entries, OpenPushMigrationResult result)
        {
            result.SessionRead = true;
            var tags = new Dictionary<string, string>(StringComparer.Ordinal);
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            string externalId = null;
            var detached = false;
            foreach (var pair in entries)
            {
                if (pair.Key.StartsWith("t:", StringComparison.Ordinal)) { if (!string.IsNullOrEmpty(pair.Value)) tags[pair.Key.Substring(2)] = pair.Value; }
                else if (pair.Key.StartsWith("a:", StringComparison.Ordinal)) { if (!string.IsNullOrEmpty(pair.Value)) aliases[pair.Key.Substring(2)] = pair.Value; }
                else if (pair.Key == "x") externalId = string.IsNullOrEmpty(pair.Value) ? null : pair.Value;
                else if (pair.Key == "d") detached = pair.Value == "1";
                else if (pair.Key == "o") result.OptedOut |= pair.Value == "1";
                else if (pair.Key == "s") result.OptedOut |= pair.Value == OpenPushNotificationTypes.OptedOut.ToString();
            }
            if (result.OptedOut) c.Platform.PushSubscription.OptOut();
            if (tags.Count > 0) { c.Platform.User.AddTags(tags); result.Tags = tags.Count; }
            if (aliases.Count > 0) { c.Platform.User.AddAliases(aliases); result.Aliases = aliases.Count; }
            if (externalId == null || detached) return;
            result.ExternalId = externalId;
            if (c.ReloginWithoutAuthHash)
            {
                var done = c.LoginDone;
                c.Platform.Core.Login(externalId, null, identity =>
                {
                    try { done?.Invoke(externalId, identity); }
                    catch (Exception e) { try { c.Log?.Invoke(OpenPushLogLevel.Error, "✗ migrated login callback failed: " + e.Message); } catch (Exception) { } }
                });
                result.Relogged = true;
            }
            else
            {
                c.IdentityVerificationRequired?.Invoke(externalId);
                result.VerificationRequired = true;
            }
        }
    }
}
