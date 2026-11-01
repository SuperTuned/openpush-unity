package ai.openpush.unity;

/** Native log lines reach logcat only from a debuggable build, as the native SDK's own lines do. */
public final class OpenPushUnityBridgeLogcatTest {
    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    public static void main(String[] args) {
        // ApplicationInfo.FLAG_DEBUGGABLE is 1 << 1; a release build has it clear.
        check(OpenPushUnityBridge.debuggable(1 << 1), "a debuggable build copies log lines to logcat");
        check(OpenPushUnityBridge.debuggable((1 << 1) | (1 << 3)), "other flags do not hide the debuggable one");
        check(!OpenPushUnityBridge.debuggable(0), "a release build writes nothing to logcat");
        check(!OpenPushUnityBridge.debuggable(~(1 << 1)), "only the debuggable flag turns logcat on");
        System.out.println("✓ native log lines reach logcat only from a debuggable build");
    }
}
