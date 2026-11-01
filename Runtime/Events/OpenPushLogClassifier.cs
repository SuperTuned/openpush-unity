using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>The natives expose one log sink; the level is read off the line's leading glyph.</summary>
    internal static class OpenPushLogClassifier
    {
        internal static OpenPushLogLevel Classify(string line)
        {
            if (string.IsNullOrEmpty(line)) return OpenPushLogLevel.Info;
            var text = line.TrimStart();
            if (text.Length == 0) return OpenPushLogLevel.Info;
            switch (text[0])
            {
                case '✗': return OpenPushLogLevel.Error;
                case '⚠': return OpenPushLogLevel.Warning;
                case '✓':
                case '→': return OpenPushLogLevel.Info;
                case '•':
                case '·': return OpenPushLogLevel.Verbose;
                default: return OpenPushLogLevel.Info;
            }
        }

        internal static bool Passes(OpenPushLogLevel line, OpenPushLogLevel configured)
            => configured != OpenPushLogLevel.None && line != OpenPushLogLevel.None && line <= configured;
    }
}
