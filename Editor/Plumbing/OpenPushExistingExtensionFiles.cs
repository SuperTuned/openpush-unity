using System.IO;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The source edits TakeOver and Share make to an existing notification service extension. Swift only:
    /// an Objective-C class is never edited. Every file is backed up once, to
    /// <c>.openpush-backup/</c> at the export root (outside any target or synchronized folder), before
    /// its first change.
    /// </summary>
    internal static class OpenPushExistingExtensionFiles
    {
        internal const string BackupFolder = ".openpush-backup";
        internal const string PatchFile = ".openpush/share.patch";
        internal const string TakeOverMarker = "// OpenPush took over this notification service extension";

        internal sealed class Outcome
        {
            public bool Changed;
            /// <summary>Why nothing could be done; the build stops with it. Null on success.</summary>
            public string Problem;
            public string Note;
        }

        /// <summary>The principal class rewritten as OpenPush's handler, keeping its name.</summary>
        internal static string TakeOverSource(string className, string appGroup, bool objcName) =>
            TakeOverMarker + " (TakeOver). The source it replaced is in " + BackupFolder + "/.\n"
          + "import OpenPushNSE\n"
          + "import UserNotifications\n\n"
          + (objcName ? "@objc(" + className + ")\n" : "")
          + "final class " + className + ": UNNotificationServiceExtension {\n"
          + "    private let openPush = OpenPushNotificationServiceHandler(options: {\n"
          + "        var options = OpenPushNotificationServiceHandler.Options()\n"
          + "        options.appGroup = " + OpenPushSharePatcher.SwiftString(appGroup) + "\n"
          + "        return options\n"
          + "    }())\n\n"
          + "    override func didReceive(\n"
          + "        _ request: UNNotificationRequest,\n"
          + "        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void\n"
          + "    ) {\n"
          + "        openPush.didReceive(request, withContentHandler: contentHandler)\n"
          + "    }\n\n"
          + "    override func serviceExtensionTimeWillExpire() {\n"
          + "        openPush.serviceExtensionTimeWillExpire()\n"
          + "    }\n"
          + "}\n";

        internal static Outcome TakeOver(string root, OpenPushXcodeExtension extension, string appGroup)
        {
            if (extension.PrincipalFile == null)
                return new Outcome { Problem = "OpenPush could not find the file declaring " + (extension.PrincipalClass ?? "the principal class")
                    + " of " + extension.Target + " (searched beside " + (extension.InfoPlist ?? "its Info.plist") + ")." };
            if (extension.Language != "swift")
                return new Outcome { Problem = extension.Target + "'s principal class is Objective-C (" + extension.PrincipalFile
                    + "), which OpenPush never rewrites. Choose Share for the lines to add by hand, or Off." };
            var path = Path.Combine(root, extension.PrincipalFile);
            var current = File.ReadAllText(path);
            var replacement = TakeOverSource(extension.ClassName, appGroup, !extension.PrincipalClass.Contains("."));
            if (current == replacement) return new Outcome();
            // Hand-written OpenPush code (not ours to regenerate) is left as it is.
            if (!current.Contains(TakeOverMarker) && current.Contains("OpenPushNotificationServiceHandler"))
                return new Outcome { Note = extension.PrincipalFile + " already uses OpenPush's handler; left as it is." };
            Backup(root, extension.PrincipalFile);
            File.WriteAllText(path, replacement);
            return new Outcome { Changed = true, Note = extension.PrincipalFile + ": " + extension.ClassName + " is now OpenPush's handler (the original is in " + BackupFolder + "/)." };
        }

        internal static Outcome Share(string root, OpenPushXcodeExtension extension, string appGroup)
        {
            var patcher = new OpenPushSharePatcher(extension.ClassName ?? "NotificationService", appGroup);
            var patchPath = Path.Combine(root, PatchFile);
            if (extension.PrincipalFile == null)
                return new Outcome { Problem = "OpenPush could not find the file declaring " + (extension.PrincipalClass ?? "the principal class")
                    + " of " + extension.Target + ", so it cannot add its lines. Turn the extension Off, or add the lines by hand." };
            if (extension.Language != "swift")
            {
                WritePatch(patchPath, patcher.ObjectiveCInstructions(extension.Target, extension.PrincipalFile, extension.InfoPlist));
                return new Outcome { Problem = extension.Target + "'s principal class is Objective-C (" + extension.PrincipalFile
                    + "), which OpenPush never edits. The lines to add are in " + patchPath + "." };
            }
            var path = Path.Combine(root, extension.PrincipalFile);
            var result = patcher.Patch(File.ReadAllText(path));
            switch (result.Outcome)
            {
                case OpenPushSharePatcher.Outcome.AlreadyShared:
                    return new Outcome();
                case OpenPushSharePatcher.Outcome.Patched:
                    Backup(root, extension.PrincipalFile);
                    File.WriteAllText(path, result.Text);
                    return new Outcome { Changed = true, Note = extension.PrincipalFile + ": OpenPush handles its own pushes first ("
                        + result.InsertedLines.Count + " lines added)." };
                default:
                    WritePatch(patchPath, patcher.Instructions(extension.PrincipalFile, result.Missing));
                    return new Outcome { Problem = "OpenPush could not add its lines to " + extension.PrincipalFile + " (not found: "
                        + string.Join("; ", result.Missing) + "). The lines to add by hand are in " + patchPath + "." };
            }
        }

        /// <summary>
        /// The Podfile lines that add pods to <paramref name="target"/> (its <c>target '…' do … end</c> block), for
        /// the build log; null when there are none. The Podfile itself is never edited.
        /// </summary>
        internal static string PodfileTargetBlock(string podfile, string target)
        {
            var lines = (podfile ?? "").Replace("\r\n", "\n").Split('\n');
            var start = System.Array.FindIndex(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l,
                @"^\s*target\s+['""]" + System.Text.RegularExpressions.Regex.Escape(target) + @"['""]\s+do\b"));
            if (start < 0) return null;
            var depth = 0;
            for (var i = start; i < lines.Length; i++)
            {
                var code = lines[i].Split('#')[0];
                if (System.Text.RegularExpressions.Regex.IsMatch(code, @"\bdo\b(\s*\|[^|]*\|)?\s*$")) depth++;
                if (System.Text.RegularExpressions.Regex.IsMatch(code, @"^\s*end\b")) depth--;
                if (depth == 0) return string.Join("\n", lines, start, i - start + 1);
            }
            return string.Join("\n", lines, start, lines.Length - start);
        }

        /// <summary>Copies the file to the backup folder unless an earlier export already saved the original.</summary>
        internal static void Backup(string root, string relative)
        {
            var target = Path.Combine(root, BackupFolder, relative);
            if (File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(Path.Combine(root, relative), target);
        }

        private static void WritePatch(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}
