// Just enough of UnityEngine for Runtime/Unity/OpenPushSettings.cs to compile and run headless, so the
// asset's field initializers and its new-asset defaults are tested on the real class.
namespace UnityEngine
{
    public class Object { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeField : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class HideInInspector : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class TooltipAttribute : System.Attribute { public TooltipAttribute(string tooltip) { } }

    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
    public sealed class HeaderAttribute : System.Attribute { public HeaderAttribute(string header) { } }

    internal static class Application
    {
        public static string identifier { get; set; } = "com.example.game";
    }

    internal static class Resources
    {
        public static T Load<T>(string path) where T : class => null;
    }
}
