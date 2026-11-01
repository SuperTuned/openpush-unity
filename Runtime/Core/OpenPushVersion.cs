namespace OpenPushSDK.Core
{
    /// <summary>
    /// The Unity package version: the single source for package.json (checked by a test), the
    /// wrapper string sent to the native SDKs, and OpenPush.WrapperVersion.
    /// </summary>
    public static class OpenPushVersion
    {
        public const string Current = "1.2.2";
        public const string WrapperName = "unity";
        public static string Wrapper => WrapperName + "/" + Current;
    }
}
