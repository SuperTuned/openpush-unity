using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushSetupRefreshTests
    {
        /// <summary>A row over a stamped project scan, like the existing-setup and previous-SDK rows.</summary>
        private sealed class ScanCheck : OpenPushSetupCheck
        {
            private readonly OpenPushStampedCache<bool> _cache = new OpenPushStampedCache<bool>();
            public bool OnDisk;
            public int Scans;
            public bool Scanned => _cache.Get(() => { Scans++; return OnDisk; });
            public override string Title => "scan";
            public override string Detail => Scanned ? "found" : "missing";
            public override bool Passes() => Scanned;
            public override bool CanFix => !Passes();
            public override bool FixesItself => true;
            // Writes a file the asset database has not imported yet: only a rescan sees it.
            public override void Fix() => OnDisk = true;
        }

        /// <summary>A row that reads a settings value and caches a scan keyed on it, like the icons row.</summary>
        private sealed class SettingsCheck : OpenPushSetupCheck
        {
            private readonly OpenPushStampedCache<bool> _cache = new OpenPushStampedCache<bool>();
            public int Value;
            public int Scans;
            public override string Title => "settings";
            public override string Detail => "value " + Value;
            public override bool Passes() => _cache.Get(() => { Scans++; return Value > 0; }, Value);
        }

        /// <summary>
        /// What OpenPushSetupRows.Draw does on a layout event: evaluate when nothing was evaluated yet or the
        /// refresh says so, keyed on the change stamp and (standing in for the settings dirty count) a counter.
        /// </summary>
        private sealed class Surface
        {
            public readonly OpenPushSetupRefresh Refresh = new OpenPushSetupRefresh();
            public int SettingsDirty;
            public int Evaluations;
            public List<OpenPushRunRow> Rows;

            public void Layout(IReadOnlyList<OpenPushSetupCheck> checks)
            {
                var key = (OpenPushProjectChangeStamp.Stamp, SettingsDirty);
                if (Rows != null && !Refresh.Due(key)) return;
                Rows = OpenPushSetupEvaluation.Collect(checks);
                Evaluations++;
                Refresh.Evaluated(key);
            }
        }

        [TearDown]
        public void Reset()
        {
            OpenPushSetupAssets.StandIn = null;
            UnityEditor.AssetDatabase.Searches.Clear();
        }

        [Test]
        public void IdleLayoutsReuseTheRowsWithNoTimer()
        {
            var scan = new ScanCheck();
            var surface = new Surface();
            for (var i = 0; i < 5; i++) surface.Layout(new[] { scan });
            Assert.AreEqual(1, surface.Evaluations, "mouse-move repaints re-run nothing");
            Assert.AreEqual(1, scan.Scans);
        }

        [Test]
        public void FocusReasksEveryRowButKeepsTheStampedScan()
        {
            var scan = new ScanCheck();
            var surface = new Surface();
            surface.Layout(new[] { scan });

            surface.Refresh.Reevaluate(); // OnFocus
            surface.Layout(new[] { scan });

            Assert.AreEqual(2, surface.Evaluations, "the rows were asked again");
            Assert.AreEqual(1, scan.Scans, "focus does not move the stamp, so the project is not scanned again");
        }

        [Test]
        public void ReCheckRescansTheProject()
        {
            var scan = new ScanCheck();
            var surface = new Surface();
            surface.Layout(new[] { scan });
            scan.OnDisk = true;

            surface.Refresh.Rescan(); // Re-check, or after a Fix
            surface.Layout(new[] { scan });

            Assert.AreEqual(2, scan.Scans);
            Assert.IsTrue(surface.Rows[0].Passed);
        }

        [Test]
        public void AnAssetChangeReevaluatesAndRescansWithoutBeingAsked()
        {
            var scan = new ScanCheck();
            var surface = new Surface();
            surface.Layout(new[] { scan });
            scan.OnDisk = true;

            OpenPushProjectChangeStamp.Bump(); // OnPostprocessAllAssets
            surface.Layout(new[] { scan });

            Assert.AreEqual(2, surface.Evaluations);
            Assert.AreEqual(2, scan.Scans);
            Assert.IsTrue(surface.Rows[0].Passed);
        }

        [Test]
        public void ASettingsEditReevaluatesTheRowsThatReadItAndNoOtherScan()
        {
            var scan = new ScanCheck();
            var settings = new SettingsCheck();
            var surface = new Surface();
            surface.Layout(new OpenPushSetupCheck[] { scan, settings });
            Assert.IsFalse(surface.Rows[1].Passed);

            settings.Value = 1;
            surface.SettingsDirty++; // the inspector or Project Settings applied an edit
            surface.Layout(new OpenPushSetupCheck[] { scan, settings });

            Assert.AreEqual(2, surface.Evaluations);
            Assert.IsTrue(surface.Rows[1].Passed, "the row shows the edit at the next layout");
            Assert.AreEqual("value 1", surface.Rows[1].Detail);
            Assert.AreEqual(2, settings.Scans, "keyed on the value it reads");
            Assert.AreEqual(1, scan.Scans, "a scan that does not read the settings is kept");
        }

        [Test]
        public void NotificationIconsSearchOncePerEvaluationAndOnlyAgainAfterAChange()
        {
            OpenPushSetupAssets.StandIn = new OpenPushSettings { AndroidPushDelivery = OpenPushAndroidPushDelivery.Automatic };
            OpenPushProjectChangeStamp.Bump(); // clear what an earlier test cached
            var checks = new OpenPushSetupCheck[] { new NotificationIconsPresent() };
            var searches = UnityEditor.AssetDatabase.Searches;

            var rows = OpenPushSetupEvaluation.Collect(checks);
            Assert.AreEqual(4, searches.Count, "one search per icon name for Passes and Detail together");
            Assert.IsFalse(rows[0].Passed);
            StringAssert.Contains(OpenPushNotificationIcons.StatusIcon, rows[0].Detail);

            OpenPushSetupEvaluation.Collect(checks);
            Assert.AreEqual(4, searches.Count, "re-asking the row (focus) does not search again");

            OpenPushProjectChangeStamp.Bump();
            OpenPushSetupEvaluation.Collect(checks);
            Assert.AreEqual(8, searches.Count, "an asset change searches again");

            OpenPushSetupAssets.StandIn.IconsSource = OpenPushPlumbingSource.Reused;
            OpenPushSetupAssets.StandIn.AndroidNotificationIconName = "ic_stat_game";
            OpenPushSetupEvaluation.Collect(checks);
            Assert.AreEqual(13, searches.Count, "a settings edit it reads searches again, now for the reused icon too");
            Assert.AreEqual("ic_stat_game", searches.Last());

            OpenPushSetupAssets.StandIn.AndroidPushDelivery = OpenPushAndroidPushDelivery.Forward;
            rows = OpenPushSetupEvaluation.Collect(checks);
            Assert.IsTrue(rows[0].Passed, "Forward: the host's service owns the icons");
        }

        [Test]
        public void RunAllReevaluatesAndRescansBetweenFixes()
        {
            // The second row only passes once the first row's fix is on disk and scanned.
            var first = new ScanCheck();
            var second = new SecondCheck(first);
            var checks = new OpenPushSetupCheck[] { first, second };
            var refresh = new OpenPushSetupRefresh();
            var fixedRows = new List<string>();
            var evaluations = 0;

            // The batch entry point: rescan, then evaluate, before every step.
            var end = OpenPushRunAllPlanner.RunToEnd(() =>
            {
                refresh.Rescan();
                evaluations++;
                return OpenPushSetupEvaluation.Collect(checks);
            }, fixedRows);

            Assert.AreEqual(OpenPushRunStep.Done, end.Step, end.Reason);
            CollectionAssert.AreEqual(new[] { "scan", "second" }, fixedRows);
            Assert.AreEqual(3, evaluations, "every row re-read before each step");
            Assert.AreEqual(3, first.Scans, "and every stamped scan with it");
        }

        private sealed class SecondCheck : OpenPushSetupCheck
        {
            private readonly ScanCheck _first;
            private bool _done;
            public SecondCheck(ScanCheck first) { _first = first; }
            public override string Title => "second";
            public override string Detail => "needs scan";
            public override bool Passes() => _done;
            public override bool CanFix => _first.Scanned;
            public override bool FixesItself => true;
            public override void Fix() => _done = true;
        }
    }
}
