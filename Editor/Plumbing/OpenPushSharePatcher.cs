using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Adds OpenPush to an existing Swift extension class by inserting whole lines and nothing else, so
    /// every byte of the developer's code outside them is kept (the rules of the iOS setup tool's share
    /// mode). Each insertion needs an anchor it must find first:
    /// <list type="bullet">
    /// <item><c>import OpenPushNSE</c> after the last file-level import;</item>
    /// <item>a stored handler property at the top of the class;</item>
    /// <item><c>if openPush.handleIfOpenPush(…) { return }</c> first in <c>didReceive</c>;</item>
    /// <item><c>if openPush.serviceExtensionTimeWillExpireIfHandling() { return }</c> first in
    /// <c>serviceExtensionTimeWillExpire</c>, or that whole override, passing on to <c>super</c>, when the
    /// class has none.</item>
    /// </list>
    /// Only the principal class's own body is searched. Conditional compilation (<c>#if</c>) inside it is
    /// never guessed at, and any missing anchor leaves the file untouched.
    /// </summary>
    internal sealed class OpenPushSharePatcher
    {
        internal enum Outcome { Patched, AlreadyShared, AnchorsMissing }

        internal sealed class Result
        {
            public Outcome Outcome;
            public string Text;
            public List<int> InsertedLines = new List<int>();
            public List<string> Missing = new List<string>();
        }

        private readonly string _className, _appGroup;

        internal OpenPushSharePatcher(string className, string appGroup)
        {
            _className = className;
            _appGroup = appGroup;
        }

        internal Result Patch(string source)
        {
            source = source ?? "";
            if (source.Contains("handleIfOpenPush(") || source.Contains("OpenPushNotificationServiceHandler"))
                return new Result { Outcome = Outcome.AlreadyShared, Text = source };
            var newline = source.Contains("\r\n") ? "\r\n" : "\n";
            var lines = source.Split(new[] { newline }, StringSplitOptions.None).ToList();

            var classLine = lines.FindIndex(DeclaresClass);
            var classEnd = classLine < 0 ? -1 : ClosingLine(classLine, lines);
            if (classLine < 0 || classEnd < 0)
                return Missing("class " + _className + " with its opening brace on the same line");
            var bodyStart = classLine + 1;
            var bodyEnd = classEnd; // exclusive
            for (var i = bodyStart; i < bodyEnd; i++)
                if (lines[i].Trim().StartsWith("#if")) return Missing("#if blocks inside class " + _className);

            var missing = new List<string>();
            var receive = BodyStart(@"func\s+didReceive\s*\(", lines, bodyStart, bodyEnd);
            string request = null, handler = null;
            if (receive != null)
            {
                var signature = string.Join(" ", lines.Skip(receive.Value.Function).Take(receive.Value.Body - receive.Value.Function + 1));
                request = Capture(@"didReceive\s*\(\s*_\s+(\w+)\s*:", signature);
                handler = Capture(@"withContentHandler\s+(\w+)\s*:", signature) ?? Capture(@"(withContentHandler)\s*:", signature);
            }
            if (receive == null || request == null || handler == null)
                missing.Add("override func didReceive(_:withContentHandler:) with its body's brace ending a line");
            const string expireMarker = @"func\s+serviceExtensionTimeWillExpire\s*\(\s*\)";
            var expire = BodyStart(expireMarker, lines, bodyStart, bodyEnd);
            var expireDeclared = false;
            for (var i = bodyStart; i < bodyEnd; i++) if (Regex.IsMatch(StripComment(lines[i]), expireMarker)) expireDeclared = true;
            if (expire == null && expireDeclared)
                missing.Add("override func serviceExtensionTimeWillExpire() with its body's brace ending a line");
            if (missing.Count > 0) return new Result { Outcome = Outcome.AnchorsMissing, Text = source, Missing = missing };

            var classIndent = Indentation(lines[classLine]);
            var unit = IndentUnit(lines, classLine, classIndent);
            var member = classIndent + unit;
            var property = Regex.IsMatch(source, @"\bopenPush\b") ? "openPushHandler" : "openPush";

            var insertions = new List<(int After, List<string> Lines)>
            {
                (receive.Value.Body, new List<string> { Indentation(lines[receive.Value.Function]) + unit
                    + "if " + property + ".handleIfOpenPush(" + request + ", withContentHandler: " + handler + ") { return }" }),
            };
            if (expire != null)
                insertions.Add((expire.Value.Body, new List<string> { Indentation(lines[expire.Value.Function]) + unit
                    + "if " + property + ".serviceExtensionTimeWillExpireIfHandling() { return }" }));
            var block = new List<string>
            {
                member + "private let " + property + " = OpenPushNotificationServiceHandler(options: {",
                member + unit + "var options = OpenPushNotificationServiceHandler.Options()",
                member + unit + "options.appGroup = " + SwiftString(_appGroup),
                member + unit + "return options",
                member + "}())",
            };
            if (expire == null)
                block.AddRange(new[]
                {
                    "",
                    member + "override func serviceExtensionTimeWillExpire() {",
                    member + unit + "if " + property + ".serviceExtensionTimeWillExpireIfHandling() { return }",
                    member + unit + "super.serviceExtensionTimeWillExpire()",
                    member + "}",
                });
            block.Add("");
            insertions.Add((classLine, block));
            if (!lines.Any(l => l.Trim() == "import OpenPushNSE"))
                insertions.Add((LastTopLevelImport(lines, classLine) ?? -1, new List<string> { "import OpenPushNSE" }));

            var inserted = new List<int>();
            foreach (var insertion in insertions.OrderByDescending(i => i.After))
            {
                lines.InsertRange(insertion.After + 1, insertion.Lines);
                inserted = inserted.Select(i => i > insertion.After ? i + insertion.Lines.Count : i)
                    .Concat(Enumerable.Range(insertion.After + 1, insertion.Lines.Count)).ToList();
            }
            inserted.Sort();
            return new Result { Outcome = Outcome.Patched, Text = string.Join(newline, lines), InsertedLines = inserted };
        }

        /// <summary>The instructions written beside the export when anchors are missing.</summary>
        internal string Instructions(string file, IEnumerable<string> missing) =>
            "# OpenPush could not insert its lines into " + file + " automatically.\n"
          + "# Not found: " + string.Join("; ", missing) + ".\n"
          + "# Add these to the extension class " + _className + " by hand (or keep them in the editor script that\n"
          + "# writes this file), then export again.\n\n"
          + "import OpenPushNSE\n\n"
          + "// In the class body:\n"
          + "private let openPush = OpenPushNotificationServiceHandler(options: {\n"
          + "    var options = OpenPushNotificationServiceHandler.Options()\n"
          + "    options.appGroup = " + SwiftString(_appGroup) + "\n"
          + "    return options\n"
          + "}())\n\n"
          + "// First line of didReceive(_:withContentHandler:):\n"
          + "if openPush.handleIfOpenPush(request, withContentHandler: contentHandler) { return }\n\n"
          + "// First line of serviceExtensionTimeWillExpire():\n"
          + "if openPush.serviceExtensionTimeWillExpireIfHandling() { return }\n";

        /// <summary>For an Objective-C class, which is never edited: a Swift subclass to add by hand.</summary>
        internal string ObjectiveCInstructions(string target, string file, string infoPlist) =>
            "# " + target + "'s principal class " + _className + " (" + file + ") is Objective-C, and OpenPush's\n"
          + "# handler is a Swift API, so OpenPush does not edit it. Add OpenPush beside it with a Swift subclass:\n"
          + "#\n"
          + "# 1. Give " + target + " a bridging header (SWIFT_OBJC_BRIDGING_HEADER) that imports " + _className + "'s header.\n"
          + "# 2. Add this Swift file to " + target + ":\n\n"
          + "import OpenPushNSE\nimport UserNotifications\n\n"
          + "final class OpenPushNotificationService: " + _className + " {\n"
          + "    private let openPush = OpenPushNotificationServiceHandler(options: {\n"
          + "        var options = OpenPushNotificationServiceHandler.Options()\n"
          + "        options.appGroup = " + SwiftString(_appGroup) + "\n"
          + "        return options\n"
          + "    }())\n\n"
          + "    override func didReceive(\n"
          + "        _ request: UNNotificationRequest,\n"
          + "        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void\n"
          + "    ) {\n"
          + "        if openPush.handleIfOpenPush(request, withContentHandler: contentHandler) { return }\n"
          + "        super.didReceive(request, withContentHandler: contentHandler)\n"
          + "    }\n\n"
          + "    override func serviceExtensionTimeWillExpire() {\n"
          + "        if openPush.serviceExtensionTimeWillExpireIfHandling() { return }\n"
          + "        super.serviceExtensionTimeWillExpire()\n"
          + "    }\n"
          + "}\n\n"
          + "# 3. In " + (infoPlist ?? "the extension's Info.plist") + ", set NSExtensionPrincipalClass to\n"
          + "#    $(PRODUCT_MODULE_NAME).OpenPushNotificationService.\n";

        internal static string SwiftString(string value)
            => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        private static Result Missing(string what)
            => new Result { Outcome = Outcome.AnchorsMissing, Missing = new List<string> { what } };

        private bool DeclaresClass(string line)
        {
            var code = StripComment(line);
            return Regex.IsMatch(code, @"(^|\s)class\s+" + Regex.Escape(_className) + @"\b") && code.Trim().EndsWith("{");
        }

        private static (int Function, int Body)? BodyStart(string marker, List<string> lines, int from, int to)
        {
            var function = -1;
            for (var i = from; i < to; i++) if (Regex.IsMatch(StripComment(lines[i]), marker)) { function = i; break; }
            if (function < 0) return null;
            for (var i = function; i < Math.Min(to, function + 12); i++)
            {
                var code = StripComment(lines[i]).Trim();
                if (code.EndsWith("{")) return (function, i);
                if (code.Contains("{")) return null;
            }
            return null;
        }

        /// <summary>The line where the brace opened on <paramref name="start"/> closes, outside strings and comments.</summary>
        private static int ClosingLine(int start, List<string> lines)
        {
            var depth = 0;
            for (var index = start; index < lines.Count; index++)
            {
                var inString = false; var escaped = false; var previous = ' ';
                foreach (var c in lines[index])
                {
                    if (inString)
                    {
                        if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') inString = false;
                    }
                    else if (c == '"') inString = true;
                    else if (c == '/' && previous == '/') break;
                    else if (c == '{') depth++;
                    else if (c == '}') { depth--; if (depth == 0) return index; }
                    previous = c;
                }
            }
            return -1;
        }

        private static int? LastTopLevelImport(List<string> lines, int end)
        {
            var depth = 0; int? last = null;
            for (var i = 0; i < end; i++)
            {
                var code = StripComment(lines[i]).Trim();
                if (code.StartsWith("#if")) depth++;
                else if (code.StartsWith("#endif")) depth = Math.Max(0, depth - 1);
                else if (depth == 0 && Regex.IsMatch(code, @"^(@\w+\s+)*import\s")) last = i;
            }
            return last;
        }

        private static string StripComment(string line)
        {
            var at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 ? line : line.Substring(0, at);
        }

        private static string Indentation(string line) => new string(line.TakeWhile(c => c == ' ' || c == '\t').ToArray());

        private static string IndentUnit(List<string> lines, int classLine, string classIndent)
        {
            for (var i = classLine + 1; i < lines.Count; i++)
            {
                if (lines[i].Trim().Length == 0) continue;
                var indent = Indentation(lines[i]);
                return indent.Length > classIndent.Length ? indent.Substring(classIndent.Length) : "    ";
            }
            return "    ";
        }

        private static string Capture(string pattern, string text)
        {
            var match = Regex.Match(text, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
