using System.Reflection;
using TMPro;
using UnityEngine;

namespace SunkenCryptTimer
{
    /// <summary>
    /// Single-line HUD text label showing the dungeon reset countdown, styled after
    /// the game's own HUD text (font, size and color are copied from the event-name
    /// text element so it matches the active UI/skin mods). Lazily created and
    /// recreated automatically if the HUD is rebuilt (world transitions).
    /// </summary>
    internal static class HudLabel
    {
        private static TextMeshProUGUI _label;
        private static readonly FieldInfo HudInstanceField =
            typeof(Hud).GetField("m_instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static Hud HudInstance => HudInstanceField != null ? (Hud)HudInstanceField.GetValue(null) : null;

        internal static void Show(string text)
        {
            var label = GetOrCreate();
            if (label == null) return;
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            label.text = text;
        }

        internal static void Hide()
        {
            if (_label != null && _label.gameObject.activeSelf) _label.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI GetOrCreate()
        {
            if (_label != null) return _label;

            var hud = HudInstance;
            if (hud == null || hud.m_eventName == null) return null;

            var template = hud.m_eventName;
            var go = new GameObject(nameof(SunkenCryptTimer), typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(template.transform.parent, false);
            go.layer = template.gameObject.layer; // UI layer, so the canvas camera renders it

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -SunkenCryptTimerPlugin.CeHudOffsetY.Value);
            rect.sizeDelta = new Vector2(900f, 30f);

            _label = go.GetComponent<TextMeshProUGUI>();
            _label.font = template.font;
            _label.fontSize = template.fontSize;
            _label.color = template.color;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.raycastTarget = false;

            SunkenCryptTimerPlugin.Log.LogDebug("HUD label created.");
            return _label;
        }
    }
}
