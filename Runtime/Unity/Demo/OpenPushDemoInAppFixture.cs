using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK
{
    /// <summary>The one in-app document the Editor can show: Demo mode only, no fetch, no engine.</summary>
    internal static class OpenPushDemoInAppFixture
    {
        internal const string BlocksJson =
            "{\"background\":{\"color\":\"#ffffff\",\"padding\":24},\"close\":{\"enabled\":true},"
          + "\"cards\":[{\"blocks\":["
          + "{\"kind\":\"text\",\"text\":\"Demo in-app message\",\"size\":20,\"bold\":true},"
          + "{\"kind\":\"text\",\"text\":\"Rendered in the Editor from a bundled fixture. On a device the native SDK presents the message your console published.\",\"size\":14},"
          + "{\"kind\":\"button\",\"text\":\"Close\",\"click\":{\"kind\":\"close\"}}]}]}";

        internal static OpenPushInAppMessage Message()
        {
            OpenPushJson.TryParse(BlocksJson, out var blocks);
            return new OpenPushInAppMessage
            {
                Id = "demo-fixture", Layout = "center", Editor = "blocks",
                Blocks = blocks as Dictionary<string, object> ?? new Dictionary<string, object>(),
            };
        }
    }
}
