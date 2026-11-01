package ai.openpush.unity;

import ai.openpush.android.ForegroundEvent;
import ai.openpush.android.OpenPush;
import ai.openpush.android.inappmessages.OpenPushInAppMessages;
import ai.openpush.android.liveupdates.OpenPushLiveUpdates;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Looper;
import android.provider.Settings;
import android.util.Log;

import java.lang.ref.WeakReference;
import java.util.ArrayDeque;
import java.util.concurrent.atomic.AtomicBoolean;

import kotlin.Unit;

/**
 * The Unity package's typed adapter over the native OpenPush Android SDK. C# calls these static
 * methods with primitives and JSON strings and implements three small Java interfaces, so it never
 * proxies kotlin.jvm.functions types. Threading is C#'s job: events are emitted on whatever thread
 * the native SDK uses, and C# marshals them to the Unity main thread.
 */
public final class OpenPushUnityBridge {
    static final long FOREGROUND_DECISION_TIMEOUT_MS = 1000L;
    private static final int MAX_QUEUED_EVENTS = 50;
    private static final String TAG = "OpenPush";
    private static final String PREFS = "openpush.unity";
    private static final String PERMISSION_ASKED_KEY = "permissionRequested";

    private static final Object LOCK = new Object();
    private static final ArrayDeque<String[]> QUEUED = new ArrayDeque<>();
    private static final AtomicBoolean HOOKS_INSTALLED = new AtomicBoolean();
    private static final OpenPushUnityForegroundDecisions DECISIONS = new OpenPushUnityForegroundDecisions();
    private static final ThreadLocal<Boolean> HOST_DRIVEN_FOREGROUND = ThreadLocal.withInitial(() -> Boolean.FALSE);

    private static volatile OpenPushUnityListener listener;
    private static volatile String credential;
    private static volatile Context appContext;
    private static volatile WeakReference<Activity> activity = new WeakReference<>(null);
    /** Native log lines are copied to logcat only in a debuggable build, as the native SDK does. */
    private static volatile boolean logcat;

    private OpenPushUnityBridge() {}

    // ---------------------------------------------------------------- events

    /** Registers the C# listener and replays events emitted before it existed (newest 50). */
    public static void setListener(OpenPushUnityListener value) {
        String[][] replay;
        synchronized (LOCK) {
            listener = value;
            replay = QUEUED.toArray(new String[0][]);
            QUEUED.clear();
        }
        if (value == null) return;
        for (String[] event : replay) deliver(value, event[0], event[1]);
    }

    static void emit(String kind, String json) {
        OpenPushUnityListener current;
        synchronized (LOCK) {
            current = listener;
            if (current == null) {
                if (QUEUED.size() == MAX_QUEUED_EVENTS) QUEUED.removeFirst();
                QUEUED.addLast(new String[] {kind, json});
                return;
            }
        }
        deliver(current, kind, json);
    }

    private static void deliver(OpenPushUnityListener target, String kind, String json) {
        try {
            target.onEvent(kind, json);
        } catch (Throwable t) {
            Log.w(TAG, "Unity listener threw on " + kind + ": " + t);
        }
    }

