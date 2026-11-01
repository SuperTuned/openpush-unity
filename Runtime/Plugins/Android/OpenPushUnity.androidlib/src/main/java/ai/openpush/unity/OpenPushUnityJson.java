package ai.openpush.unity;

import ai.openpush.android.BackgroundMessageEvent;
import ai.openpush.android.ClickEvent;
import ai.openpush.android.ForegroundEvent;
import ai.openpush.android.IdentityResult;
import ai.openpush.android.OpenPushVerification;
import ai.openpush.android.PushSubscriptionChange;
import ai.openpush.android.PushSubscriptionState;
import ai.openpush.android.inappmessages.InAppMessageClickEvent;
import ai.openpush.android.inappmessages.InAppMessageLifecycleEvent;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/** Native models to the Unity wire JSON (snake_case, same keys the iOS bridge emits). */
final class OpenPushUnityJson {
    private OpenPushUnityJson() {}

    private static JSONObject put(JSONObject target, String key, Object value) {
        try {
            target.put(key, value == null ? JSONObject.NULL : value);
        } catch (JSONException ignored) {
            // put only throws for a null key or a non-finite number; neither reaches here
        }
        return target;
    }

    static String message(String message) { return put(new JSONObject(), "message", message).toString(); }

    static String externalId(String externalId) { return put(new JSONObject(), "external_id", externalId).toString(); }

    static String permission(Boolean granted) { return put(new JSONObject(), "granted", granted != null && granted).toString(); }

    static JSONObject subscriptionObject(PushSubscriptionState state) {
        if (state == null) return null;
        JSONObject o = new JSONObject();
        put(o, "id", state.getId());
        put(o, "token", state.getToken());
        put(o, "opted_in", state.getOptedIn());
        return o;
    }

    static String subscription(PushSubscriptionState state) {
        return subscription(state, null);
    }

    /**
     * The snapshot plus the wire status the native SDK keeps internal. Only the "not answered"
     * states are reported (-18 never asked, -19 asked and still askable); null lets the C#
     * calculator derive 1 or 0 from the permission, as it does for every other state.
     */
    static String subscription(PushSubscriptionState state, Integer status) {
        JSONObject o = subscriptionObject(state);
        if (o == null) o = new JSONObject();
        if (status != null) put(o, "status", status);
        return o.toString();
    }

    /** Wire status for a permission that is neither granted nor permanently denied, else null. */
    static Integer unansweredStatus(boolean granted, boolean canRequest, boolean askedBefore) {
        if (granted || !canRequest) return null;
        return askedBefore ? -19 : -18;
    }

    static String subscriptionChange(PushSubscriptionChange change) {
        JSONObject o = new JSONObject();
        put(o, "previous", subscriptionObject(change.getPrevious()));
        put(o, "current", subscriptionObject(change.getCurrent()));
        return o.toString();
    }

    static String click(ClickEvent event) {
        JSONObject o = new JSONObject();
        put(o, "message_id", event.getMessageId());
        put(o, "action_id", event.getActionId());
        put(o, "url", event.getNotification().getLaunchUrl());
        put(o, "action_url", event.getActionUrl());
        put(o, "launch_url_opened", event.getLaunchUrlOpened());
        put(o, "notification", event.getNotification().toJson());
        put(o, "data", new JSONObject(event.getData()));
        return o.toString();
    }

    static String foreground(ForegroundEvent event, String completionId) {
        JSONObject o = new JSONObject();
        put(o, "message_id", event.getMessageId());
        put(o, "completion_id", completionId);
        put(o, "notification", event.getNotification().toJson());
        put(o, "data", new JSONObject(event.getData()));
        return o.toString();
    }

    static String background(BackgroundMessageEvent event) {
        JSONObject o = new JSONObject();
        put(o, "message_id", event.getMessageId());
        put(o, "notification", event.getNotification().toJson());
        put(o, "data", new JSONObject(event.getData()));
        return o.toString();
    }

    static String identity(IdentityResult result) {
        JSONObject o = new JSONObject();
        put(o, "success", result instanceof IdentityResult.Success);
        put(o, "identity_rejected_note",
            result instanceof IdentityResult.IdentityRejected ? ((IdentityResult.IdentityRejected) result).getNote() : null);
        put(o, "error", result instanceof IdentityResult.Failed ? ((IdentityResult.Failed) result).getMessage() : null);
        return o.toString();
    }

