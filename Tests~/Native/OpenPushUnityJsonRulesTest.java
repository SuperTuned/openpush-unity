package ai.openpush.unity;

/** The pure decisions in the JSON helper: the in-app click kind, the unanswered status and the phone-code error word. */
public final class OpenPushUnityJsonRulesTest {
    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    public static void main(String[] args) {
        check("url".equals(OpenPushUnityJson.iamClickKind("https://x", "claim", true)), "a url wins");
        check("custom".equals(OpenPushUnityJson.iamClickKind("", "claim", true)), "a named action is custom");
        check("close".equals(OpenPushUnityJson.iamClickKind(null, null, true)), "a bare close is close");
        check(OpenPushUnityJson.iamClickKind(null, "", false) == null, "nothing known leaves the kind to C#");

        check(OpenPushUnityJson.unansweredStatus(true, false, true) == null, "granted reports no status");
        check(OpenPushUnityJson.unansweredStatus(false, false, true) == null, "a permanent denial reports no status");
        check(Integer.valueOf(-18).equals(OpenPushUnityJson.unansweredStatus(false, true, false)), "never asked is -18");
        check(Integer.valueOf(-19).equals(OpenPushUnityJson.unansweredStatus(false, true, true)), "asked and still askable is -19");

        check("opted_out".equals(OpenPushUnityJson.verifyCode("OPTED_OUT")), "OPTED_OUT is opted_out");
        check("credit".equals(OpenPushUnityJson.verifyCode("NO_CREDIT")), "NO_CREDIT is the server's credit");
        check("no_rate".equals(OpenPushUnityJson.verifyCode("NO_RATE")), "NO_RATE is no_rate");
        check("rate_limited".equals(OpenPushUnityJson.verifyCode("RATE_LIMITED")), "other codes keep their lower-cased name");
        System.out.println("✓ in-app click kinds, unanswered permission statuses and phone-code errors map as the wire expects");
    }
}