    private static void installHooks() {
        if (!HOOKS_INSTALLED.compareAndSet(false, true)) return;
        OpenPush.setOnLog(message -> {
            if (logcat) Log.i(TAG, message);
            emit("log", OpenPushUnityJson.message(message));
            return Unit.INSTANCE;
        });
        OpenPush.setOnRegistered(() -> {
            emit("registered", "{}");
            return Unit.INSTANCE;
        });
        OpenPush.setOnRegistrationFailed(message -> {
            emit("registration_failed", OpenPushUnityJson.message(message));
            return Unit.INSTANCE;
        });
        OpenPush.setOnIdentityVerificationRequired(externalId -> {
            emit("identity_verification_required", OpenPushUnityJson.externalId(externalId));
            return Unit.INSTANCE;
        });
        OpenPush.PushSubscription.addChangeObserver(change -> {
            emit("push_subscription", OpenPushUnityJson.subscriptionChange(change));
            return Unit.INSTANCE;
        });
        OpenPush.Notifications.addPermissionObserver(granted -> {
            emit("permission", OpenPushUnityJson.permission(granted));
            return Unit.INSTANCE;
        });
        OpenPush.Notifications.addForegroundLifecycleListener(event -> {
            onForeground(event);
            return Unit.INSTANCE;
        });
        // Background (data-only) pushes: never displayed, handed over with their data. The
        // native SDK calls this on the delivering (FCM) thread; C# marshals to the Unity thread. A
        // push delivered before initialize in this process has no listener and is dropped natively.
        OpenPush.Notifications.addBackgroundMessageListener(event -> {
            emit("background", OpenPushUnityJson.background(event));
            return Unit.INSTANCE;
        });
        // Registered last: the native SDK replays held cold-start clicks into the first listener.
        OpenPush.Notifications.addClickListener(click -> {
            emit("click", OpenPushUnityJson.click(click));
            return Unit.INSTANCE;
        });
        OpenPushInAppMessages.addLifecycleListener(event -> {
            emit("iam_lifecycle", OpenPushUnityJson.iamLifecycle(event));
            return Unit.INSTANCE;
        });
        OpenPushInAppMessages.addClickListener(event -> {
            emit("iam_click", OpenPushUnityJson.iamClick(event));
            return Unit.INSTANCE;
        });
    }

    private static void onForeground(ForegroundEvent event) {
        // A host-driven ProcessForeground already ran the C# handlers on the Unity thread.
        if (Boolean.TRUE.equals(HOST_DRIVEN_FOREGROUND.get())) return;
        // With no C# listener yet the native default (display) stands; a foreground event is never
        // queued for a later replay, because a late decision about a shown message means nothing.
        if (listener == null) return;
        boolean canWait = Looper.myLooper() != Looper.getMainLooper();
        if (!canWait) {
            emit("foreground", OpenPushUnityJson.foreground(event, null));
            return;
        }
        String id = DECISIONS.open();
        emit("foreground", OpenPushUnityJson.foreground(event, id));
        if (!DECISIONS.await(id, FOREGROUND_DECISION_TIMEOUT_MS)) event.preventDefault();
    }

    // ---------------------------------------------------------------- core

    public static void initialize(Activity host, String appId, String sdkKey, String serverUrl,
                                  String wrapper, String initialCredential) {
        credential = initialCredential;
        if (host != null) {
            activity = new WeakReference<>(host);
            appContext = host.getApplicationContext();
        }
        Context context = appContext;
        logcat = context != null && debuggable(context.getApplicationInfo().flags);
        OpenPush.setSdkWrapper(wrapper);
        installHooks();
        OpenPush.initialize(host, appId, sdkKey, serverUrl, () -> credential);
    }

    /**
     * A debuggable build (a Development Build, or android:debuggable) copies native log lines to
     * logcat. A release build does not: the lines can name the external id. Every line still reaches
     * C#, where OpenPush.LogLevel filters it.
     */
    static boolean debuggable(int applicationFlags) {
        return (applicationFlags & ApplicationInfo.FLAG_DEBUGGABLE) != 0;
    }

    public static void setCredential(String value) { credential = value; }

    public static boolean isInitialized() { return OpenPush.isInitialized(); }

    public static String sdkVersion() { return OpenPush.getSdkVersion(); }

    public static String currentToken() { return OpenPush.getCurrentToken(); }

    /**
     * A provider token from the host. An external id is attached only through login: the native
     * two-argument form logs a new id in first (no auth hash), as {@code OpenPush.Register(token, id)}
     * always meant, and the plain form keeps the current login.
     */
    @SuppressWarnings("deprecation")
    public static void register(String token, String externalId) {
        if (externalId == null || externalId.isEmpty()) OpenPush.register(token);
        else OpenPush.register(token, externalId);
    }

    public static void receipt(String type, String messageId, OpenPushUnityCallback done) {
        OpenPush.receipt(type, messageId, () -> {
            if (done != null) done.onResult("{}");
            return Unit.INSTANCE;
        });
    }

    public static void trackEvent(String name, String propertiesJson) {
        OpenPush.trackEvent(name, OpenPushUnityJson.objectMap(propertiesJson), null);
    }

