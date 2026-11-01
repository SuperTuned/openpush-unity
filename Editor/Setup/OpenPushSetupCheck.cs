using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// One line of the setup checklist: a question about the project that has a
    /// yes/no answer, and — where the editor can act on the answer — a one-click
    /// fix. Both surfaces that show the checklist (the
    /// <see cref="OpenPushSetupWindow"/> and the OpenPush page in Project
    /// Settings) discover subclasses through <see cref="Discover"/>, so adding a
    /// check is one file and no registration.
    /// </summary>
    public abstract class OpenPushSetupCheck
    {
        /// Short name of what is being checked.
        public abstract string Title { get; }

        /// One or two sentences describing the current state and what to do
        /// about it. Re-read after every evaluation, so it may report values.
        public abstract string Detail { get; }

        /// True when the project already satisfies this check.
        public abstract bool Passes();

        /// True to offer a button for this row.
        public virtual bool CanFix => false;

        /// The button's label. "Select" for rows that only reveal the asset.
        public virtual string FixLabel => "Fix";

        /// Runs the button's action. Must be safe to run when the check passes.
        public virtual void Fix() { }

        /// <summary>
        /// True when <see cref="Fix"/> can make the row pass with no one's input, so Run all may call it.
        /// False (the default) for buttons that only reveal something, ask, or need a person's choice.
        /// </summary>
        public virtual bool FixesItself => false;

        /// <summary>
        /// True while the effect of <see cref="Fix"/> is still arriving (a package Unity is resolving and has not
        /// loaded yet). Run all waits instead of judging the rows below.
        /// </summary>
        public virtual bool FixPending => false;

        /// Sort key for the checklist, ascending.
        public virtual int Order => 100;

        /// <summary>
        /// Rows to show in place of this one, one per thing the check found (each with its own buttons),
        /// or null for the usual single row.
        /// </summary>
        public virtual IReadOnlyList<OpenPushSetupItem> Items() => null;

        private static OpenPushSetupCheck[] _cached;

        /// <summary>
        /// Every concrete check, ordered by <see cref="Order"/> and then by type
        /// name so the list is stable. Cached: <c>TypeCache</c> only gains types
        /// through a recompile, which reloads the domain and clears this.
        /// </summary>
        public static IReadOnlyList<OpenPushSetupCheck> Discover() => _cached ??= Build();

        private static OpenPushSetupCheck[] Build()
        {
            var found = new List<OpenPushSetupCheck>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<OpenPushSetupCheck>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;
                try
                {
                    if (Activator.CreateInstance(type) is OpenPushSetupCheck check) found.Add(check);
                }
                catch (Exception e)
                {
                    // A host subclass without a parameterless constructor, or one
                    // that throws in it, must not take the whole checklist down.
                    Debug.LogWarning($"OpenPush: setup check {type.FullName} could not be created: {e.Message}");
                }
            }

            found.Sort((a, b) =>
            {
                var byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder
                    : string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            });
            return found.ToArray();
        }
    }

    /// <summary>One row of a check that shows several (see <see cref="OpenPushSetupCheck.Items"/>).</summary>
    public sealed class OpenPushSetupItem
    {
        public readonly string Title;
        public readonly string Detail;
        public readonly bool Passed;
        /// The row's buttons, left to right.
        public readonly IReadOnlyList<KeyValuePair<string, Action>> Actions;
        /// What Run all does for this row (its default answer), or null when it needs a person.
        public Action RunAll { get; set; }

        public OpenPushSetupItem(string title, string detail, bool passed, params KeyValuePair<string, Action>[] actions)
        {
            Title = title;
            Detail = detail;
            Passed = passed;
            Actions = actions ?? new KeyValuePair<string, Action>[0];
        }

        public static KeyValuePair<string, Action> Button(string label, Action run) => new KeyValuePair<string, Action>(label, run);
    }
}
