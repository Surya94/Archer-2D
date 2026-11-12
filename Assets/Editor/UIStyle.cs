using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Archer.Scripts.View;

namespace Archer.Editor
{
    /// <summary>
    /// Palette and widget factory shared by the UI builders. Colours are picked to sit with the
    /// game's cartoon parallax/balloon art: warm cream cards, saturated primaries, and dark
    /// brown label text on every fill so contrast stays comfortably readable rather than the
    /// low-contrast white-on-pastel that saturated buttons usually invite.
    ///
    /// Fonts and sprites come from FeelAssetBuilder (Archer/FX/Build Feel Assets); each lookup
    /// falls back to a Unity built-in so the builders still run before that has been built.
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color Dim = new Color32(0x0A, 0x1A, 0x2F, 0xB4); // deep navy scrim, ~70%
        public static readonly Color Card = new Color32(0xFF, 0xF4, 0xDC, 0xFF); // warm cream
        public static readonly Color CardEdge = new Color32(0xE9, 0xD3, 0xA6, 0xFF); // card rim / inset wells
        public static readonly Color CardShadow = new Color32(0x1E, 0x14, 0x0A, 0x8C);

        public static readonly Color TitleRed = new Color32(0xE8, 0x54, 0x3F, 0xFF);
        public static readonly Color LabelBrown = new Color32(0x6B, 0x5B, 0x45, 0xFF);
        public static readonly Color ValueSlate = new Color32(0x2E, 0x40, 0x57, 0xFF);
        public static readonly Color ButtonInk = new Color32(0x3A, 0x2A, 0x18, 0xFF);
        public static readonly Color OnDark = new Color32(0xFF, 0xF8, 0xE8, 0xFF);

        public static readonly Color Gold = new Color32(0xFF, 0xC5, 0x3D, 0xFF); // reward
        public static readonly Color Green = new Color32(0x7E, 0xD9, 0x57, 0xFF); // go / restart
        public static readonly Color Sky = new Color32(0x6E, 0xC6, 0xF5, 0xFF); // neutral / home
        public static readonly Color Disabled = new Color32(0xD8, 0xD2, 0xC4, 0xFF);

        // Reference-resolution pixels. The canvas reference height is 1080, so 110px is roughly
        // 9mm on a phone - comfortably above the ~48dp minimum touch target.
        public const float ButtonHeight = 124f;
        public const float AdButtonHeight = 144f;
        public const float CardWidth = 900f;
        /// <summary>How far a button's face sits above its darker lip.</summary>
        public const float LipDepth = 12f;

        // ------------------------------------------------------------------ Assets

        /// <summary>The 9-slice rounded rectangle used for cards, pills and buttons.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                Sprite sprite = FeelAssetBuilder.LoadKitSprite("RoundRect");
                if (sprite != null) return sprite;

