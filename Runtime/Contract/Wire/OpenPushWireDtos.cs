using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    // One DTO per native payload. Field names are the JSON keys both bridges emit (spec §4).
    // ToJson exists for round-trip tests and for the stub platform.

    internal sealed class ActionWire
    {
        public string id, label, icon, url; public bool system_icon;
        public static ActionWire From(Dictionary<string, object> o) => o == null ? null : new ActionWire
        {
            id = OpenPushWire.Str(o, "id"), label = OpenPushWire.Str(o, "label"), icon = OpenPushWire.Str(o, "icon"),
            system_icon = OpenPushWire.Bool(o, "system_icon"), url = OpenPushWire.Str(o, "url"),
        };
        public Dictionary<string, object> ToMap() => new Dictionary<string, object>
        { ["id"] = id, ["label"] = label, ["icon"] = icon, ["system_icon"] = system_icon, ["url"] = url };
    }

    internal sealed class NotificationWire
    {
        public string message_id, title, body, image_url, launch_url, collapse_id, priority, action_category, app_id;
        public int? ttl_seconds;
        public bool system_message;
        public List<ActionWire> actions = new List<ActionWire>();
        /// <summary>The Android renderer overrides, keyed without the op_android_ prefix.</summary>
        public IReadOnlyDictionary<string, string> android;
        public IReadOnlyDictionary<string, string> data;

        public static NotificationWire From(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var wire = new NotificationWire
            {
                message_id = OpenPushWire.Str(o, "message_id"), title = OpenPushWire.Str(o, "title"),
                body = OpenPushWire.Str(o, "body"), image_url = OpenPushWire.Str(o, "image_url"),
                launch_url = OpenPushWire.Str(o, "launch_url"), collapse_id = OpenPushWire.Str(o, "collapse_id"),
                priority = OpenPushWire.Str(o, "priority"), action_category = OpenPushWire.Str(o, "action_category"),
                app_id = OpenPushWire.Str(o, "app_id"), ttl_seconds = OpenPushWire.Int(o, "ttl_seconds"),
                system_message = OpenPushWire.Bool(o, "system_message"),
                android = OpenPushWire.StringMap(o, "android"),
                data = OpenPushWire.StringMap(o, "data"),
            };
            var list = OpenPushWire.Arr(o, "actions");
            if (list != null)
                foreach (var item in list)
                {
                    var action = ActionWire.From(item as Dictionary<string, object>);
                    if (action != null && !string.IsNullOrEmpty(action.id)) wire.actions.Add(action);
                }
            return wire;
        }

        public Dictionary<string, object> ToMap()
        {
            var actionMaps = new List<object>();
            foreach (var a in actions) actionMaps.Add(a.ToMap());
            var dataMap = new Dictionary<string, object>();
            if (data != null) foreach (var p in data) dataMap[p.Key] = p.Value;
            return new Dictionary<string, object>
            {
                ["message_id"] = message_id, ["title"] = title, ["body"] = body, ["image_url"] = image_url,
                ["launch_url"] = launch_url, ["collapse_id"] = collapse_id, ["priority"] = priority,
                ["action_category"] = action_category, ["app_id"] = app_id, ["ttl_seconds"] = ttl_seconds,
                ["system_message"] = system_message, ["actions"] = actionMaps,
                ["android"] = ClickEventWire.ToObjectMap(android), ["data"] = dataMap,
            };
        }

        public string ToJson() => OpenPushWire.ToJson(ToMap());
    }

    internal sealed class ClickEventWire
    {
        public string message_id, action_id, url, action_url;
        public bool launch_url_opened;
        public NotificationWire notification;
        public IReadOnlyDictionary<string, string> data;

        public static ClickEventWire From(Dictionary<string, object> o) => o == null ? null : new ClickEventWire
        {
            message_id = OpenPushWire.Str(o, "message_id"), action_id = OpenPushWire.Str(o, "action_id"),
            url = OpenPushWire.Str(o, "url"), action_url = OpenPushWire.Str(o, "action_url"),
            launch_url_opened = OpenPushWire.Bool(o, "launch_url_opened"),
            notification = NotificationWire.From(OpenPushWire.Obj(o, "notification")),
            data = OpenPushWire.StringMap(o, "data"),
        };

        public string ToJson() => OpenPushWire.ToJson(new Dictionary<string, object>
        {
            ["message_id"] = message_id, ["action_id"] = action_id, ["url"] = url, ["action_url"] = action_url,
            ["launch_url_opened"] = launch_url_opened,
            ["notification"] = notification?.ToMap(), ["data"] = ToObjectMap(data),
        });

        public OpenPushClickEvent ToPublic()
        {
            var payload = data != null && data.Count > 0 ? data : notification?.data ?? data;
            var parsed = OpenPushNotification.From(payload);
            WireNotificationFill.Fill(parsed, notification);
            return new OpenPushClickEvent
            {
                MessageId = message_id ?? parsed.MessageId,
                Data = parsed.Data,
                Notification = parsed,
                ActionId = action_id,
                Url = url ?? parsed.LaunchUrl,
                // The bridge reports the tapped button's link; a native build without the key (or a
                // host-forwarded tap) leaves it to the parsed actions, matched by the action id.
                ActionUrl = action_url ?? ButtonUrl(parsed, action_id),
                LaunchUrlOpened = launch_url_opened,
            };
        }

        private static string ButtonUrl(OpenPushNotification notification, string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return null;
            foreach (var action in notification.Actions)
                if (action.Id == actionId) return action.Url;
            return null;
        }

        internal static Dictionary<string, object> ToObjectMap(IReadOnlyDictionary<string, string> map)
        {
            var result = new Dictionary<string, object>();
            if (map != null) foreach (var p in map) result[p.Key] = p.Value;
            return result;
        }
    }

    /// <summary>
    /// iOS alert text lives under aps, not in the flat data map OpenPushNotification.From reads,
    /// so fields the data map lacks are taken from the bridge's notification object (spec §4:
    /// every OpenPushNotification field). The data map wins wherever it carries a value.
    /// </summary>
    internal static class WireNotificationFill
    {
        internal static void Fill(OpenPushNotification target, NotificationWire source)
        {
            if (target == null || source == null) return;
            target.MessageId ??= source.message_id;
            target.Title ??= source.title;
            target.Body ??= source.body;
            target.ImageUrl ??= source.image_url;
            target.LaunchUrl ??= source.launch_url;
            target.CollapseId ??= source.collapse_id;
            target.Priority ??= source.priority;
            target.ActionCategory ??= source.action_category;
            target.AppId ??= source.app_id;
            target.TtlSeconds ??= source.ttl_seconds;
            target.SystemMessage |= source.system_message;
            if (target.Actions.Count == 0 && source.actions.Count > 0)
            {
                var actions = new List<OpenPushAction>(source.actions.Count);
                foreach (var a in source.actions)
                    if (!string.IsNullOrEmpty(a.id) && !string.IsNullOrEmpty(a.label))
                        actions.Add(new OpenPushAction
                        {
                            Id = a.id, Label = a.label, Icon = string.IsNullOrEmpty(a.icon) ? null : a.icon,
                            SystemIcon = a.system_icon, Url = string.IsNullOrEmpty(a.url) ? null : a.url,
                        });
                if (actions.Count > 0) target.Actions = actions;
            }
            if (target.AndroidOverrides.Count == 0 && source.android != null && source.android.Count > 0)
                target.AndroidOverrides = source.android;
        }
    }

    internal sealed class BackgroundMessageWire
    {
        public string message_id;
        public NotificationWire notification;
        public IReadOnlyDictionary<string, string> data;

        public static BackgroundMessageWire From(Dictionary<string, object> o) => o == null ? null : new BackgroundMessageWire
        {
            message_id = OpenPushWire.Str(o, "message_id"),
            notification = NotificationWire.From(OpenPushWire.Obj(o, "notification")), data = OpenPushWire.StringMap(o, "data"),
        };

        public string ToJson() => OpenPushWire.ToJson(new Dictionary<string, object>
        {
            ["message_id"] = message_id, ["notification"] = notification?.ToMap(), ["data"] = ClickEventWire.ToObjectMap(data),
        });

        public OpenPushBackgroundMessage ToPublic()
        {
            var payload = data != null && data.Count > 0 ? data : notification?.data ?? data;
            var parsed = OpenPushNotification.From(payload);
            WireNotificationFill.Fill(parsed, notification);
            return new OpenPushBackgroundMessage { MessageId = message_id ?? parsed.MessageId, Data = parsed.Data, Notification = parsed };
        }
    }

    internal sealed class ForegroundEventWire
    {
        public string message_id, completion_id;
        public NotificationWire notification;
        public IReadOnlyDictionary<string, string> data;

        public static ForegroundEventWire From(Dictionary<string, object> o) => o == null ? null : new ForegroundEventWire
        {
            message_id = OpenPushWire.Str(o, "message_id"), completion_id = OpenPushWire.Str(o, "completion_id"),
            notification = NotificationWire.From(OpenPushWire.Obj(o, "notification")), data = OpenPushWire.StringMap(o, "data"),
        };

        public string ToJson() => OpenPushWire.ToJson(new Dictionary<string, object>
        {
            ["message_id"] = message_id, ["completion_id"] = completion_id,
            ["notification"] = notification?.ToMap(), ["data"] = ClickEventWire.ToObjectMap(data),
        });

        public OpenPushForegroundEvent ToPublic(Action<string, bool> completer, Action<string> displayer)
        {
            var payload = data != null && data.Count > 0 ? data : notification?.data ?? data;
            var parsed = OpenPushNotification.From(payload);
            WireNotificationFill.Fill(parsed, notification);
            return new OpenPushForegroundEvent
            {
                MessageId = message_id ?? parsed.MessageId, Data = parsed.Data, Notification = parsed,
                CompletionId = string.IsNullOrEmpty(completion_id) ? null : completion_id,
                Completer = completer, Displayer = displayer,
            };
        }
    }

    internal sealed class PushSubscriptionWire
    {
        public string id, token; public bool opted_in; public int? status;

        public static PushSubscriptionWire From(Dictionary<string, object> o) => o == null ? null : new PushSubscriptionWire
        { id = OpenPushWire.Str(o, "id"), token = OpenPushWire.Str(o, "token"), opted_in = OpenPushWire.Bool(o, "opted_in"), status = OpenPushWire.Int(o, "status") };

        public Dictionary<string, object> ToMap() => new Dictionary<string, object>
        { ["id"] = id, ["token"] = token, ["opted_in"] = opted_in, ["status"] = status };

        public string ToJson() => OpenPushWire.ToJson(ToMap());

        public PushSubscriptionState ToPublic() => new PushSubscriptionState { Id = id, Token = token, OptedIn = opted_in, Status = status };
    }

    internal sealed class PushSubscriptionChangeWire
    {
        public PushSubscriptionWire previous, current;

        public static PushSubscriptionChangeWire From(Dictionary<string, object> o) => o == null ? null : new PushSubscriptionChangeWire
        { previous = PushSubscriptionWire.From(OpenPushWire.Obj(o, "previous")), current = PushSubscriptionWire.From(OpenPushWire.Obj(o, "current")) };

        public string ToJson() => OpenPushWire.ToJson(new Dictionary<string, object>
        { ["previous"] = previous?.ToMap(), ["current"] = current?.ToMap() });
    }

    internal sealed class UserStateWire
    {
        public string external_id, subscription_id;
        public static UserStateWire From(Dictionary<string, object> o) => o == null ? null : new UserStateWire
        { external_id = OpenPushWire.Str(o, "external_id"), subscription_id = OpenPushWire.Str(o, "subscription_id") };
        public OpenPushUserState ToPublic() => new OpenPushUserState { ExternalId = external_id, SubscriptionId = subscription_id };
    }

    internal sealed class IdentityResultWire
    {
        public bool success; public string identity_rejected_note, error;

        public static IdentityResultWire From(Dictionary<string, object> o) => o == null ? null : new IdentityResultWire
        { success = OpenPushWire.Bool(o, "success"), identity_rejected_note = OpenPushWire.Str(o, "identity_rejected_note"), error = OpenPushWire.Str(o, "error") };

        public string ToJson() => OpenPushWire.ToJson(new Dictionary<string, object>
        { ["success"] = success, ["identity_rejected_note"] = identity_rejected_note, ["error"] = error });

        public OpenPushIdentityResult ToPublic()
            => success ? OpenPushIdentityResult.Ok()
             : identity_rejected_note != null ? OpenPushIdentityResult.Rejected(identity_rejected_note)
             : OpenPushIdentityResult.Failed(error);
    }

    internal sealed class InAppClickWire
    {
        /// <summary>kind is optional: a bridge that knows the composer's kind sends it, otherwise it is derived.</summary>
        public string message_id, action_id, url, url_target, outcome, prompt, kind;
        public bool close, first_click;
        public IReadOnlyDictionary<string, string> tags;

        public static InAppClickWire From(Dictionary<string, object> o) => o == null ? null : new InAppClickWire
        {
            message_id = OpenPushWire.Str(o, "message_id"), action_id = OpenPushWire.Str(o, "action_id"),
            url = OpenPushWire.Str(o, "url"), url_target = OpenPushWire.Str(o, "url_target"),
            outcome = OpenPushWire.Str(o, "outcome"), prompt = OpenPushWire.Str(o, "prompt"),
            kind = OpenPushWire.Str(o, "kind"),
            close = OpenPushWire.Bool(o, "close"), first_click = OpenPushWire.Bool(o, "first_click"),
            tags = OpenPushWire.StringMap(o, "tags"),
        };

        public string ToJson() => OpenPushWire.ToJson(new Dictionary<string, object>
        {
            ["message_id"] = message_id, ["action_id"] = action_id, ["url"] = url, ["url_target"] = url_target,
            ["outcome"] = outcome, ["prompt"] = prompt, ["kind"] = kind, ["close"] = close, ["first_click"] = first_click,
            ["tags"] = ClickEventWire.ToObjectMap(tags),
        });

        /// <summary>
        /// Precedence: a url is a link first, a prompt outranks a tag or an
        /// outcome, a named action is custom, and close comes last because most actions close.
        /// </summary>
        internal string ResolvedKind()
        {
            if (!string.IsNullOrEmpty(kind)) return kind;
            if (!string.IsNullOrEmpty(url)) return "url";
            if (prompt == "push") return "prompt_push";
            if (prompt == "location") return "prompt_location";
            if (tags != null && tags.Count > 0) return "tag";
            if (!string.IsNullOrEmpty(outcome)) return "outcome";
            if (!string.IsNullOrEmpty(action_id)) return "custom";
            return close ? "close" : "click";
        }

        public OpenPushInAppClick ToPublic()
        {
            var resolved = ResolvedKind();
            return new OpenPushInAppClick
            {
                Kind = resolved,
                // ActionId is the custom action's own name and nothing else (OpenPushInAppClick.Kind doc).
                ActionId = resolved == "custom" ? action_id : null,
                Url = url, UrlTarget = url_target, Outcome = outcome,
                Prompt = prompt ?? (resolved == "prompt_push" ? "push" : resolved == "prompt_location" ? "location" : null),
                Tags = tags != null && tags.Count > 0 ? tags : null,
                Closes = close,
            };
        }
    }

    internal sealed class InAppLifecycleWire
    {
        public string message_id, phase;
        public static InAppLifecycleWire From(Dictionary<string, object> o) => o == null ? null : new InAppLifecycleWire
        { message_id = OpenPushWire.Str(o, "message_id"), phase = OpenPushWire.Str(o, "phase") };
    }

    internal sealed class ResultWire
    {
        public long correlation_id; public Dictionary<string, object> payload;
        public static ResultWire From(Dictionary<string, object> o) => o == null ? null : new ResultWire
        { correlation_id = OpenPushWire.Long(o, "correlation_id"), payload = OpenPushWire.Obj(o, "payload") ?? new Dictionary<string, object>() };
    }
}
