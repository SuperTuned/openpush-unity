using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OpenPushSDK.Core;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// EDM4U's resolver through reflection: its DLLs are loaded by its version handler and cannot be
    /// referenced at compile time (PlayServicesResolver.ResolveSync(bool) → true).
    /// </summary>
    internal static class OpenPushEdm4u
    {
        private static Type Resolver => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GooglePlayServices.PlayServicesResolver", false))
            .FirstOrDefault(t => t != null);

        internal static bool Loaded => Resolver != null;

        internal static bool ForceResolve()
        {
            var method = Resolver?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
            if (method == null) { Debug.LogError("OpenPush: EDM4U (com.google.external-dependency-manager) is not loaded."); return false; }
            // EDM4U copies Unity's Gradle templates into Assets/Plugins/Android and fails when the folder is missing.
            Directory.CreateDirectory("Assets/Plugins/Android");
            var ok = method.Invoke(null, new object[] { true }) is bool b && b;
            if (ok) RecordResolve();
            return ok;
        }

        internal static void RecordResolve()
        {
            Directory.CreateDirectory("Library");
            File.WriteAllText(OpenPushResolveState.StatePath, OpenPushResolveState.Serialize(Current()));
        }

        internal static OpenPushResolveRecord Last()
            => File.Exists(OpenPushResolveState.StatePath) ? OpenPushResolveState.Parse(File.ReadAllText(OpenPushResolveState.StatePath)) : null;

        internal static OpenPushResolveRecord Current()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(OpenPushEdm4u).Assembly);
            var deps = File.Exists(OpenPushAndroidDependencies.OutputPath) ? File.ReadAllText(OpenPushAndroidDependencies.OutputPath) : "";
            using (var sha = SHA256.Create())
            {
                var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(deps))).Replace("-", "").ToLowerInvariant();
                return new OpenPushResolveRecord
                {
                    PackageVersion = info?.version ?? OpenPushVersion.Current,
                    PackagePath = info?.resolvedPath ?? "Packages/ai.openpush.unity",
                    DependenciesHash = hash,
                };
            }
        }

        /// <summary>After a package upgrade or a delivery-mode change, re-resolve once (spec §5.1).</summary>
        [InitializeOnLoadMethod]
        private static void ResolveAfterUpgrade()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || !Loaded) return;
                OpenPushAndroidDependencies.WriteNow();
                if (OpenPushResolveState.NeedsForcedResolve(Last(), Current()))
                {
                    Debug.Log("OpenPush: package or dependency change detected — forcing an EDM4U resolve.");
                    ForceResolve();
                }
            };
        }
    }
}
