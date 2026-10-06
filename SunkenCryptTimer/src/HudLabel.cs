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

            // Self-heal an unusable font: the event-name template's font can
            // be a non-null reference to a not-yet-loaded asset (Unity 6
            // lazy loading) or get assigned after our label was created.
            if (!IsUsable(label.font))
            {
                var hud = HudInstance;
                if (hud != null && hud.m_eventName != null)
                {
                    label.font = ResolveFont(hud, hud.m_eventName);
                }
            }

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

            // Source label: the event-name text only if it is actually being
            // rendered; otherwise any live label on an active canvas (HUD mods
            // can leave the whole vanilla HUD disabled with unloaded fonts).
            var source = hud.m_eventName;
            if (!(source.isActiveAndEnabled && IsUsable(source.font)))
            {
                source = null;
                foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
                {
                    if (tmp.isActiveAndEnabled && IsUsable(tmp.font) &&
                        tmp.canvas != null && tmp.canvas.isActiveAndEnabled)
                    {
                        source = tmp;
                        break;
                    }
                }
                if (source == null)
                {
                    SunkenCryptTimerPlugin.Log.LogWarning("No live HUD label to clone from yet; retrying next update.");
                    return null; // retried from Show() on later ticks
                }
                SunkenCryptTimerPlugin.Log.LogWarning($"HUD label cloned from '{source.name}' (canvas '{source.canvas.name}').");
            }

            // Clone the live label: inherits its font (loaded), material and
            // canvas - built from scratch, a label on a HUD-mod-disabled
            // canvas renders nothing regardless of font.
            var go = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
            go.name = nameof(SunkenCryptTimer);
            foreach (Transform child in go.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            foreach (var comp in go.GetComponents<Component>())
            {
                if (!(comp is Transform) && !(comp is RectTransform) && !(comp is TextMeshProUGUI))
                {
                    UnityEngine.Object.Destroy(comp);
                }
            }

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -SunkenCryptTimerPlugin.CeHudOffsetY.Value);
            rect.sizeDelta = new Vector2(900f, 30f);

            _label = go.GetComponent<TextMeshProUGUI>();
            _label.fontSize = source.fontSize;
            _label.color = source.color;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.raycastTarget = false;

            SunkenCryptTimerPlugin.Log.LogInfo($"HUD label ready (from '{source.name}').");
            return _label;
        }

        /// <summary>
        /// Font with fallbacks. The event-name label's font can be null OR a
        /// non-null reference to an unloaded asset (seen on Unity 6: TMP then
        /// renders nothing and logs "no Font Asset assigned"). Only trust
        /// fonts whose atlas is resident; try other live HUD labels, then
        /// TMP's default asset. Warnings fire once - this re-runs from Show()
        /// until a usable font appears.
        /// </summary>
        private static TMP_FontAsset ResolveFont(Hud hud, TMP_Text template)
        {
            if (IsUsable(template.font))
            {
                return template.font;
            }

            foreach (var tmp in hud.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.isActiveAndEnabled && IsUsable(tmp.font))
                {
                    WarnOnce($"Event font not usable; using font from HUD label '{tmp.name}'.");
                    return tmp.font;
                }
            }

            // HUD mods (e.g. MyLittleUI) can replace the vanilla HUD, leaving
            // its fonts never loaded - scan every ACTIVE text in the scene.
            foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
            {
                if (IsUsable(tmp.font))
                {
                    WarnOnce($"Event font not usable; using font from scene label '{tmp.name}'.");
                    return tmp.font;
                }
            }

            if (IsUsable(TMP_Settings.defaultFontAsset))
            {
                WarnOnce("No usable font in HUD; using TMP_Settings.defaultFontAsset.");
                return TMP_Settings.defaultFontAsset;
            }

            if (!_fontErrored)
            {
                _fontErrored = true;
                SunkenCryptTimerPlugin.Log.LogError("No usable TMP font found yet; will retry each update.");
            }
            return null;
        }

        /// <summary>A font is usable only if an atlas texture is actually loaded.</summary>
        private static bool IsUsable(TMP_FontAsset font)
        {
            return font != null && (font.atlas != null || font.atlasTexture != null);
        }

        private static bool _fontWarned;
        private static bool _fontErrored;

        private static void WarnOnce(string message)
        {
            if (_fontWarned) return;
            _fontWarned = true;
            SunkenCryptTimerPlugin.Log.LogWarning(message);
        }
    }
}
