package ai.openpush.unity;

/** Answers one foreground decision: display (true) or suppress (false). */
public interface OpenPushUnityCompletion {
    void complete(String id, boolean display);
}
