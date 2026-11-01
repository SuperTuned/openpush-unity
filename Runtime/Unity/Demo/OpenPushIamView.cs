using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenPushSDK
{
    internal sealed class OpenPushIamView
    {
        internal readonly VisualElement Root = new VisualElement();
        private readonly Action<OpenPushInAppClick> _action;
        private readonly Action<string, Action<Texture2D>> _loadImage;
        private readonly VisualElement _surface = new VisualElement();
        private readonly VisualElement _cardHost = new VisualElement();
        private readonly List<Dictionary<string, object>> _cards = new List<Dictionary<string, object>>();
        private int _cardIndex;

        internal OpenPushIamView(OpenPushInAppMessage message,
                                Action<OpenPushInAppClick> action,
                                Action<string, Action<Texture2D>> loadImage)
        {
            _action = action; _loadImage = loadImage;
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.right = Root.style.top = Root.style.bottom = 0;
            Root.style.backgroundColor = new Color(0, 0, 0, .45f);
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.justifyContent = message.Layout == "top" ? Justify.FlexStart
                : message.Layout == "bottom" ? Justify.FlexEnd : Justify.Center;

            _surface.style.position = Position.Relative;
            _surface.style.backgroundColor = Color.white;
            _surface.style.marginLeft = _surface.style.marginRight = 24;
            _surface.style.maxWidth = 430;
            if (message.Layout == "full" || message.Layout == "carousel")
            {
                _surface.style.flexGrow = 1; _surface.style.alignSelf = Align.Stretch;
                _surface.style.maxWidth = StyleKeyword.None;
                _surface.style.marginLeft = _surface.style.marginRight = 0;
            }
            else _surface.style.alignSelf = Align.Center;
            Root.Add(_surface);

            var blocks = message.Blocks as Dictionary<string, object>;
            var background = Map(blocks, "background");
            _surface.style.backgroundColor = ColorOf(Text(background, "color", "#ffffff"), Color.white);
            var padding = Number(background, "padding", 24);
            _surface.style.paddingLeft = _surface.style.paddingRight = padding;
            _surface.style.paddingTop = _surface.style.paddingBottom = padding;
            var backgroundImage = Text(background, "image");
            if (!string.IsNullOrEmpty(backgroundImage))
                _loadImage(backgroundImage, texture => _surface.style.backgroundImage = new StyleBackground(texture));
            var backgroundClick = Map(background, "click");
            if (backgroundClick != null)
                _surface.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _surface)
                        Emit(Click(backgroundClick, null, Bool(background, "dismiss_on_click")));
                });

            var rawCards = List(blocks, "cards");
            if (rawCards != null) foreach (var card in rawCards)
                if (card is Dictionary<string, object> map) _cards.Add(map);

            _surface.Add(_cardHost);
            AddClose(Map(blocks, "close"));
            RenderCard();
        }

        private void AddClose(Dictionary<string, object> close)
        {
            if (close == null || !Bool(close, "enabled", true)) return;
            var button = new Button(() => Emit(Click(Map(close, "click"), null, true)))
                { text = "×" };
            button.tooltip = "Close";
            button.style.position = Position.Absolute; button.style.right = 10; button.style.top = 8;
            button.style.width = Math.Max(28, Number(close, "w", 10) + 18);
            button.style.height = Math.Max(28, Number(close, "h", 10) + 18);
            button.style.backgroundColor = Color.clear;
            button.style.borderBottomWidth = button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = button.style.borderTopWidth = 0;
            button.style.fontSize = 22;
            _surface.Add(button);
        }

        private void RenderCard()
        {
            _cardHost.Clear();
            if (_cards.Count == 0) return;
            var blocks = List(_cards[Math.Max(0, Math.Min(_cardIndex, _cards.Count - 1))], "blocks");
            if (blocks != null) foreach (var raw in blocks)
            {
                if (!(raw is Dictionary<string, object> block)) continue;
                var kind = Text(block, "kind");
                if (kind == "text") AddText(block);
                else if (kind == "image") AddImage(block);
                else if (kind == "button") AddButton(block);
            }
            if (_cards.Count > 1) AddCarouselControls();
        }

        private void AddText(Dictionary<string, object> block)
        {
            var label = new Label(Text(block, "text"));
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = Number(block, "size", 16);
            label.style.color = ColorOf(Text(block, "color", "#0a0a0a"), Color.black);
            label.style.unityTextAlign = Alignment(block);
            if (Bool(block, "bold")) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            // A text block can carry a click too — the composer lets any block
            // have one — and the receipt is keyed by the block's own key, so it
            // registers exactly like an image does.
            var click = Map(block, "click");
            if (click != null) label.RegisterCallback<ClickEvent>(_ =>
                Emit(Click(click, Text(block, "key"), Bool(block, "dismiss_on_click"))));
            ApplyMargins(label, block); _cardHost.Add(label);
        }

        private void AddImage(Dictionary<string, object> block)
        {
            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.style.minHeight = 120; image.style.maxHeight = 360;
            ApplyMargins(image, block);
            var url = Text(block, "url");
            if (!string.IsNullOrEmpty(url)) _loadImage(url, texture => image.image = texture);
            var click = Map(block, "click");
            if (click != null) image.RegisterCallback<ClickEvent>(_ =>
                Emit(Click(click, Text(block, "key"), Bool(block, "dismiss_on_click"))));
            _cardHost.Add(image);
        }

        private void AddButton(Dictionary<string, object> block)
        {
            var click = Map(block, "click");
            var button = new Button(() => Emit(Click(click, Text(block, "key"),
                                                        Bool(block, "dismiss_on_click", true))))
                { text = Text(block, "text", "Continue") };
            button.style.fontSize = Number(block, "size", 16);
            button.style.color = ColorOf(Text(block, "color", "#ffffff"), Color.white);
            button.style.backgroundColor = ColorOf(Text(block, "bg", "#0a0a0a"), Color.black);
            button.style.unityTextAlign = Alignment(block);
            button.style.height = 48;
            var radius = Number(block, "radius", 0);
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = radius;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = radius;
            var border = Number(block, "border_w", 0);
            var borderColor = ColorOf(Text(block, "border_color", "#0a0a0a"), Color.black);
            button.style.borderBottomWidth = button.style.borderLeftWidth = border;
            button.style.borderRightWidth = button.style.borderTopWidth = border;
            button.style.borderBottomColor = button.style.borderLeftColor = borderColor;
            button.style.borderRightColor = button.style.borderTopColor = borderColor;
            if (Bool(block, "bold", true)) button.style.unityFontStyleAndWeight = FontStyle.Bold;
            ApplyMargins(button, block); _cardHost.Add(button);
        }

        private void AddCarouselControls()
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween; row.style.marginTop = 14;
            var previous = new Button(() => { _cardIndex--; RenderCard(); }) { text = "Previous" };
            previous.SetEnabled(_cardIndex > 0);
            var count = new Label($"{_cardIndex + 1} / {_cards.Count}");
            count.style.unityTextAlign = TextAnchor.MiddleCenter;
            var next = new Button(() => { _cardIndex++; RenderCard(); }) { text = "Next" };
            next.SetEnabled(_cardIndex < _cards.Count - 1);
            row.Add(previous); row.Add(count); row.Add(next); _cardHost.Add(row);
        }

        private OpenPushInAppClick Click(Dictionary<string, object> action, string blockKey,
                                         bool closes)
        {
            var kind = Text(action, "kind");
            IReadOnlyDictionary<string, string> tags = null;
            var tagKey = Text(action, "tag_key");
            if (!string.IsNullOrEmpty(tagKey))
                tags = new Dictionary<string, string> { [tagKey] = Text(action, "tag_value", "") };
            return new OpenPushInAppClick
            {
                ClickId = Text(action, "click_id"), BlockKey = blockKey,
                // The kind rides in Kind, the way the native presenters report
                // it. ActionId is the custom action's own name and nothing
                // else: putting the kind here made a plain url action look to
                // the host like a custom action named "url".
                // The composer leaves the kind empty for an action that only
                // records a click, and the close button has no action at all.
                // The parser reports those as "click" and "close", and the same
                // campaign must not describe itself differently in the Editor.
                Kind = !string.IsNullOrEmpty(kind) ? kind : (closes ? "close" : "click"),
                ActionId = kind == "custom" ? Text(action, "action_id") : null,
                Url = Text(action, "url"), UrlTarget = Text(action, "url_target"),
                Outcome = Text(action, "outcome"), Tags = tags,
                Prompt = kind == "prompt_push" ? "push" : kind == "prompt_location" ? "location" : null,
                Closes = closes
            };
        }

        // This view never opens URLs itself: the click goes to the caller, which
        // raises it through InAppMessages.Clicked. On a device the native SDK
        // presents messages and handles their clicks.
        private void Emit(OpenPushInAppClick click) => _action(click);

        private static void ApplyMargins(VisualElement element, Dictionary<string, object> block)
        {
            var values = List(block, "margins");
            if (values == null || values.Count != 4) return;
            element.style.marginTop = Convert.ToSingle(values[0]);
            element.style.marginRight = Convert.ToSingle(values[1]);
            element.style.marginBottom = Convert.ToSingle(values[2]);
            element.style.marginLeft = Convert.ToSingle(values[3]);
        }

        private static TextAnchor Alignment(Dictionary<string, object> block)
        {
            var align = Text(block, "align", "center");
            return align == "left" ? TextAnchor.MiddleLeft
                : align == "right" ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
        }

        private static Color ColorOf(string value, Color fallback)
            => ColorUtility.TryParseHtmlString(value, out var color) ? color : fallback;
        private static Dictionary<string, object> Map(Dictionary<string, object> map, string key)
            => map != null && map.TryGetValue(key, out var value)
                ? value as Dictionary<string, object> : null;
        private static List<object> List(Dictionary<string, object> map, string key)
            => map != null && map.TryGetValue(key, out var value) ? value as List<object> : null;
        private static string Text(Dictionary<string, object> map, string key, string fallback = null)
            => map != null && map.TryGetValue(key, out var value) && value is string text
                ? text : fallback;
        private static float Number(Dictionary<string, object> map, string key, float fallback = 0)
            => map != null && map.TryGetValue(key, out var value) && value != null
                ? Convert.ToSingle(value) : fallback;
        private static bool Bool(Dictionary<string, object> map, string key, bool fallback = false)
            => map != null && map.TryGetValue(key, out var value) && value is bool result
                ? result : fallback;
    }
}
