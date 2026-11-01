using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace OpenPushSDK.Editor
{
    /// <summary>Reads an XML property list (Info.plist, entitlements) into dictionaries, lists and strings.</summary>
    internal static class OpenPushPlist
    {
        /// <summary>The root dictionary, or null when the file is missing or not an XML plist.</summary>
        internal static Dictionary<string, object> Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreWhitespace = true };
                using (var reader = XmlReader.Create(path, settings))
                    return Value(XDocument.Load(reader).Root?.Elements().FirstOrDefault()) as Dictionary<string, object>;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object Value(XElement element)
        {
            if (element == null) return null;
            switch (element.Name.LocalName)
            {
                case "dict":
                    var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    var children = element.Elements().ToList();
                    for (var i = 0; i + 1 < children.Count; i += 2)
                        if (children[i].Name.LocalName == "key") map[children[i].Value] = Value(children[i + 1]);
                    return map;
                case "array": return element.Elements().Select(Value).ToList();
                case "true": return true;
                case "false": return false;
                default: return element.Value;
            }
        }

        internal static string String(Dictionary<string, object> dict, params string[] path)
        {
            object current = dict;
            foreach (var key in path)
                current = current is Dictionary<string, object> map && map.TryGetValue(key, out var next) ? next : null;
            return current as string;
        }

        internal static List<string> Strings(Dictionary<string, object> dict, string key)
            => dict != null && dict.TryGetValue(key, out var value) && value is List<object> list
                ? list.OfType<string>().ToList() : new List<string>();
    }
}
