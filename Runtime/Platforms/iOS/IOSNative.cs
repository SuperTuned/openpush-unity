using System;
using System.Runtime.InteropServices;

namespace OpenPushSDK.iOS
{
    internal delegate void OpenPushNativeEventCallback(IntPtr kind, IntPtr json);

    /// <summary>The Swift bridge's @_cdecl functions. Strings go in as UTF-8; returned strings are strdup'd natively and freed by IL2CPP.</summary>
    internal static class IOSNative
    {
        [DllImport("__Internal")] internal static extern void openpush_unity_set_event_callback(OpenPushNativeEventCallback callback);
        [DllImport("__Internal")] internal static extern string openpush_unity_sdk_version();
        [DllImport("__Internal")] internal static extern void openpush_unity_initialize(string appId, string sdkKey, string serverUrl, string appGroup,
                                                                                       string wrapperVersion, string credential, [MarshalAs(UnmanagedType.I1)] bool provisional);
        [DllImport("__Internal")] internal static extern void openpush_unity_set_credential(string credential);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool openpush_unity_is_initialized();
        [DllImport("__Internal")] internal static extern string openpush_unity_current_token();
        [DllImport("__Internal")] internal static extern void openpush_unity_register(string token, string externalId);
        [DllImport("__Internal")] internal static extern void openpush_unity_receipt(string type, string messageId, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_track_event(string name, string propertiesJson);
        [DllImport("__Internal")] internal static extern void openpush_unity_login(string externalId, string authHash, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_logout(long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_on_app_foregrounded();
        [DllImport("__Internal")] internal static extern void openpush_unity_remove_legacy_group_keys(string appGroup);
        [DllImport("__Internal")] internal static extern void openpush_unity_add_tags(string json);
        [DllImport("__Internal")] internal static extern void openpush_unity_remove_tags(string keysJson);
        [DllImport("__Internal")] internal static extern string openpush_unity_get_tags();
        [DllImport("__Internal")] internal static extern void openpush_unity_add_aliases(string json);
        [DllImport("__Internal")] internal static extern void openpush_unity_remove_aliases(string keysJson);
        [DllImport("__Internal")] internal static extern string openpush_unity_external_id();
        [DllImport("__Internal")] internal static extern void openpush_unity_add_sms(string number);
        [DllImport("__Internal")] internal static extern void openpush_unity_remove_sms(string number);
        [DllImport("__Internal")] internal static extern void openpush_unity_add_whatsapp(string number);
        [DllImport("__Internal")] internal static extern void openpush_unity_remove_whatsapp(string number);
        [DllImport("__Internal")] internal static extern void openpush_unity_add_email(string address);
        [DllImport("__Internal")] internal static extern void openpush_unity_remove_email(string address);
        [DllImport("__Internal")] internal static extern void openpush_unity_verify_start(string phone, string channel, string locale, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_verify_check(string verificationId, string code, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_verify_resend(string verificationId, string channel, long correlationId);
        [DllImport("__Internal")] internal static extern string openpush_unity_subscription();
        [DllImport("__Internal")] internal static extern void openpush_unity_opt_in();
        [DllImport("__Internal")] internal static extern void openpush_unity_opt_out();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool openpush_unity_permission();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool openpush_unity_can_request_permission();
        [DllImport("__Internal")] internal static extern void openpush_unity_request_permission([MarshalAs(UnmanagedType.I1)] bool fallbackToSettings, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_request_provisional_permission(long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_clear_all();
        [DllImport("__Internal")] internal static extern void openpush_unity_set_badge_count(int count);
        [DllImport("__Internal")] internal static extern void openpush_unity_process_foreground_data(string userInfoJson);
        [DllImport("__Internal")] internal static extern void openpush_unity_process_click_data(string userInfoJson);
        [DllImport("__Internal")] internal static extern void openpush_unity_process_displayed_data(string userInfoJson);
        [DllImport("__Internal")] internal static extern void openpush_unity_complete_foreground(string completionId, [MarshalAs(UnmanagedType.I1)] bool display);
        [DllImport("__Internal")] internal static extern void openpush_unity_display_foreground(string completionId);
        [DllImport("__Internal")] internal static extern void openpush_unity_iam_set_paused([MarshalAs(UnmanagedType.I1)] bool paused);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool openpush_unity_iam_paused();
        [DllImport("__Internal")] internal static extern void openpush_unity_iam_add_triggers(string json);
        [DllImport("__Internal")] internal static extern void openpush_unity_iam_remove_triggers(string keysJson);
        [DllImport("__Internal")] internal static extern void openpush_unity_iam_clear_triggers();
        [DllImport("__Internal")] internal static extern void openpush_unity_iam_fetch();
        [DllImport("__Internal")] internal static extern void openpush_unity_la_setup_default([MarshalAs(UnmanagedType.I1)] bool pushToStart, [MarshalAs(UnmanagedType.I1)] bool pushToUpdate);
        [DllImport("__Internal")] internal static extern void openpush_unity_la_start_default(string activityId, string attributesJson, string contentJson);
        [DllImport("__Internal")] internal static extern void openpush_unity_la_enter(string activityId, string token, string activityType, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_la_exit(string activityId, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_la_set_push_to_start_token(string activityType, string token, long correlationId);
        [DllImport("__Internal")] internal static extern void openpush_unity_la_remove_push_to_start_token(string activityType, long correlationId);
        // The kept OpenPushNotifications.mm export (not part of the Swift bridge).
        [DllImport("__Internal")] internal static extern void _openpush_remove_notification(string messageId);
    }
}
