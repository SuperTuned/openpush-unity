package ai.openpush.unity;

/** RF3 on the Android side: one decision per foreground event, a timeout that displays, no leaks. */
public final class OpenPushUnityBridgeForegroundTest {
    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    public static void main(String[] args) throws Exception {
        OpenPushUnityForegroundDecisions decisions = new OpenPushUnityForegroundDecisions();

        String answered = decisions.open();
        new Thread(() -> decisions.complete(answered, false)).start();
        check(!decisions.await(answered, 2000), "a suppress decision must be returned");

        String silent = decisions.open();
        long start = System.nanoTime();
        check(decisions.await(silent, 50), "a timed-out decision displays");
        check((System.nanoTime() - start) >= 40_000_000L, "await must wait for the timeout");

        String twice = decisions.open();
        decisions.complete(twice, false);
        decisions.complete(twice, true);
        check(!decisions.await(twice, 10), "the first decision wins");

        decisions.complete("fg-unknown", false);
        decisions.complete(null, false);
        check(decisions.pendingCount() == 0, "nothing may stay pending: " + decisions.pendingCount());
        System.out.println("✓ foreground decisions complete once, time out to display, and never leak");
    }
}
