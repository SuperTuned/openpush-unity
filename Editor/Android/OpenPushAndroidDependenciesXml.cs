using System.Text;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The EDM4U dependency file (verified on Unity 2022.3 with EDM4U 1.2.189). The
    /// in-package repository comes first so the vendored natives win; Maven Central and Google Maven
    /// supply their transitive dependencies (firebase-messaging 24.0.2, kotlin-stdlib 2.0.21, androidx).
    /// </summary>
    internal static class OpenPushAndroidDependenciesXml
    {
        internal const string RepositoryPath = "Packages/ai.openpush.unity/Runtime/Plugins/Android/maven~";
        internal const string NativeVersion = "1.2.2";

        internal static string Build(OpenPushAndroidPushDelivery delivery)
        {
            var sb = new StringBuilder();
            sb.Append("<dependencies>\n");
            sb.Append("  <androidPackages>\n");
            sb.Append("    <repositories>\n");
            sb.Append("      <repository>").Append(RepositoryPath).Append("</repository>\n");
            sb.Append("      <repository>https://repo.maven.apache.org/maven2</repository>\n");
            sb.Append("      <repository>https://maven.google.com</repository>\n");
            sb.Append("    </repositories>\n");
            Package(sb, "openpush-android");
            Package(sb, "openpush-android-in-app-messages");
            Package(sb, "openpush-android-live-updates");
            if (delivery == OpenPushAndroidPushDelivery.Automatic) Package(sb, "openpush-android-fcm");
            sb.Append("  </androidPackages>\n");
            sb.Append("</dependencies>\n");
            return sb.ToString();
        }

        private static void Package(StringBuilder sb, string artifact)
            => sb.Append("    <androidPackage spec=\"ai.openpush:").Append(artifact).Append(':').Append(NativeVersion).Append("\" />\n");
    }
}
