package ai.openpush.unity;

import ai.openpush.android.OpenPush;
import ai.openpush.android.OpenPushVerification;
import ai.openpush.android.OpenPushVerifyChannel;
import ai.openpush.android.OpenPushVerifyError;
import ai.openpush.android.OpenPushVerifyResult;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

import kotlin.Unit;
import kotlin.jvm.functions.Function1;

/**
 * Phone codes for the Unity bridge. An OpenPushVerification carries the full number that check and
 * resend need and has no public constructor, so every one the native SDK returns is held here by id,
 * in memory only (as natively: never persisted), and C# names it by id. Each call answers its
 * callback exactly once, with the verification or the error (OpenPushUnityJson writes both).
 */
final class OpenPushUnityPhoneCodes {
    private static final Map<String, OpenPushVerification> HELD = new ConcurrentHashMap<>();
    private static final String UNKNOWN = "unknown verification — start a new one";

    private OpenPushUnityPhoneCodes() {}

    static void start(String phone, String channel, String locale, OpenPushUnityCallback done) {
        OpenPush.Verify.start(phone, channel(channel, OpenPushVerifyChannel.AUTO), locale, answer(done));
    }

    static void check(String verificationId, String code, OpenPushUnityCallback done) {
        OpenPushVerification held = held(verificationId);
        if (held == null) { reply(done, OpenPushUnityJson.verifyError("refused", UNKNOWN)); return; }
        OpenPush.Verify.check(held, code == null ? "" : code, answer(done));
    }

    static void resend(String verificationId, String channel, OpenPushUnityCallback done) {
        OpenPushVerification held = held(verificationId);
        if (held == null) { reply(done, OpenPushUnityJson.verifyError("refused", UNKNOWN)); return; }
        OpenPush.Verify.resend(held, channel(channel, held.getChannel()), answer(done));
    }

    private static OpenPushVerification held(String verificationId) {
        return verificationId == null ? null : HELD.get(verificationId);
    }

    /** The native callback: holds a returned verification, then answers C#. Runs on an SDK thread. */
    private static Function1<OpenPushVerifyResult, Unit> answer(OpenPushUnityCallback done) {
        return result -> {
            if (result instanceof OpenPushVerifyResult.Success) {
                OpenPushVerification verification = ((OpenPushVerifyResult.Success) result).getVerification();
                HELD.put(verification.getId(), verification);
                reply(done, OpenPushUnityJson.verification(verification));
            } else if (result instanceof OpenPushVerifyResult.Failure) {
                OpenPushVerifyError failure = ((OpenPushVerifyResult.Failure) result).getError();
                reply(done, OpenPushUnityJson.verifyError(OpenPushUnityJson.verifyCode(failure.getCode().name()), failure.getMessage()));
            } else {
                reply(done, OpenPushUnityJson.verifyError("network", "the native SDK returned no result"));
            }
            return Unit.INSTANCE;
        };
    }

    private static void reply(OpenPushUnityCallback done, String json) {
        if (done != null) done.onResult(json);
    }

    /** C# sends the wire word; an unknown or missing one falls back rather than failing the call. */
    private static OpenPushVerifyChannel channel(String wire, OpenPushVerifyChannel fallback) {
        if (wire == null) return fallback;
        for (OpenPushVerifyChannel value : OpenPushVerifyChannel.values()) {
            if (OpenPushUnityJson.wireWord(value.name()).equals(wire)) return value;
        }
        return fallback;
    }
}