    /** A phone-code result: {"verification": {...}}, enum values as the server's snake_case words. */
    static String verification(OpenPushVerification v) {
        JSONObject o = new JSONObject();
        put(o, "id", v.getId());
        put(o, "masked_number", v.getMaskedNumber());
        put(o, "channel", wireWord(v.getChannel().name()));
        put(o, "status", wireWord(v.getStatus().name()));
        put(o, "attempts", v.getAttempts());
        put(o, "expires_at", v.getExpiresAt() == null ? null : v.getExpiresAt().getTime());
        return put(new JSONObject(), "verification", o).toString();
    }

    /** A failed phone-code call: {"error": {"code", "message"}}. */
    static String verifyError(String code, String message) {
        JSONObject o = new JSONObject();
        put(o, "code", code);
        put(o, "message", message);
        return put(new JSONObject(), "error", o).toString();
    }

    /** An enum constant as the wire word: RATE_LIMITED → rate_limited, MAX_ATTEMPTS → max_attempts. */
    static String wireWord(String enumName) { return enumName.toLowerCase(Locale.ROOT); }

    /**
     * A phone-code error code as the server's word, which C# and the iOS bridge use: the enum name
     * lower-cased, except NO_CREDIT, which the server calls credit.
     */
    static String verifyCode(String enumName) {
        return "NO_CREDIT".equals(enumName) ? "credit" : wireWord(enumName);
    }

    static String iamLifecycle(InAppMessageLifecycleEvent event) {
        JSONObject o = new JSONObject();
        put(o, "message_id", event.getMessageId());
        put(o, "phase", event.getPhase());
        return o.toString();
    }

    static String iamClick(InAppMessageClickEvent event) {
        JSONObject o = new JSONObject();
        put(o, "message_id", event.getMessageId());
        put(o, "action_id", event.getActionId());
        put(o, "url", event.getUrl());
        put(o, "url_target", event.getUrlTarget());
        put(o, "close", event.getClosingMessage());
        put(o, "first_click", event.getFirstClick());
        put(o, "kind", iamClickKind(event.getUrl(), event.getActionId(), event.getClosingMessage()));
        return o.toString();
    }

    /**
     * The composer kind, as far as the native click model exposes it: a url, a named (custom)
     * action, or a close. Prompt, tag and outcome effects are applied natively and are not on the
     * model, so those taps report the nearest of the three, or null (C# then derives "click").
     */
    static String iamClickKind(String url, String actionId, boolean closes) {
        if (url != null && !url.isEmpty()) return "url";
        if (actionId != null && !actionId.isEmpty()) return "custom";
        return closes ? "close" : null;
    }

    static Map<String, String> stringMap(String json) {
        Map<String, String> map = new HashMap<>();
        JSONObject o = parse(json);
        if (o == null) return map;
        Iterator<String> keys = o.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            Object value = o.opt(key);
            if (value != null && value != JSONObject.NULL) map.put(key, String.valueOf(value));
        }
        return map;
    }

    static Map<String, Object> objectMap(String json) {
        JSONObject o = parse(json);
        return o == null ? new HashMap<>() : toMap(o, false);
    }

    static Map<String, Object> nonNullObjectMap(String json) {
        JSONObject o = parse(json);
        return o == null ? new HashMap<>() : toMap(o, true);
    }

    static List<String> stringList(String json) {
        List<String> list = new ArrayList<>();
        if (json == null) return list;
        try {
            JSONArray array = new JSONArray(json);
            for (int i = 0; i < array.length(); i++) {
                if (!array.isNull(i)) list.add(array.optString(i));
            }
        } catch (JSONException ignored) {
            // malformed input reads as empty
        }
        return list;
    }

    static String fromStringMap(Map<String, String> map) {
        return map == null ? "{}" : new JSONObject(map).toString();
    }

    private static JSONObject parse(String json) {
        if (json == null || json.isEmpty()) return null;
        try {
            return new JSONObject(json);
        } catch (JSONException e) {
            return null;
        }
    }

    private static Map<String, Object> toMap(JSONObject o, boolean dropNulls) {
        Map<String, Object> map = new HashMap<>();
        Iterator<String> keys = o.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            Object value = unwrap(o.opt(key), dropNulls);
            if (value == null && dropNulls) continue;
            map.put(key, value);
        }
        return map;
    }

    private static Object unwrap(Object value, boolean dropNulls) {
        if (value == null || value == JSONObject.NULL) return null;
        if (value instanceof JSONObject) return toMap((JSONObject) value, dropNulls);
        if (value instanceof JSONArray) {
            JSONArray array = (JSONArray) value;
            List<Object> list = new ArrayList<>();
            for (int i = 0; i < array.length(); i++) list.add(unwrap(array.opt(i), dropNulls));
            return list;
        }
        return value;
    }
}
