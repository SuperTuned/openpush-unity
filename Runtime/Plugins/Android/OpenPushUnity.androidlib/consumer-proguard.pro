# Everything the Unity C# layer reaches by name over JNI. R8 cannot see those calls, so without these
# rules a Minify build renames ai.openpush.android.OpenPush and fails at runtime.
-keep class ai.openpush.** { public *; }
-keep class ai.openpush.unity.** { *; }
-keep interface kotlin.jvm.functions.Function* { *; }
-keep class kotlin.Unit { public static final kotlin.Unit INSTANCE; }
# The three interfaces C# implements with AndroidJavaProxy.
-keep interface ai.openpush.unity.OpenPushUnityListener { *; }
-keep interface ai.openpush.unity.OpenPushUnityCallback { *; }
-keep interface ai.openpush.unity.OpenPushUnityCompletion { *; }
-keepclassmembers class * implements ai.openpush.unity.OpenPushUnityListener { *; }
-keepclassmembers class * implements ai.openpush.unity.OpenPushUnityCallback { *; }
# Still used by DeviceCredentialStore (migration and credential source).
-keep class ai.openpush.unity.OpenPushCredentialStore { *; }
# The adapter classes C# calls by name: the static bridge and the foreground completion it hands out.
-keep class ai.openpush.unity.OpenPushUnityBridge { public static *; }
-keep class ai.openpush.unity.OpenPushUnityForegroundDecisions { public *; }
# The live-updates renderer references API 36 types behind SDK_INT guards; with compileSdk 34 they
# are absent from android.jar and R8 fails on the missing classes.
-dontwarn android.app.Notification$ProgressStyle
-dontwarn android.app.Notification$ProgressStyle$Segment
