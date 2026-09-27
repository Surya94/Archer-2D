using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Archer.Editor
{
    /// <summary>
    /// Palette and widget factory shared by the UI builders. Colours are picked to sit with the
    /// game's cartoon parallax/balloon art: warm cream cards, saturated primaries, and dark
    /// brown label text on every fill so contrast stays comfortably readable rather than the
    /// low-contrast white-on-pastel that saturated buttons usually invite.
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color Dim = new Color32(0x0A, 0x1A, 0x2F, 0x9E); // deep navy scrim, ~62%
        public static readonly Color Card = new Color32(0xFF, 0xF4, 0xDC, 0xFF); // warm cream
        public static readonly Color CardShadow = new Color32(0x4A, 0x3B, 0x2A, 0x6E);

        public static readonly Color TitleRed = new Color32(0xE8, 0x54, 0x3F, 0xFF);
        public static readonly Color LabelBrown = new Color32(0x6B, 0x5B, 0x45, 0xFF);
        public static readonly Color ValueSlate = new Color32(0x2E, 0x40, 0x57, 0xFF);
        public static readonly Color ButtonInk = new Color32(0x3A, 0x2A, 0x18, 0xFF);

        public static readonly Color Gold = new Color32(0xFF, 0xC5, 0x3D, 0xFF); // reward
        public static readonly Color Green = new Color32(0x7E, 0xD9, 0x57, 0xFF); // go / restart
        public static readonly Color Sky = new Color32(0x6E, 0xC6, 0xF5, 0xFF); // neutral / home
        public static readonly Color Disabled = new Color32(0xD8, 0xD2, 0xC4, 0xFF);

        // Reference-resolution pixels. The canvas reference height is 1080, so 110px is roughly
        // 9mm on a phone - comfortably above the ~48dp minimum touch target.
        public const float ButtonHeight = 120f;
        public const float AdButtonHeight = 140f;
        public const float CardWidth = 900f;

        /// <summary>
        /// Unity's built-in rounded 9-slice UI sprite. It lives in the "builtin extra" resource
        /// file, not the plain builtin one, so Resources.GetBuiltinResource cannot see it and
        /// logs an assert - AssetDatabase.GetBuiltinExtraResource is the correct lookup.
        /// </summary>
        public static Sprite RoundedSprite
        {
            get
            {
                Sprite sprite = UnityEditor.AssetDatabase
                    .GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                if (sprite == null)
                    Debug.LogWarning("[UIStyle] Built-in UISprite not found; panels will render as plain rectangles.");

                return sprite;
            }
        }

        public static TMP_FontAsset Font
        {
            get
            {
                if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
                return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor to all four edges of the parent with zero inset - survives any aspect ratio.</summary>
        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchor to the parent's centre so the element stays centred at every resolution.</summary>
        public static RectTransform Centre(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string content,
            float size, Color colour, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = CreateRect(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = colour;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, Color fill,
            out TextMeshProUGUI labelText, float height = ButtonHeight)
        {
            RectTransform rt = CreateRect(name, parent);

            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            // White base: the Button's ColorBlock tints it, which is what makes the disabled
            // state actually grey out instead of staying at full saturation.
            image.color = Color.white;

            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = CardShadow;
            shadow.effectDistance = new Vector2(0f, -6f);

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colours = button.colors;
            colours.normalColor = fill;
            colours.highlightedColor = Lighten(fill, 0.12f);
            colours.pressedColor = Darken(fill, 0.15f);
            colours.selectedColor = fill;
            colours.disabledColor = Disabled;
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            labelText = CreateText("Label", rt, label, 46f, ButtonInk);
            Stretch((RectTransform)labelText.transform);

            LayoutElement layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            return button;
        }

        /// <summary>A label/value line whose value stays right-aligned as the number grows.</summary>
        public static TextMeshProUGUI CreateStatRow(string name, Transform parent, string label)
        {
            RectTransform row = CreateRect(name, parent);
            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            group.spacing = 16f;

            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 72f;

            TextMeshProUGUI caption = CreateText("Label", row, label, 42f, LabelBrown, TextAlignmentOptions.Left);
            LayoutElement captionLayout = caption.gameObject.AddComponent<LayoutElement>();
            captionLayout.flexibleWidth = 1f;

            TextMeshProUGUI value = CreateText("Value", row, "0", 46f, ValueSlate, TextAlignmentOptions.Right);
            LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.minWidth = 220f;

            return value;
        }

        public static Color Lighten(Color c, float amount) =>
            Color.Lerp(c, Color.white, amount);

        public static Color Darken(Color c, float amount) =>
            Color.Lerp(c, Color.black, amount);
    }
}
