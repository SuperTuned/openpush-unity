using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenPushFeatureLab
{
    internal sealed class FeatureLabActivityCopy
    {
        public string Title;
        public string PlatformNote;
        public string Summary;
        public string Help;
        public string Capability;
        public string PreviewButton;
        public bool ShowSettings;
    }

    internal sealed class FeatureLabView : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _app;
        private readonly FeatureLabController _controller;
        private readonly Dictionary<string, VisualElement> _elements = new Dictionary<string, VisualElement>();
        private readonly Dictionary<LabTab, VisualElement> _pages = new Dictionary<LabTab, VisualElement>();
        private readonly Dictionary<LabTab, Button> _tabs = new Dictionary<LabTab, Button>();
        private readonly ScrollView _content;
        private readonly Scroller _verticalScroller;
        private readonly VisualElement _scrim;
        private readonly VisualElement _settingsModal;
        private readonly VisualElement _customModal;
        private readonly VisualElement _keyValueModal;
        private readonly VisualElement _previewModal;
        private readonly VisualElement[] _modals;
        private readonly Label[] _modeBadges;
        private readonly SettingsGearIcon _settingsIcon;
        private NotificationDraft _activeDraft;
        private bool _editingAlias;
        private bool _lastSystemDark;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        private LabTab? _selectedTab;
        private int _scrollbarRevealVersion;

        public FeatureLabView(VisualElement root, FeatureLabController controller, Texture2D featureImage)
        {
            _root = root;
            _app = Require<VisualElement>("app");
            _controller = controller;
            _scrim = Require<VisualElement>("modal-scrim");
            _settingsModal = Require<VisualElement>("settings-modal");
            _customModal = Require<VisualElement>("custom-modal");
            _keyValueModal = Require<VisualElement>("key-value-modal");
            _previewModal = Require<VisualElement>("preview-modal");
            _modals = new[] { _settingsModal, _customModal, _keyValueModal, _previewModal };
            _modeBadges = new[]
            {
                Require<Label>("status-mode-badge"),
                Require<Label>("user-mode-badge"),
                Require<Label>("events-mode-badge"),
                Require<Label>("activity-mode-badge")
            };
            _content = Require<ScrollView>("content-scroll");
            _verticalScroller = _content.verticalScroller;
            foreach (LabTab tab in Enum.GetValues(typeof(LabTab)))
            {
                var id = tab.ToString().ToLowerInvariant();
                _pages[tab] = Require<VisualElement>(id + "-page");
                _tabs[tab] = Require<Button>("tab-" + id);
            }

            _settingsIcon = new SettingsGearIcon();
            Require<Button>("settings-button").Add(_settingsIcon);
            BuildNotificationFixtureRows();
            ConfigureRowLabels();
            if (featureImage != null) Require<VisualElement>("preview-image").style.backgroundImage = featureImage;
            BindActions();
            ApplyTypography();
            ConfigureScrollbar();
            _controller.Changed += Refresh;
            SetTab(LabTab.Status);
            RefreshSystemAppearance(true);
            Refresh();
        }

        public void RefreshSystemAppearance(bool force = false)
        {
            var dark = FeatureLabSystemAppearance.IsDark;
            if (!force && dark == _lastSystemDark) return;
            _lastSystemDark = dark;
            ApplyAppearance();
        }

        public void RefreshSafeArea()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2(Screen.width, Screen.height);
            if (safe == _lastSafeArea && screen == _lastScreenSize) return;
            _lastSafeArea = safe;
            _lastScreenSize = screen;
            if (screen.x <= 0 || screen.y <= 0 || float.IsNaN(_app.resolvedStyle.width) || float.IsNaN(_app.resolvedStyle.height)) return;
            var panelHeight = _app.resolvedStyle.height;
            var panelWidth = _app.resolvedStyle.width;
            var topInset = (screen.y - safe.yMax) / screen.y * panelHeight;
            var bottomInset = safe.yMin / screen.y * panelHeight;
            var leftInset = safe.xMin / screen.x * panelWidth;
            var rightInset = (screen.x - safe.xMax) / screen.x * panelWidth;
            var navigationBar = _root.Q<VisualElement>(className: "navigation-bar");
            var tabBar = _root.Q<VisualElement>(className: "tab-bar");
            navigationBar.style.paddingTop = 8 + topInset;
            navigationBar.style.paddingLeft = 16 + leftInset;
            navigationBar.style.paddingRight = 16 + rightInset;
            navigationBar.style.height = 74 + topInset;
            navigationBar.style.minHeight = 74 + topInset;
            tabBar.style.paddingBottom = bottomInset;
            tabBar.style.paddingLeft = leftInset;
            tabBar.style.paddingRight = rightInset;
            tabBar.style.height = 70 + bottomInset;
            tabBar.style.minHeight = 70 + bottomInset;
            _content.style.paddingLeft = leftInset;
            _content.style.paddingRight = rightInset;
        }

        public void Dispose()
        {
            _controller.Changed -= Refresh;
            _verticalScroller.valueChanged -= HandleScrollValueChanged;
            _content.UnregisterCallback<WheelEvent>(HandleScrollWheel);
        }

        private void BindActions()
        {
            foreach (var pair in _tabs)
            {
                var tab = pair.Key;
                pair.Value.clicked += () => SetTab(tab);
            }

            Require<Button>("settings-button").clicked += OpenSettings;
            Require<Button>("settings-close").clicked += CloseModals;
            Require<Button>("mode-demo").clicked += () => _controller.SetMode(LabRuntimeMode.Demo);
            Require<Button>("mode-live").clicked += () => _controller.SetMode(LabRuntimeMode.Live);
            Require<Button>("appearance-system").clicked += () => _controller.SetAppearance(LabAppearance.System);
            Require<Button>("appearance-light").clicked += () => _controller.SetAppearance(LabAppearance.Light);
            Require<Button>("appearance-dark").clicked += () => _controller.SetAppearance(LabAppearance.Dark);
            Require<Button>("save-settings").clicked += () =>
                _controller.SaveConfiguration(Require<TextField>("app-id-field").value, Require<TextField>("sdk-key-field").value);

            Require<Button>("grant-consent-gate").clicked += HandleGateAction;
            Require<Button>("permission-button").clicked += _controller.RequestPermission;
            Require<Toggle>("push-toggle").RegisterValueChangedCallback(evt => _controller.SetPushEnabled(evt.newValue));
            Require<Toggle>("consent-required-toggle").RegisterValueChangedCallback(evt => _controller.SetConsentRequired(evt.newValue));
            Require<Button>("consent-action").clicked += ConsentAction;
            Require<Button>("refresh-button").clicked += _controller.Refresh;

            Require<Button>("login-button").clicked += () => _controller.Login(Require<TextField>("external-id").value);
            Require<Button>("logout-button").clicked += _controller.Logout;
            Require<Button>("add-tag-button").clicked += () => OpenKeyValueEditor(false);
            Require<Button>("add-alias-button").clicked += () => OpenKeyValueEditor(true);
            Require<Button>("key-value-cancel").clicked += CloseModals;
            Require<Button>("key-value-save").clicked += SaveKeyValue;

            Require<Toggle>("suppress-toggle").RegisterValueChangedCallback(evt => _controller.SuppressForegroundPresentation = evt.newValue);
            Require<Button>("event-foreground").clicked += () => _controller.AddSyntheticEvent("foreground");
            Require<Button>("event-click").clicked += () => _controller.AddSyntheticEvent("click");
            Require<Button>("event-permission").clicked += () => _controller.AddSyntheticEvent("permission");
            Require<Button>("event-subscription").clicked += () => _controller.AddSyntheticEvent("subscription");
            Require<Button>("clear-events-button").clicked += _controller.ClearEvents;

            Require<Button>("inapp-fetch").clicked += _controller.FetchInAppMessages;
            Require<Button>("inapp-reset").clicked += _controller.ResetInAppDisplayState;
            Require<Button>("inapp-add-trigger").clicked += () => _controller.AddInAppTrigger(
                Require<TextField>("inapp-trigger-key").value,
                Require<TextField>("inapp-trigger-value").value);

            Require<Button>("preview-live-update").clicked += _controller.PreviewPlatformActivity;
            Require<Button>("open-live-settings").clicked += _controller.OpenPlatformActivitySettings;

            Require<Button>("custom-cancel").clicked += CloseModals;
            Require<Button>("custom-preview").clicked += SaveCustomDraft;
            Require<Button>("preview-close").clicked += CloseModals;
            Require<Button>("preview-forward").clicked += () =>
            {
                _controller.PreviewNotification(_activeDraft);
                Require<Label>("pns-last-request").text = $"Last request · {_activeDraft.Kind} fixture forwarded";
                CloseModals();
            };
            Require<Button>("preview-click").clicked += () =>
            {
                _controller.PreviewClick(_activeDraft);
                CloseModals();
            };
        }

        private void Refresh()
        {
            var snapshot = _controller.Snapshot;
            var modeText = _controller.Mode == LabRuntimeMode.Live ? "LIVE SDK" : "DEMO · SDK WRITES SIMULATED";
            foreach (var badge in _modeBadges) badge.text = modeText;

            var permissionLabel = snapshot.Permission ? "Allowed" : snapshot.PermissionKnown ? "Denied" : "Not determined";
            Require<Label>("metric-permission").text = permissionLabel;
            Require<Label>("metric-push").text = snapshot.OptedIn ? "Opted in" : "Opted out";
            Require<Label>("metric-push-detail").text = $"notification_types {snapshot.NotificationTypes}";
            Require<Label>("metric-identity").text = string.IsNullOrEmpty(snapshot.ExternalId) ? "Anonymous" : snapshot.ExternalId;
            Require<Label>("metric-sdk").text = snapshot.SdkVersion + " · unity " + snapshot.WrapperVersion;
            Require<Label>("metric-sdk-detail").text = _controller.Mode == LabRuntimeMode.Demo ? "simulated service writes" : snapshot.IsInitialized ? "initialized" : "waiting for configuration";
            Require<Label>("authorization-value").text = permissionLabel;
            Require<Toggle>("push-toggle").SetValueWithoutNotify(snapshot.OptedIn);
            var permissionButton = Require<Button>("permission-button");
            permissionButton.text = _controller.Mode == LabRuntimeMode.Live
                ? "Request notification permission"
                : "Simulate notification permission";
            permissionButton.EnableInClassList("hidden", snapshot.Permission);

            Require<Toggle>("consent-required-toggle").SetValueWithoutNotify(_controller.ConsentRequired);
            Require<Label>("consent-value").text = !_controller.ConsentRequired ? "Not required" : _controller.ConsentGranted ? "Granted" : "Waiting";
            Require<Label>("startup-value").text = _controller.Mode == LabRuntimeMode.Demo ? "Demo runtime" : snapshot.IsInitialized ? "Initialized" : "Waiting";
            Require<Button>("consent-action").text = _controller.ConsentGranted ? "Reset saved consent" : "Grant consent & start SDK";
            Require<Button>("consent-action").EnableInClassList("danger-button", _controller.ConsentGranted);
            Require<Button>("consent-action").EnableInClassList("primary-button", !_controller.ConsentGranted);
            Require<Button>("consent-action").EnableInClassList("hidden", !_controller.ConsentRequired);
            Require<Label>("consent-relaunch-note").EnableInClassList("hidden", !_controller.ConsentRequired || !_controller.ConsentGranted || !snapshot.IsInitialized);
            Require<VisualElement>("consent-gate").EnableInClassList("hidden", !_controller.LiveActionsBlocked);
            Require<Label>("gate-title").text = !_controller.LiveConfigured ? "LIVE CONFIGURATION REQUIRED" : _controller.ConsentBlocksStartup ? "SDK WAITING · HOST CONSENT" : "SDK NOT INITIALIZED";
            Require<Label>("gate-message").text = !_controller.LiveConfigured
                ? "Add an App ID and public SDK key before invoking Live SDK actions."
                : _controller.ConsentBlocksStartup
                    ? "Live SDK actions are paused. Grant consent to initialize OpenPush for this process."
                    : "OpenPush did not initialize. Review configuration and the event stream.";
            Require<Button>("grant-consent-gate").text = !_controller.LiveConfigured ? "Open Feature Lab settings" : _controller.ConsentBlocksStartup ? "Grant consent & start SDK" : "Review Feature Lab settings";

            Require<Label>("registration-mode").text = _controller.Mode.ToString().ToUpperInvariant();
            Require<Label>("subscription-id").text = Short(snapshot.SubscriptionId);
            Require<Label>("provider-token").text = Short(snapshot.Token);

            Require<Label>("identity-note").text = string.IsNullOrEmpty(snapshot.ExternalId) ? "ANONYMOUS" : "IDENTIFIED";
            Require<Label>("current-user").text = string.IsNullOrEmpty(snapshot.ExternalId) ? "Anonymous install" : snapshot.ExternalId;
            RenderDataRows(
                Require<VisualElement>("tag-list"),
                snapshot.Tags,
                "No tags in this session. Add one below.",
                _controller.RemoveTag);
            RenderDataRows(
                Require<VisualElement>("alias-list"),
                snapshot.Aliases,
                "No aliases in this session. Add one below.",
                _controller.RemoveAlias);
            RenderEvents();
            RefreshPlatformCopy(snapshot);
            RefreshSettingsControls();
            ApplyAppearance();
        }

        private void RefreshSettingsControls()
        {
            SelectSegment("mode-demo", _controller.Mode == LabRuntimeMode.Demo);
            SelectSegment("mode-live", _controller.Mode == LabRuntimeMode.Live);
            SelectSegment("appearance-system", _controller.Appearance == LabAppearance.System);
            SelectSegment("appearance-light", _controller.Appearance == LabAppearance.Light);
            SelectSegment("appearance-dark", _controller.Appearance == LabAppearance.Dark);
            Require<Label>("settings-sdk-version").text = _controller.Snapshot.SdkVersion + " (native) · " + _controller.Snapshot.WrapperVersion + " (unity)";
        }

        private void RefreshPlatformCopy(LabSnapshot snapshot)
        {
            var copy = ActivityCopy(Application.platform, snapshot.AndroidLiveUpdateCapability);
            Require<Label>("live-update-capability").text = copy.Capability;
            Require<Label>("activity-panel-title").text = copy.Title;
            Require<Label>("activity-platform-note").text = copy.PlatformNote;
            Require<Label>("activity-summary").text = copy.Summary;
            Require<Label>("activity-platform-help").text = copy.Help;
            Require<Button>("preview-live-update").text = copy.PreviewButton;
            Require<Button>("open-live-settings").EnableInClassList("hidden", !copy.ShowSettings);
        }

        internal static FeatureLabActivityCopy ActivityCopy(
            RuntimePlatform platform, string androidCapability)
        {
            if (platform == RuntimePlatform.IPhonePlayer)
                return new FeatureLabActivityCopy
                {
                    Title = "iOS Live Activities",
                    PlatformNote = "ACTIVITYKIT",
                    Summary = "Start a local iOS Live Activity and verify the injected widget before testing remote updates.",
                    Help = "The package injects the default ActivityKit widget during export and forwards its update token to OpenPush.",
                    Capability = "ActivityKit · iOS 16.1+",
                    PreviewButton = "Start iOS Live Activity",
                    ShowSettings = false
                };
            if (platform == RuntimePlatform.Android)
                return new FeatureLabActivityCopy
                {
                    Title = "Android Live Updates",
                    PlatformNote = "NATIVE BRIDGE",
                    Summary = "Exercise the Android Live Update renderer and inspect its current promotion capability.",
                    Help = "Automatic FCM: fill the Firebase fields in OpenPushSettings (google-services.json only works alongside Firebase Unity). Eligible payloads are rendered by the native live-updates module, including while the app is killed.",
                    Capability = androidCapability,
                    PreviewButton = "Run Android Live Update fixture",
                    ShowSettings = true
                };
            return new FeatureLabActivityCopy
            {
                Title = "Live notification preview",
                PlatformNote = "EDITOR PREVIEW",
                Summary = "Preview the shared live-notification layout before testing on an Android or iOS player.",
                Help = "Build for Android to test Live Updates or iOS to start an ActivityKit Live Activity.",
                Capability = "Device build required",
                PreviewButton = "Run preview fixture",
                ShowSettings = false
            };
        }

        private void ApplyAppearance()
        {
            var dark = _controller.Appearance == LabAppearance.Dark ||
                       (_controller.Appearance == LabAppearance.System && _lastSystemDark);
            _app.EnableInClassList("theme-dark", dark);
            _app.EnableInClassList("theme-light", !dark);
            _settingsIcon.MarkDirtyRepaint();
        }

        private void ApplyTypography()
        {
            var bodyNames = Application.platform == RuntimePlatform.Android
                ? new[] { "Roboto", "Noto Sans", "Arial" }
                : new[] { "SF Pro Text", "SF Pro Display", "Helvetica Neue", "Arial" };
            var monoNames = Application.platform == RuntimePlatform.Android
                ? new[] { "Roboto Mono", "Droid Sans Mono", "monospace" }
                : new[] { "SF Mono", "Menlo", "Courier New" };
            var body = Font.CreateDynamicFontFromOSFont(bodyNames, 16);
            if (body != null) _app.style.unityFont = body;

            var mono = Font.CreateDynamicFontFromOSFont(monoNames, 14);
            if (mono == null) return;
            foreach (var className in new[] { "code", "eyebrow", "mode-badge", "support-good", "support-warn" })
                foreach (var label in _root.Query<Label>(className: className).ToList()) label.style.unityFont = mono;
        }

        private void ConfigureScrollbar()
        {
            _verticalScroller.AddToClassList("transient-scrollbar");
            _verticalScroller.lowButton.style.display = DisplayStyle.None;
            _verticalScroller.highButton.style.display = DisplayStyle.None;
            _verticalScroller.valueChanged += HandleScrollValueChanged;
            _content.RegisterCallback<WheelEvent>(HandleScrollWheel);
        }

        private void HandleScrollValueChanged(float _) => RevealScrollbar();

        private void HandleScrollWheel(WheelEvent _) => RevealScrollbar();

        private void RevealScrollbar()
        {
            _verticalScroller.AddToClassList("scrollbar-visible");
            var version = ++_scrollbarRevealVersion;
            _verticalScroller.schedule.Execute(() =>
            {
                if (version == _scrollbarRevealVersion)
                    _verticalScroller.RemoveFromClassList("scrollbar-visible");
            }).StartingIn(700);
        }

        private void SetTab(LabTab selected)
        {
            if (_selectedTab == selected) return;
            foreach (var pair in _pages) pair.Value.EnableInClassList("hidden", pair.Key != selected);
            foreach (var pair in _tabs) pair.Value.EnableInClassList("selected", pair.Key == selected);
            Require<Label>("navigation-title").text = selected.ToString();
            _content.scrollOffset = Vector2.zero;

            var page = _pages[selected];
            if (_selectedTab.HasValue)
            {
                page.AddToClassList("page-entering");
                page.schedule.Execute(() => page.RemoveFromClassList("page-entering")).StartingIn(16);
            }
            _selectedTab = selected;
        }

        private void OpenSettings()
        {
            Require<TextField>("app-id-field").SetValueWithoutNotify(_controller.AppId);
            Require<TextField>("sdk-key-field").SetValueWithoutNotify(_controller.SdkKey);
            ShowModal(_settingsModal);
        }

        private void OpenCustomEditor()
        {
            var draft = FeatureLabController.DefaultDraft("Custom");
            Require<TextField>("custom-title").SetValueWithoutNotify(draft.Title);
            Require<TextField>("custom-description").SetValueWithoutNotify(draft.Description);
            Require<TextField>("custom-key").SetValueWithoutNotify(draft.DataKey);
            Require<TextField>("custom-value").SetValueWithoutNotify(draft.DataValue);
            Require<Label>("custom-validation").EnableInClassList("hidden", true);
            ShowModal(_customModal);
        }

        private void SaveCustomDraft()
        {
            var draft = new NotificationDraft
            {
                Kind = "Custom",
                Title = Require<TextField>("custom-title").value.Trim(),
                Description = Require<TextField>("custom-description").value.Trim(),
                DataKey = Require<TextField>("custom-key").value.Trim(),
                DataValue = Require<TextField>("custom-value").value.Trim()
            };
            var valid = !string.IsNullOrEmpty(draft.Title) && !string.IsNullOrEmpty(draft.Description) &&
                        !string.IsNullOrEmpty(draft.DataKey) && !string.IsNullOrEmpty(draft.DataValue);
            Require<Label>("custom-validation").EnableInClassList("hidden", valid);
            if (valid) OpenPreview(draft);
        }

        private void OpenPreview(NotificationDraft draft)
        {
            _activeDraft = draft;
            Require<Label>("preview-kind").text = draft.Kind.ToUpperInvariant() + " FIXTURE";
            Require<Label>("preview-title").text = draft.Title;
            Require<Label>("preview-description").text = draft.Description;
            Require<Label>("preview-data").text = $"{draft.DataKey} = {draft.DataValue}";
            Require<VisualElement>("preview-image").EnableInClassList("hidden", draft.Kind != "Image");
            Require<Label>("preview-note").text = draft.Kind == "Sound"
                ? "The SDK forwards sound metadata; bundling and playing a notification sound belongs to the native host."
                : draft.Kind == "Button"
                    ? "The SDK forwards action metadata; native notification categories and buttons belong to the host."
                    : "This invokes the SDK foreground listener. Native scheduling and presentation belong to the platform host.";
            ShowModal(_previewModal);
        }

        private void OpenKeyValueEditor(bool alias)
        {
            _editingAlias = alias;
            Require<Label>("key-value-title").text = alias ? "Add Alias" : "Add Tag";
            Require<TextField>("key-field").label = alias ? "LABEL" : "KEY";
            Require<TextField>("value-field").label = alias ? "ID" : "VALUE";
            Require<TextField>("key-field").SetValueWithoutNotify(string.Empty);
            Require<TextField>("value-field").SetValueWithoutNotify(string.Empty);
            Require<Label>("key-value-validation").EnableInClassList("hidden", true);
            ShowModal(_keyValueModal);
        }

        private void SaveKeyValue()
        {
            var key = Require<TextField>("key-field").value;
            var value = Require<TextField>("value-field").value;
            var valid = !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value);
            Require<Label>("key-value-validation").EnableInClassList("hidden", valid);
            if (!valid) return;
            if (_editingAlias) _controller.AddAlias(key, value);
            else _controller.AddTag(key, value);
            CloseModals();
        }

        private static void RenderDataRows(
            VisualElement container,
            IDictionary<string, string> values,
            string emptyText,
            Action<string> remove)
        {
            container.Clear();
            if (values.Count == 0)
            {
                container.Add(CreateEmptyState(emptyText));
                return;
            }

            foreach (var pair in values)
                container.Add(CreateDataRow(pair.Key, pair.Value, remove));
        }

        private void RenderEvents()
        {
            var container = Require<VisualElement>("event-list");
            container.Clear();
            Require<Label>("event-count").text = $"{_controller.Events.Count} RETAINED · NEWEST FIRST";
            Require<Button>("clear-events-button").EnableInClassList("hidden", _controller.Events.Count == 0);
            if (_controller.Events.Count == 0)
            {
                container.Add(CreateEmptyState("No events yet. Run an action or preview an observer event."));
                return;
            }

            foreach (var item in _controller.Events)
                container.Add(CreateEventRow(item));
        }

        private void ConsentAction()
        {
            if (_controller.ConsentGranted) _controller.ResetConsent();
            else _controller.GrantConsent();
        }

        private void HandleGateAction()
        {
            if (!_controller.LiveConfigured || !_controller.ConsentBlocksStartup) OpenSettings();
            else _controller.GrantConsent();
        }

        private void ShowModal(VisualElement modal)
        {
            _scrim.RemoveFromClassList("hidden");
            SetActiveModal(modal);
            ApplyAppearance();
        }

        private void CloseModals()
        {
            _scrim.AddToClassList("hidden");
            SetActiveModal(null);
        }

        private void SetActiveModal(VisualElement active)
        {
            foreach (var modal in _modals) modal.EnableInClassList("hidden", modal != active);
        }

        private void SelectSegment(string name, bool selected) => Require<Button>(name).EnableInClassList("selected", selected);

        private T Require<T>(string name) where T : VisualElement
        {
            VisualElement cached;
            if (_elements.TryGetValue(name, out cached))
            {
                var typed = cached as T;
                if (typed == null) throw new InvalidOperationException($"OpenPush Feature Lab UI '{name}' is not a {typeof(T).Name}.");
                return typed;
            }

            var element = _root.Q<T>(name);
            if (element == null) throw new InvalidOperationException($"OpenPush Feature Lab UI is missing '{name}'.");
            _elements[name] = element;
            return element;
        }

        private void BuildNotificationFixtureRows()
        {
            var container = Require<VisualElement>("pns-fixture-list");
            container.Clear();
            foreach (var fixture in NotificationFixtures)
            {
                var button = new Button
                {
                    name = "pns-" + fixture.Kind.ToLowerInvariant(),
                    tooltip = fixture.Detail
                };
                button.AddToClassList("fixture-button");
                var copy = new VisualElement();
                copy.AddToClassList("fixture-copy");
                copy.Add(CreateLabel(fixture.Kind, "fixture-title"));
                copy.Add(CreateLabel(fixture.Detail, "fixture-detail"));
                button.Add(copy);

                var kind = fixture.Kind;
                if (kind == "Custom") button.clicked += OpenCustomEditor;
                else button.clicked += () => OpenPreview(FeatureLabController.DefaultDraft(kind));
                container.Add(button);
            }
        }

        private void ConfigureRowLabels()
        {
            foreach (var className in new[] { "telemetry-row", "support-row" })
            {
                foreach (var row in _root.Query<VisualElement>(className: className).ToList())
                {
                    var first = true;
                    foreach (var child in row.Children())
                    {
                        var label = child as Label;
                        if (label == null) continue;
                        label.AddToClassList(first ? "row-label" : "row-value");
                        first = false;
                    }
                }
            }
        }

        private static VisualElement CreateDataRow(string key, string value, Action<string> removeAction)
        {
            var row = new VisualElement();
            row.AddToClassList("data-row");
            var copy = new VisualElement();
            copy.AddToClassList("data-copy");
            copy.Add(CreateLabel(key, "data-key"));
            copy.Add(CreateLabel(value, "data-value"));
            var remove = new Button { text = "Remove", tooltip = $"Remove {key}" };
            remove.AddToClassList("remove-button");
            remove.clicked += () => removeAction(key);
            row.Add(copy);
            row.Add(remove);
            return row;
        }

        private static VisualElement CreateEventRow(LabEvent item)
        {
            var row = new VisualElement();
            row.AddToClassList("event-row");
            row.AddToClassList("event-kind-" + item.Kind.ToString().ToLowerInvariant());
            var header = new VisualElement();
            header.AddToClassList("event-header");
            header.Add(CreateLabel(item.Title, "event-title"));
            header.Add(CreateLabel(item.OccurredAt.ToString("HH:mm:ss"), "event-time"));
            row.Add(header);
            row.Add(CreateLabel(item.Detail, "event-detail"));
            return row;
        }

        private static Label CreateEmptyState(string text)
        {
            return CreateLabel(text, "empty-state");
        }

        private static Label CreateLabel(string text, params string[] classNames)
        {
            var label = new Label(text);
            foreach (var className in classNames) label.AddToClassList(className);
            return label;
        }

        private static string Short(string value)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            return value.Length <= 18 ? value : value.Substring(0, 8) + "…" + value.Substring(value.Length - 6);
        }

        private static readonly FixturePresentation[] NotificationFixtures =
        {
            new FixturePresentation("Simple", "Basic title and description"),
            new FixturePresentation("Image", "Rich media preview with bundled artwork"),
            new FixturePresentation("Button", "Action metadata for the native host"),
            new FixturePresentation("Custom", "Edit title, description, and data"),
            new FixturePresentation("Sound", "Custom sound payload metadata")
        };

        private sealed class FixturePresentation
        {
            public FixturePresentation(string kind, string detail)
            {
                Kind = kind;
                Detail = detail;
            }

            public string Kind { get; }
            public string Detail { get; }
        }

        private sealed class SettingsGearIcon : VisualElement
        {
            public SettingsGearIcon()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("settings-gear");
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                var center = contentRect.center;
                const float ringRadius = 6.25f;
                painter.lineWidth = 1.8f;
                painter.lineCap = LineCap.Round;
                painter.strokeColor = resolvedStyle.color;

                painter.BeginPath();
                painter.Arc(center, ringRadius, Angle.Degrees(0), Angle.Degrees(360));
                painter.Stroke();

                painter.BeginPath();
                painter.Arc(center, 2.15f, Angle.Degrees(0), Angle.Degrees(360));
                painter.Stroke();

                painter.BeginPath();
                for (var index = 0; index < 8; index++)
                {
                    var radians = index * Mathf.PI / 4f;
                    var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    painter.MoveTo(center + direction * 8f);
                    painter.LineTo(center + direction * 10.2f);
                }
                painter.Stroke();
            }
        }
    }
}
