using System.Reflection;
using TMPro;
using UnityEngine;

namespace SunkenCryptTimer
{
    /// <summary>
    /// Right-aligned HUD text label showing the nearest tracked feature's reset
    /// countdown, anchored below the top-right corner of the screen (clear of the
    /// top-center event/notice area). Styling comes from the event-name text (font
    /// size and color; the font itself from any actively rendering HUD label, since
    /// the event name's font can be an unloaded asset). Built from scratch as a
    /// direct child of the HUD canvas - never parented into another mod's panel
    /// (v1.1.5 cloned panels' labels as siblings, which scattered stale copies
    /// across the screen when those panels were rebuilt or pooled). Long text
    /// wraps to extra lines inside a width-clamped rect, so it can never run off
    /// the screen. Lazily created and recreated automatically if the HUD is
    /// rebuilt (world transitions).
    /// </summary>
    internal static class HudLabel
    {
        private const string LabelName = "SunkenCryptTimer.HudLabel";

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

            // Font donor: the event-name text only if its font is actually
            // loaded; otherwise any live label on an active canvas (HUD mods
            // can leave the whole vanilla HUD disabled with unloaded fonts).
            // Only the FONT is taken from the donor - the label itself is
            // never cloned or parented into the donor's panel.
            var donor = hud.m_eventName;
            if (!IsUsable(donor.font))
            {
                donor = null;
                foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
                {
                    if (tmp.isActiveAndEnabled && IsUsable(tmp.font) &&
                        tmp.canvas != null && tmp.canvas.isActiveAndEnabled)
                    {
                        donor = tmp;
                        break;
                    }
                }
                if (donor == null)
                {
                    SunkenCryptTimerPlugin.Log.LogWarning("No live HUD label to take a font from yet; retrying next update.");
                    return null; // retried from Show() on later ticks
                }
                SunkenCryptTimerPlugin.Log.LogWarning($"HUD label font taken from '{donor.name}' (canvas '{donor.canvas.name}').");
            }

            // Sweep stale labels from earlier HUD rebuilds so exactly one can
            // ever exist (defense in depth: nothing parents into panels now,
            // but a leftover from a pre-1.1.6 session cannot survive either).
            foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
            {
                if (tmp.name == LabelName)
                {
                    UnityEngine.Object.Destroy(tmp.gameObject);
                }
            }

            // Parent to a live full-screen canvas: the HUD's own when it is
            // active (anchors then mean "top center of the screen"), else the
            // donor's (a canvas with a rendering label on it).
            var canvas = hud.gameObject.activeInHierarchy && hud.m_eventName.canvas != null
                ? hud.m_eventName.canvas
                : donor.canvas;

            // Built from scratch: AddComponent auto-adds the CanvasRenderer
            // TMP requires - nothing to strip (stripping a clone's components
            // produced "Can't remove CanvasRenderer" errors every recreation).
            var go = new GameObject(LabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -SunkenCryptTimerPlugin.CeHudOffsetY.Value);
            // Wide enough for the longest expected line, but clamped to the
            // canvas width; with wrapping on, anything longer breaks to new
            // lines growing downward instead of running off the left edge.
            var canvasRect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(Mathf.Min(700f, canvasRect.rect.width - 40f), 30f);

            _label = go.GetComponent<TextMeshProUGUI>();
            _label.font = donor.font;
            _label.fontSize = donor.fontSize;
            // Fixed style from the event-name element: inheriting the donor's
            // color made the timer take on whatever color the donating panel
            // used (brown clock text, red name text) and change with it.
            _label.color = hud.m_eventName.color;
            _label.alignment = TextAlignmentOptions.Right;
            _label.enableWordWrapping = true;
            _label.overflowMode = TextOverflowModes.Overflow; // wrapped lines grow downward
            _label.raycastTarget = false;

            SunkenCryptTimerPlugin.Log.LogInfo($"HUD label ready on canvas '{canvas.name}' (font from '{donor.name}').");
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
