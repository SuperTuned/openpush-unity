namespace OpenPushSDK.Core
{
    /// <summary>
    /// How much of the SDK's diagnostic stream reaches <c>OpenPush.OnLog</c>.
    /// A line is delivered when its own level is at or below the configured one,
    /// so <see cref="Error"/> is the quietest useful setting and
    /// <see cref="None"/> silences the sink entirely.
    ///
    /// Lines that carry a leading glyph use it to signal their level, which is
    /// also how the level of those call sites is decided:
    /// <list type="bullet">
    /// <item><description><c>✗</c> — <see cref="Error"/>: something the host asked
    /// for did not happen.</description></item>
    /// <item><description><c>⚠</c> — <see cref="Warning"/>: it happened, but not
    /// the way the host meant.</description></item>
    /// <item><description><c>✓</c> and <c>→</c> — <see cref="Info"/>: a request
    /// left, or a request landed.</description></item>
    /// <item><description><c>•</c> and <c>·</c> — <see cref="Verbose"/>: routine
    /// bookkeeping.</description></item>
    /// </list>
    ///
    /// A few status lines carry no glyph at all — the attach banner, the
    /// app-session ping, a dropped receipt. Their level is set from what the
    /// line reports, and their text is left alone.
    ///
    /// The default is <see cref="Verbose"/>: the SDK has always said everything,
    /// and a level that silently drops lines a host was already reading would be
    /// a breaking change dressed up as a feature. Hosts lower it.
    /// </summary>
    public enum OpenPushLogLevel
    {
        /// <summary>Nothing reaches the sink. Not a level a line can carry.</summary>
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Verbose = 4,
    }
}
