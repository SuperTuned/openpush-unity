package ai.openpush.unity;

/** Implemented by the Unity C# layer (AndroidJavaProxy): every native event as (kind, json). */
public interface OpenPushUnityListener {
    void onEvent(String kind, String json);
}
