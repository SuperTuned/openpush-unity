package ai.openpush.unity;

/** Implemented by the Unity C# layer: the JSON result of one async call. */
public interface OpenPushUnityCallback {
    void onResult(String json);
}
