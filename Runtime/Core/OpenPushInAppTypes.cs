using System.Collections.Generic;

namespace OpenPushSDK.Core
{
    public sealed class OpenPushInAppMessage
    {
        public string Id, HtmlPath, Layout, Editor;
        public IReadOnlyDictionary<string, object> Blocks;
        public IReadOnlyList<IReadOnlyList<InAppCondition>> Triggers;
        public double? DisplayDuration;
        public int RedisplayLimit = 1;
        public double RedisplayDelay;
    }
    public sealed class OpenPushInAppClick
    {
        /// <summary>
        /// What the action does — url, prompt_push, prompt_location, tag,
        /// outcome, custom, close or a plain click. <see cref="ActionId"/> is
        /// the custom action's own name and is null for every other kind.
        /// </summary>
        public string Kind;
        public string ClickId, BlockKey, ActionId, Url, UrlTarget, Outcome, Prompt;
        public IReadOnlyDictionary<string, string> Tags;
        public bool Closes;
    }
    public sealed class InAppCondition
    {
        public string Id, Kind, Property, Operator;
        public object Value;
    }
}
