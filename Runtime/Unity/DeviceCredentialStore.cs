using System.Collections.Generic;

namespace OpenPushSDK
{
    /// <summary>
    /// Secure storage for the linked installation's device credential.
    /// Android: AES-GCM via Android Keystore. iOS: Keychain. Editor: an
    /// in-memory, session-only store for play-mode testing (never persisted).
    /// All other platforms fail closed: Save returns false and Load returns
    /// null. The credential is never written to disk as plaintext.
    /// </summary>
    public sealed class DeviceCredentialStore
    {
        private readonly ICredentialBackend _backend;

        public DeviceCredentialStore(string service, string iosAccessGroup = null)
        {
#if UNITY_EDITOR
            _backend = new EditorCredentialBackend(service);
#elif UNITY_ANDROID
            _backend = new AndroidCredentialBackend(service);
#elif UNITY_IOS
            _backend = new IosCredentialBackend(service, iosAccessGroup);
#else
            _backend = new FailClosedBackend();
#endif
        }

        /// <summary>
        /// Returns false if secure storage is unavailable; never falls back to
        /// plaintext.
        /// </summary>
        public bool Save(string value) => value != null && _backend.Save(value);

        // Backends disagree about how a stored-but-empty value comes back, so
        // normalize here and let every platform report "nothing stored" the
        // same way.
        public string Load()
        {
            var value = _backend.Load();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public void Wipe() => _backend.Wipe();

        private interface ICredentialBackend
        {
            bool Save(string value);
            string Load();
            void Wipe();
        }

        private sealed class FailClosedBackend : ICredentialBackend
        {
            public bool Save(string value) => false;
            public string Load() => null;
            public void Wipe() { }
        }

#if UNITY_EDITOR
        /// Volatile per-session store so play-mode flows are testable without
        /// pretending the Editor has a hardware keystore.
        private sealed class EditorCredentialBackend : ICredentialBackend
        {
            private static readonly Dictionary<string, string> Session =
                new Dictionary<string, string>();
            private readonly string _service;

            public EditorCredentialBackend(string service) { _service = service; }
            public bool Save(string value) { Session[_service] = value; return true; }
            public string Load()
                => Session.TryGetValue(_service, out var value) ? value : null;
            public void Wipe() => Session.Remove(_service);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class AndroidCredentialBackend : ICredentialBackend
        {
            private const string StoreClass = "ai.openpush.unity.OpenPushCredentialStore";
            private readonly string _service;

            public AndroidCredentialBackend(string service) { _service = service; }

            private static UnityEngine.AndroidJavaObject AppContext()
            {
                using (var player =
                       new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity =
                       player.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity"))
                {
                    return activity.Call<UnityEngine.AndroidJavaObject>("getApplicationContext");
                }
            }

            public bool Save(string value)
            {
                try
                {
                    using (var store = new UnityEngine.AndroidJavaClass(StoreClass))
                    using (var context = AppContext())
                    {
                        return store.CallStatic<bool>("save", context, _service, value);
                    }
                }
                catch (System.Exception) { return false; }
            }

            public string Load()
            {
                try
                {
                    using (var store = new UnityEngine.AndroidJavaClass(StoreClass))
                    using (var context = AppContext())
                    {
                        return store.CallStatic<string>("load", context, _service);
                    }
                }
                catch (System.Exception) { return null; }
            }

            public void Wipe()
            {
                try
                {
                    using (var store = new UnityEngine.AndroidJavaClass(StoreClass))
                    using (var context = AppContext())
                    {
                        store.CallStatic("wipe", context, _service);
                    }
                }
                catch (System.Exception) { }
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private sealed class IosCredentialBackend : ICredentialBackend
        {
            private readonly string _service;
            private readonly string _accessGroup;

            public IosCredentialBackend(string service, string accessGroup)
            {
                _service = service;
                _accessGroup = accessGroup ?? "";
            }

            /// The native function returns a C _Bool, which is one byte; the
            /// default marshalling for a returned bool is a four-byte value,
            /// and the upper bytes are not guaranteed to be zeroed. Reading
            /// them could turn a false into a true, reporting a save that did
            /// not happen -- a fail-open, so the width is stated explicitly.
            [System.Runtime.InteropServices.DllImport("__Internal")]
            [return: System.Runtime.InteropServices.MarshalAs(
                System.Runtime.InteropServices.UnmanagedType.I1)]
            private static extern bool _openpush_credential_save(
                string service, string accessGroup, string value);

            [System.Runtime.InteropServices.DllImport("__Internal")]
            private static extern string _openpush_credential_load(
                string service, string accessGroup);

            [System.Runtime.InteropServices.DllImport("__Internal")]
            private static extern void _openpush_credential_wipe(
                string service, string accessGroup);

            public bool Save(string value)
                => _openpush_credential_save(_service, _accessGroup, value);

            public string Load()
                => _openpush_credential_load(_service, _accessGroup);

            public void Wipe() => _openpush_credential_wipe(_service, _accessGroup);
        }
#endif
    }
}