    public static void login(String externalId, String authHash, OpenPushUnityCallback done) {
        OpenPush.login(externalId, authHash, result -> {
            if (done != null) done.onResult(OpenPushUnityJson.identity(result));
            return Unit.INSTANCE;
        });
    }

    public static void logout(OpenPushUnityCallback done) {
        OpenPush.logout(result -> {
            if (done != null) done.onResult(OpenPushUnityJson.identity(result));
            return Unit.INSTANCE;
        });
    }

    public static void onAppForegrounded() { OpenPush.onAppForegrounded(); }

    public static void clearToken() { OpenPush.clearToken(); }

    // ---------------------------------------------------------------- user and subscription

    public static void addTags(String json) { OpenPush.User.addTags(OpenPushUnityJson.stringMap(json)); }

    public static void removeTags(String keysJson) { OpenPush.User.removeTags(OpenPushUnityJson.stringList(keysJson)); }

    public static String getTags() { return OpenPushUnityJson.fromStringMap(OpenPush.User.getTags()); }

    public static void addAliases(String json) { OpenPush.User.addAliases(OpenPushUnityJson.stringMap(json)); }

    public static void removeAliases(String keysJson) { OpenPush.User.removeAliases(OpenPushUnityJson.stringList(keysJson)); }

    public static String externalId() { return OpenPush.User.getExternalId(); }

    // C# has already normalized the number to E.164 and refused the rest, so the native answer
    // (false only for a number that is not E.164) is not passed back.
    public static void addSms(String number) { OpenPush.User.addSms(number); }

    public static void removeSms(String number) { OpenPush.User.removeSms(number); }

    public static void addWhatsApp(String number) { OpenPush.User.addWhatsApp(number); }

    public static void removeWhatsApp(String number) { OpenPush.User.removeWhatsApp(number); }

    // C# has already trimmed, lower-cased and checked the address, so the native answer (false only
    // for an address the check refuses) and the final-answer callback are not passed back.
    public static void addEmail(String address) { OpenPush.User.addEmail(address); }

    public static void removeEmail(String address) { OpenPush.User.removeEmail(address); }

    // ---------------------------------------------------------------- phone codes

    public static void verifyStart(String phone, String channel, String locale, OpenPushUnityCallback done) {
        OpenPushUnityPhoneCodes.start(phone, channel, locale, done);
    }

    public static void verifyCheck(String verificationId, String code, OpenPushUnityCallback done) {
        OpenPushUnityPhoneCodes.check(verificationId, code, done);
    }

    public static void verifyResend(String verificationId, String channel, OpenPushUnityCallback done) {
        OpenPushUnityPhoneCodes.resend(verificationId, channel, done);
    }

    /**
     * The native snapshot plus the "not answered" wire status the native SDK keeps internal, so the
     * C# NotificationTypes calculator can report -18 (never asked) and -19 (asked, still askable).
     */
    public static String subscription() {
        boolean granted = OpenPush.Notifications.getPermission();
        Integer status = OpenPushUnityJson.unansweredStatus(granted, !granted && canRequestPermission(), permissionAsked());
        return OpenPushUnityJson.subscription(OpenPush.LivePushSubscription.snapshot(), status);
    }

    public static void optIn() { OpenPush.PushSubscription.optIn(); }

    public static void optOut() { OpenPush.PushSubscription.optOut(); }

    // ---------------------------------------------------------------- notifications

    public static boolean permission() { return OpenPush.Notifications.getPermission(); }

    /**
     * The native answer, corrected for the Unity prompt path: the native SDK records "asked" only
     * when it shows the prompt itself, and the Unity layer prompts through UnityEngine.Android.Permission,
     * so after a Unity-driven request the rationale flag decides (true while the prompt can show again).
     */
    public static boolean canRequestPermission() {
        if (!OpenPush.Notifications.getCanRequestPermission()) return false;
        if (!permissionAsked()) return true;
        Activity current = activity.get();
        return current != null && current.shouldShowRequestPermissionRationale(Manifest.permission.POST_NOTIFICATIONS);
    }

