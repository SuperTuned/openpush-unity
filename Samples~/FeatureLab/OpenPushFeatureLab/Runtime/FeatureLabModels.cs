using System;
using System.Collections.Generic;

namespace OpenPushFeatureLab
{
    internal enum LabRuntimeMode { Demo, Live }

    internal static class FeatureLabRuntimeDefaults
    {
        internal static LabRuntimeMode Mode(string appId, string sdkKey)
            => !string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(sdkKey)
                ? LabRuntimeMode.Live
                : LabRuntimeMode.Demo;
    }

    internal enum LabAppearance { System, Light, Dark }
    internal enum LabTab { Status, User, Events, Activity }
    internal enum LabEventKind { Info, Success, Warning, Error }

    internal sealed class LabEvent
    {
        public LabEvent(string title, string detail, LabEventKind kind = LabEventKind.Info)
        {
            Title = title;
            Detail = detail;
            Kind = kind;
            OccurredAt = DateTime.Now;
        }

        public string Title { get; }
        public string Detail { get; }
        public LabEventKind Kind { get; }
        public DateTime OccurredAt { get; }
    }

    internal sealed class LabSnapshot
    {
        public bool IsInitialized;
        public bool Permission;
        public bool PermissionKnown;
        public int NotificationTypes;
        public bool OptedIn;
        public string SubscriptionId;
        public string Token;
        public string ExternalId;
        public string SdkVersion = "—";
        public string WrapperVersion = "—";
        public string AndroidLiveUpdateCapability = "Unavailable in Editor";
        public readonly Dictionary<string, string> Tags = new Dictionary<string, string>();
        public readonly Dictionary<string, string> Aliases = new Dictionary<string, string>();

    }

    internal sealed class NotificationDraft
    {
        public string Kind = "Custom";
        public string Title = "Feature unlocked";
        public string Description = "OpenPush can carry app-defined data with every notification.";
        public string DataKey = "feature";
        public string DataValue = "inbox";
        // The server supplies UTM tags; receivers must preserve the full URL.
        public string LaunchUrl = "https://openpush.ai/?utm_source=openpush&utm_medium=push&utm_campaign=Feature+Lab#overview";
    }
}