                // Unity's built-in rounded sprite lives in the "builtin extra" resource file, so
                // GetBuiltinExtraResource is the correct lookup (GetBuiltinResource asserts).
                sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                if (sprite == null)
                    Debug.LogWarning("[UIStyle] No rounded sprite found; panels will render as plain rectangles.");
                return sprite;
            }
        }

        public static Sprite PillSprite => FeelAssetBuilder.LoadKitSprite("Pill") ?? RoundedSprite;
        public static Sprite ShadowSprite => FeelAssetBuilder.LoadKitSprite("Shadow");
        public static Sprite CircleSprite => FeelAssetBuilder.LoadKitSprite("Circle");
        public static Sprite RingSprite => FeelAssetBuilder.LoadKitSprite("Ring");
        public static Sprite GlowSprite => FeelAssetBuilder.LoadKitSprite("Glow");

        public static TMP_FontAsset Font
        {
            get
            {
                TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    FeelAssetBuilder.DisplayFontPath);
                if (font != null) return font;

                if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
                return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }
        }

        public static TMP_FontAsset IconFont =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FeelAssetBuilder.IconFontPath);

        /// <summary>Soft drop shadow - for text drawn straight over the game world.</summary>
        public static Material ShadowTextMaterial =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(FeelAssetBuilder.DisplayShadowMaterialPath);

        /// <summary>Dark outline + hard shadow - for titles and callouts.</summary>
        public static Material OutlineTextMaterial =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(FeelAssetBuilder.DisplayOutlineMaterialPath);

        // ------------------------------------------------------------------ Rects

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

        // ------------------------------------------------------------------ Text

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

        /// <summary>A glyph from the icon font. glyph is one of FeelAssetBuilder.Icon.*.</summary>
        public static TextMeshProUGUI CreateIcon(string name, Transform parent, string glyph,
            float size, Color colour)
        {
            RectTransform rt = CreateRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);

            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset iconFont = IconFont;
            if (iconFont != null) text.font = iconFont;
            else Debug.LogWarning("[UIStyle] Icon font missing - run Archer/FX/Build Feel Assets first.");

            text.text = glyph;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static void UseOutline(TextMeshProUGUI text)
        {
            Material material = OutlineTextMaterial;
            if (material != null && text.font == Font) text.fontSharedMaterial = material;
        }

        public static void UseShadow(TextMeshProUGUI text)
        {
            Material material = ShadowTextMaterial;
            if (material != null && text.font == Font) text.fontSharedMaterial = material;
        }

        // ------------------------------------------------------------------ Surfaces

        /// <summary>
        /// Give a rect a soft drop shadow and a rounded fill, as two children that sit behind
        /// everything else in it and are ignored by any layout group on the rect. Children are
        /// used (rather than an Image on the rect itself) because a graphic can only ever draw
        /// behind its own children, never behind itself - and the shadow has to be behind.
        /// </summary>
        public static Image AddSurface(RectTransform target, Color fill, float shadowDrop = 10f,
            bool raycastTarget = false)
        {
            Sprite shadowSprite = ShadowSprite;
            if (shadowSprite != null)
            {
                RectTransform shadow = Stretch(CreateRect("Shadow", target));
                // The sprite carries 22px of soft falloff around its opaque core.
                shadow.offsetMin = new Vector2(-22f, -22f - shadowDrop);
                shadow.offsetMax = new Vector2(22f, 22f - shadowDrop);
                Image shadowImage = shadow.gameObject.AddComponent<Image>();
                shadowImage.sprite = shadowSprite;
                shadowImage.type = Image.Type.Sliced;
                shadowImage.color = CardShadow;
                shadowImage.raycastTarget = false;
                shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                shadow.SetSiblingIndex(0);
            }

            RectTransform background = Stretch(CreateRect("Background", target));
            Image image = background.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = fill;
            image.raycastTarget = raycastTarget;
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            background.SetSiblingIndex(shadowSprite != null ? 1 : 0);
            return image;
        }

        // ------------------------------------------------------------------ Buttons

        /// <summary>
        /// A chunky cartoon button: a darker lip with the coloured face sitting on top of it, a
        /// gloss band, and an optional icon beside the label. UIButtonFeel sinks the face onto
        /// the lip while pressed and plays the click.
        /// </summary>
        public static Button CreateButton(string name, Transform parent, string label, Color fill,
            out TextMeshProUGUI labelText, float height = ButtonHeight, string icon = null,
            UIButtonFeel.ClickSound sound = UIButtonFeel.ClickSound.Click)
        {
            RectTransform rt = CreateRect(name, parent);

            RectTransform lip = Stretch(CreateRect("Lip", rt));
            Image lipImage = lip.gameObject.AddComponent<Image>();
            lipImage.sprite = RoundedSprite;
            lipImage.type = Image.Type.Sliced;
            lipImage.color = Darken(fill, 0.32f);
            lipImage.raycastTarget = false;

            RectTransform content = Stretch(CreateRect("Content", rt));
            content.offsetMin = new Vector2(0f, LipDepth);

            RectTransform face = Stretch(CreateRect("Face", content));
            Image faceImage = face.gameObject.AddComponent<Image>();
            faceImage.sprite = RoundedSprite;
            faceImage.type = Image.Type.Sliced;
            // White base: the Button's ColorBlock tints it, which is what makes the disabled
            // state actually grey out instead of staying at full saturation.
            faceImage.color = Color.white;

            RectTransform gloss = CreateRect("Gloss", content);
            gloss.anchorMin = new Vector2(0f, 0.5f);
            gloss.anchorMax = new Vector2(1f, 1f);
            gloss.offsetMin = new Vector2(8f, 0f);
            gloss.offsetMax = new Vector2(-8f, -7f);
            Image glossImage = gloss.gameObject.AddComponent<Image>();
            glossImage.sprite = RoundedSprite;
            glossImage.type = Image.Type.Sliced;
            glossImage.color = new Color(1f, 1f, 1f, 0.2f);
            glossImage.raycastTarget = false;

            RectTransform row = Stretch(CreateRect("Row", content));
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.padding = new RectOffset(16, 16, 0, 0);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            TextMeshProUGUI iconText = null;
            if (!string.IsNullOrEmpty(icon))
            {
                iconText = CreateIcon("Icon", row, icon, 60f, ButtonInk);
                LayoutElement iconLayout = iconText.gameObject.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = 60f;
                iconLayout.preferredHeight = 60f;
            }

            labelText = CreateText("Label", row, label, 48f, ButtonInk);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            // An icon-only button (pause) keeps the label object so callers always get one.
            labelText.gameObject.SetActive(!string.IsNullOrEmpty(label));

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            ColorBlock colours = button.colors;
            colours.normalColor = fill;
            colours.highlightedColor = Lighten(fill, 0.12f);
            colours.pressedColor = Darken(fill, 0.1f);
            colours.selectedColor = fill;
            colours.disabledColor = Disabled;
            colours.fadeDuration = 0.08f;
            button.colors = colours;
            // Touch-only game: no controller/keyboard focus ring to chase around the screen.
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            UIButtonFeel feel = rt.gameObject.AddComponent<UIButtonFeel>();
            feel.content = content;
            feel.lip = lipImage;
            feel.lipColour = lipImage.color;
            feel.sound = sound;
            feel.pressDepth = LipDepth - 3f;
            feel.dimWhenDisabled = iconText != null
                ? new Graphic[] { labelText, iconText }
                : new Graphic[] { labelText };

            LayoutElement layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            return button;
        }

        /// <summary>A label/value line whose value stays right-aligned as the number grows.</summary>
        public static TextMeshProUGUI CreateStatRow(string name, Transform parent, string label,
            string icon = null)
        {
            RectTransform row = CreateRect(name, parent);
            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(34, 34, 0, 0);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.spacing = 16f;

            // An inset well, slightly darker than the card, so the two numbers read as a readout.
            Image well = AddWell(row);
            well.color = CardEdge;

            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 92f;

            if (!string.IsNullOrEmpty(icon))
            {
                TextMeshProUGUI iconText = CreateIcon("Icon", row, icon, 52f, LabelBrown);
                iconText.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;
            }

            TextMeshProUGUI caption = CreateText("Label", row, label, 44f, LabelBrown, TextAlignmentOptions.Left);
            LayoutElement captionLayout = caption.gameObject.AddComponent<LayoutElement>();
            captionLayout.flexibleWidth = 1f;

            TextMeshProUGUI value = CreateText("Value", row, "0", 60f, ValueSlate, TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.minWidth = 260f;

            return value;
        }

        /// <summary>A flat rounded fill behind a row's contents, ignored by its layout group.</summary>
        public static Image AddWell(RectTransform target)
        {
            RectTransform well = Stretch(CreateRect("Well", target));
            Image image = well.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            well.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            well.SetSiblingIndex(0);
            return image;
        }

        public static Color Lighten(Color c, float amount) =>
            Color.Lerp(c, Color.white, amount);

        public static Color Darken(Color c, float amount) =>
            Color.Lerp(c, Color.black, amount);
    }
}