    private static boolean permissionAsked() {
        Context context = appContext;
        return context != null && context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getBoolean(PERMISSION_ASKED_KEY, false);
    }

    /**
     * The Unity layer asks for POST_NOTIFICATIONS through UnityEngine.Android.Permission (the player
     * activity does not forward onRequestPermissionsResult); this hands the answer to the native SDK
     * and re-syncs the subscription status.
     */
    public static void onPermissionResult(boolean granted) {
        Context context = appContext;
        if (context != null) {
            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean(PERMISSION_ASKED_KEY, true).apply();
        }
        OpenPush.Notifications.onPermissionResult(OpenPush.Notifications.PERMISSION_REQUEST_CODE,
            new int[] {granted ? PackageManager.PERMISSION_GRANTED : PackageManager.PERMISSION_DENIED});
        OpenPush.onAppForegrounded();
    }

    public static void openNotificationSettings(Activity activity) {
        if (activity == null) return;
        try {
            activity.startActivity(new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                .putExtra(Settings.EXTRA_APP_PACKAGE, activity.getPackageName()));
        } catch (RuntimeException e) {
            activity.startActivity(new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                Uri.fromParts("package", activity.getPackageName(), null)));
        }
    }

    public static void clearAll() { OpenPush.Notifications.clearAll(); }

    /** The OpenPush renderer posts each message under messageId.hashCode() (OpenPushNotificationRenderer). */
    public static void remove(String messageId) {
        if (messageId != null) OpenPush.Notifications.remove(messageId.hashCode());
    }

    public static void processClick(String dataJson) { OpenPush.Notifications.processClick(OpenPushUnityJson.stringMap(dataJson)); }

    public static void processForeground(String dataJson) {
        HOST_DRIVEN_FOREGROUND.set(Boolean.TRUE);
        try {
            OpenPush.Notifications.processForegroundMessage(OpenPushUnityJson.stringMap(dataJson));
        } finally {
            HOST_DRIVEN_FOREGROUND.set(Boolean.FALSE);
        }
    }

    /** The facade claims a host-forwarded payload before its handlers run; false means a repeat. */
    public static boolean beginReceived(String dataJson) {
        return OpenPush.Notifications.beginReceived(OpenPushUnityJson.stringMap(dataJson));
    }

    public static void finishReceived(String dataJson, boolean handled) {
        OpenPush.Notifications.finishReceived(OpenPushUnityJson.stringMap(dataJson), handled);
    }

    public static void processDisplayed(String dataJson) { OpenPush.Notifications.processDisplayed(OpenPushUnityJson.stringMap(dataJson)); }

    public static OpenPushUnityCompletion foregroundCompletion() { return DECISIONS; }

    // ---------------------------------------------------------------- in-app messages

    public static void setPaused(boolean paused) { OpenPushInAppMessages.setPaused(paused); }

    public static boolean isPaused() { return OpenPushInAppMessages.getPaused(); }

    public static void addTriggers(String json) { OpenPushInAppMessages.addTriggers(OpenPushUnityJson.nonNullObjectMap(json)); }

    public static void removeTriggers(String keysJson) { OpenPushInAppMessages.removeTriggers(OpenPushUnityJson.stringList(keysJson)); }

    public static void clearTriggers() { OpenPushInAppMessages.clearTriggers(); }

    public static void fetch() { OpenPushInAppMessages.fetch(); }

    // ---------------------------------------------------------------- live updates

    public static String handleLiveUpdate(Context context, String dataJson, String collapseId) {
        if (context == null) return "UNSUPPORTED";
        return OpenPushLiveUpdates.handle(context.getApplicationContext(), OpenPushUnityJson.stringMap(dataJson),
            collapseId == null || collapseId.isEmpty() ? null : collapseId).name();
    }

    public static String liveUpdateCapability(Context context) {
        return context == null ? "UNSUPPORTED" : OpenPushLiveUpdates.capability(context.getApplicationContext()).name();
    }

    public static boolean openPromotionSettings(Context context) {
        return context != null && OpenPushLiveUpdates.openPromotionSettings(context);
    }
}
