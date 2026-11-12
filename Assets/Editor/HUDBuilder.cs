using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Archer.Scripts.Manager.Feel;
using Archer.Scripts.Utility;

namespace Archer.Editor
{
    /// <summary>
    /// Builds the in-game HUD under GameScene's "HUD Canvas" - score pill, arrow counter, combo
    /// badge, streak callout, first-run hint - and regenerates the Score VFX
    /// popup prefab, then wires everything into GamePlayHUD, ScoreAddingVFXHandler,
    /// BonusEffectController and GameFeelController.
    ///
    /// Like UIPanelBuilder, re-running this rebuilds from scratch, so Inspector tweaks to these
    /// objects are lost - tune here. PauseButton is created by UIPanelBuilder; this builder only
    /// moves it inside the safe area.
    /// </summary>
    public static class HUDBuilder
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string ScoreVfxPrefabPath = "Assets/Prefabs/Score VFX.prefab";
        private const string ArrowSpritePath = "Assets/Art/Elf Archer/arrow.png";

        internal const string SafeAreaName = "SafeArea";

        // Matches PauseButton (124x124, inset 40) so the whole top row shares one baseline.
        private const float Inset = 40f;
        private const float PillHeight = 124f;

        [MenuItem("Archer/UI/4. Build Gameplay HUD")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[HUDBuilder] Exit play mode before building the HUD.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            GameObject vfxPrefab = BuildScoreVfxPrefab();

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            GameObject hudCanvasGo = FindRoot(scene, "HUD Canvas");
            if (hudCanvasGo == null)
            {
                Debug.LogError("[HUDBuilder] 'HUD Canvas' not found in GameScene - aborting.");
                return;
            }

            RectTransform hudRoot = (RectTransform)hudCanvasGo.transform;
            Canvas hudCanvas = hudCanvasGo.GetComponent<Canvas>();
            Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);

            RemoveLegacyHud(hudRoot, arrowSprite);
            RaiseHudAboveGameplay(hudCanvas);

            RectTransform safeArea = BuildSafeArea(hudRoot);

            TextMeshProUGUI scoreText, bestText;
            RectTransform scorePill = BuildScorePill(safeArea, out scoreText, out bestText);

            RectTransform arrowIcon;
            TextMeshProUGUI arrowText;
            RectTransform arrowPill = BuildArrowPill(safeArea, arrowSprite, out arrowIcon, out arrowText);

            TextMeshProUGUI comboText;
            RectTransform comboBadge = BuildComboBadge(safeArea, out comboText);

            TextMeshProUGUI calloutText;
            RectTransform callout = BuildCallout(safeArea, out calloutText);

            Image flash = BuildScreenFlash(hudRoot);
            BuildFirstRunHint(safeArea);
            RestyleFreezeOverlay(hudRoot);

            if (!WireHud(scorePill, scoreText, bestText, arrowPill, arrowIcon, arrowText)) return;
            if (!WireScoreVfx(vfxPrefab, scorePill)) return;
            WireBonusEffects(arrowPill);
            WireGameFeel(callout, calloutText, comboBadge, comboText, flash);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HUDBuilder] Gameplay HUD built, " + ScoreVfxPrefabPath + " regenerated, GameScene saved.");
        }

        // ------------------------------------------------------------------ Legacy cleanup

        /// <summary>
        /// Everything this builder owns, at either nesting level: directly under the canvas
        /// (older builds) or inside the safe area. The original legacy-Text HUD ("Score",
        /// "ArrowCoun", a centre-anchored arrow "Image") is swept up too; that Image is only
        /// removed when it actually shows the arrow sprite, so an unrelated object of that name
        /// survives.
        /// </summary>
        private static void RemoveLegacyHud(RectTransform hudRoot, Sprite arrowSprite)
        {
            Transform safeArea = hudRoot.Find(SafeAreaName);
            foreach (Transform parent in new[] { hudRoot, safeArea })
            {
                if (parent == null) continue;

                foreach (string name in new[]
                         {
                             "Score", "ArrowCoun", "ScorePill", "ArrowPill", "ComboBadge", "Callout",
                             "FirstRunHint", "DragIndicator", "ScreenFlash",
                         })
                {
                    DestroyChild(parent, name);
                }
            }

            Transform icon = hudRoot.Find("Image");
            Image image = icon != null ? icon.GetComponent<Image>() : null;
            if (image != null && arrowSprite != null && image.sprite == arrowSprite)
                Object.DestroyImmediate(icon.gameObject);
        }

        /// <summary>
        /// The HUD canvas sat on the Default sorting layer, below the Player/Enemy layers, so
        /// balloons drifted over the score pill and hid the score popups. Same fix
        /// UIPanelBuilder applies to "UI Canvas"; that canvas keeps a higher sorting order, so
        /// pause/game-over panels still draw above the HUD.
        /// </summary>
        private static void RaiseHudAboveGameplay(Canvas hudCanvas)
        {
            if (hudCanvas == null) return;

            if (UIPanelBuilder.HasSortingLayer("UI"))
                hudCanvas.sortingLayerName = "UI";
            else
                Debug.LogWarning("[HUDBuilder] No 'UI' sorting layer; balloons may draw over the HUD.");
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        // ------------------------------------------------------------------ Safe area

        /// <summary>
        /// Corner-pinned HUD lives in here so it clears notches and rounded corners. The
        /// full-bleed layers (frost, flash) stay directly on the canvas.
        /// </summary>
        private static RectTransform BuildSafeArea(RectTransform hudRoot)
        {
            Transform existing = hudRoot.Find(SafeAreaName);
            RectTransform safeArea = existing != null
                ? (RectTransform)existing
                : UIStyle.CreateRect(SafeAreaName, hudRoot);
            UIStyle.Stretch(safeArea);

            if (safeArea.GetComponent<SafeAreaFitter>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFitter>();

            // PauseButton keeps its identity (GameplayHUDPanel holds a reference to it); it is
            // only re-parented. Its corner anchoring carries over unchanged.
            Transform pause = hudRoot.Find("PauseButton");
            if (pause != null) pause.SetParent(safeArea, false);

            return safeArea;
        }

        // ------------------------------------------------------------------ Score pill

        private static RectTransform BuildScorePill(RectTransform parent,
            out TextMeshProUGUI scoreText, out TextMeshProUGUI bestText)
        {
            // Top-centre: clear of PauseButton (top-right) and the arrow pill (top-left) on
            // every aspect ratio, since each is pinned to its own anchor.
            RectTransform pill = CreatePill("ScorePill", parent, 400f);
            pill.anchorMin = new Vector2(0.5f, 1f);
            pill.anchorMax = new Vector2(0.5f, 1f);
            pill.pivot = new Vector2(0.5f, 1f);
            pill.anchoredPosition = new Vector2(0f, -Inset);

            scoreText = UIStyle.CreateText("ScoreValue", pill, "0", 86f, UIStyle.ValueSlate);
            // Autosize so a six-digit score shrinks to fit instead of overflowing the pill.
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 86f;
            RectTransform scoreRt = UIStyle.Stretch((RectTransform)scoreText.transform);
            scoreRt.offsetMin = new Vector2(24f, 8f);
            scoreRt.offsetMax = new Vector2(-24f, -8f);

            // Hangs below the pill rather than inside it, so hiding it on a first run (nothing
            // to beat yet) doesn't leave the score off-centre. It is drawn straight over the
            // scenery, hence the light colour and drop shadow.
            bestText = UIStyle.CreateText("BestLabel", pill, "BEST 0", 38f, UIStyle.OnDark);
            UIStyle.UseShadow(bestText);
            RectTransform bestRt = (RectTransform)bestText.transform;
            bestRt.anchorMin = new Vector2(0f, 0f);
            bestRt.anchorMax = new Vector2(1f, 0f);
            bestRt.pivot = new Vector2(0.5f, 1f);
            bestRt.anchoredPosition = new Vector2(0f, -12f);
            bestRt.sizeDelta = new Vector2(0f, 48f);

            return pill;
        }

        // ------------------------------------------------------------------ Arrow pill

        private static RectTransform BuildArrowPill(RectTransform parent, Sprite arrowSprite,
            out RectTransform iconRt, out TextMeshProUGUI countText)
        {
            RectTransform pill = CreatePill("ArrowPill", parent, 330f);
            pill.anchorMin = new Vector2(0f, 1f);
            pill.anchorMax = new Vector2(0f, 1f);
            pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(Inset, -Inset);

            iconRt = UIStyle.CreateRect("ArrowIcon", pill);
            Image icon = iconRt.gameObject.AddComponent<Image>();
            icon.sprite = arrowSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (arrowSprite == null)
                Debug.LogWarning("[HUDBuilder] Arrow sprite not found at " + ArrowSpritePath + "; icon will be blank.");
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(90f, 0f);
            iconRt.sizeDelta = new Vector2(140f, 48f);
            // Tilted up like a nocked shot - reads as "ammo" faster than a flat arrow.
            iconRt.localRotation = Quaternion.Euler(0f, 0f, 25f);

            countText = UIStyle.CreateText("ArrowCount", pill, "×10", 76f, UIStyle.ValueSlate,
                TextAlignmentOptions.Left);
            // Never wrap "×10" onto two lines; shrink instead once a streak pushes it wider.
            countText.textWrappingMode = TextWrappingModes.NoWrap;
            countText.enableAutoSizing = true;
            countText.fontSizeMin = 40f;
            countText.fontSizeMax = 76f;
            RectTransform countRt = (RectTransform)countText.transform;
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(1f, 1f);
            // Pivot near the text's visual centre so the punch scales around the number, not
            // the pill's left edge.
            countRt.pivot = new Vector2(0.35f, 0.5f);
            countRt.offsetMin = new Vector2(185f, 0f);
            countRt.offsetMax = new Vector2(-16f, 0f);

            return pill;
        }

        internal static RectTransform CreatePill(string name, RectTransform parent, float width)
        {
            RectTransform pill = UIStyle.CreateRect(name, parent);
            pill.sizeDelta = new Vector2(width, PillHeight);

            Image image = pill.gameObject.AddComponent<Image>();
            image.sprite = UIStyle.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = UIStyle.Card;
            // Display-only: must not swallow the drag that aims the bow.
            image.raycastTarget = false;

            // A hard offset copy rather than a soft blur: it gives the pill the same chunky
            // "lip" the buttons have.
            Shadow shadow = pill.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0x4A, 0x3B, 0x2A, 0xB4);
            shadow.effectDistance = new Vector2(0f, -8f);

            // Behind PauseButton and any runtime popups in draw order.
            pill.SetAsFirstSibling();
            return pill;
        }

        // ------------------------------------------------------------------ Combo + callout

        /// <summary>The live streak count, tucked against the score pill's right shoulder.</summary>
        private static RectTransform BuildComboBadge(RectTransform parent, out TextMeshProUGUI text)
        {
            RectTransform badge = UIStyle.CreateRect("ComboBadge", parent);
            badge.anchorMin = new Vector2(0.5f, 1f);
            badge.anchorMax = new Vector2(0.5f, 1f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = new Vector2(292f, -Inset - PillHeight * 0.5f);
            badge.sizeDelta = new Vector2(150f, 92f);
            badge.localRotation = Quaternion.Euler(0f, 0f, -6f);

            Image image = badge.gameObject.AddComponent<Image>();
            image.sprite = UIStyle.PillSprite;
            image.type = Image.Type.Sliced;
            image.color = UIStyle.Gold;
            image.raycastTarget = false;

            Shadow shadow = badge.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0x8A, 0x5A, 0x10, 0xFF);
            shadow.effectDistance = new Vector2(0f, -7f);

            text = UIStyle.CreateText("Value", badge, "×2", 62f, UIStyle.ButtonInk);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.Stretch((RectTransform)text.transform);

            badge.gameObject.SetActive(false);
            return badge;
        }

        /// <summary>NICE! / GREAT! / ON FIRE! - upper-middle of the screen, over the play field.</summary>
        private static RectTransform BuildCallout(RectTransform parent, out TextMeshProUGUI text)
        {
            RectTransform callout = UIStyle.CreateRect("Callout", parent);
            callout.anchorMin = new Vector2(0.5f, 1f);
            callout.anchorMax = new Vector2(0.5f, 1f);
            callout.pivot = new Vector2(0.5f, 0.5f);
            callout.anchoredPosition = new Vector2(0f, -400f);
            callout.sizeDelta = new Vector2(1100f, 180f);

            text = UIStyle.CreateText("Label", callout, "NICE!", 132f, UIStyle.Gold);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.UseOutline(text);
            UIStyle.Stretch((RectTransform)text.transform);

            callout.gameObject.SetActive(false);
            return callout;
        }

        // ------------------------------------------------------------------ Full-screen layers

        private static Image BuildScreenFlash(RectTransform hudRoot)
        {
            RectTransform rt = UIStyle.Stretch(UIStyle.CreateRect("ScreenFlash", hudRoot));
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;

            // Over the frost, under the pills.
            Transform freeze = hudRoot.Find("FreezeOverlay");
            rt.SetSiblingIndex(freeze != null ? freeze.GetSiblingIndex() + 1 : 0);
            rt.gameObject.SetActive(false);
            return image;
        }

        private static void BuildFirstRunHint(RectTransform parent)
        {
            RectTransform root = UIStyle.Stretch(UIStyle.CreateRect("FirstRunHint", parent));
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform hand = UIStyle.Centre(UIStyle.CreateRect("Hand", root), 200f, 200f);
            hand.anchoredPosition = new Vector2(140f, -40f);

            // A dark halo so the pale hand reads over bright sky as well as dark trees.
            RectTransform halo = UIStyle.Stretch(UIStyle.CreateRect("Halo", hand));
            halo.offsetMin = new Vector2(-50f, -50f);
            halo.offsetMax = new Vector2(50f, 50f);
            Image haloImage = halo.gameObject.AddComponent<Image>();
            haloImage.sprite = UIStyle.GlowSprite;
            haloImage.color = new Color(0.04f, 0.08f, 0.14f, 0.7f);
            haloImage.raycastTarget = false;

            TextMeshProUGUI icon = UIStyle.CreateIcon("Icon", hand, FeelAssetBuilder.Icon.Touch, 170f, UIStyle.OnDark);
            UIStyle.Stretch((RectTransform)icon.transform);

            TextMeshProUGUI label = UIStyle.CreateText("Label", root, "DRAG BACK & RELEASE", 72f, UIStyle.OnDark);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.UseOutline(label);
            RectTransform labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = new Vector2(0.5f, 0f);
            labelRt.anchorMax = new Vector2(0.5f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 150f);
            labelRt.sizeDelta = new Vector2(1200f, 100f);

            FirstRunHint hint = root.gameObject.AddComponent<FirstRunHint>();
            hint.group = group;
            hint.hand = hand;
        }

        /// <summary>
        /// The freeze countdown is built by BonusBalloonBuilder, which is not re-run for a
        /// restyle (it also rebuilds the bonus prefabs). Bring its pill and number in line with
        /// the rest of the HUD in place.
        /// </summary>
        private static void RestyleFreezeOverlay(RectTransform hudRoot)
        {
            Transform pill = hudRoot.Find("FreezeOverlay/FreezeCountdown");
            if (pill == null) return;

            Image image = pill.GetComponent<Image>();
            if (image != null) image.sprite = UIStyle.RoundedSprite;

            foreach (TextMeshProUGUI text in pill.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.font = UIStyle.Font;
                text.fontStyle = FontStyles.Normal;
            }
        }

        // ------------------------------------------------------------------ Score VFX prefab

        /// <summary>
        /// Regenerated rather than patched: the old prefab was Animator-driven (with an
        /// animation-event hook in ScoreVFXHolder) and its clip keys position and scale, which
        /// would fight the DOTween flight. ScoreVFXData now sits on the root, because the
        /// flight moves the object that is the canvas's direct child.
        /// </summary>
        private static GameObject BuildScoreVfxPrefab()
        {
            RectTransform root = UIStyle.CreateRect("Score VFX", null);
            root.sizeDelta = new Vector2(360f, 110f);

            TextMeshProUGUI label = UIStyle.CreateText("Label", root, "+10", 76f, Color.white);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.Stretch((RectTransform)label.transform);
            // A shared preset material, so no per-object instance is created and the font's
            // own default material is left untouched.
            UIStyle.UseOutline(label);

            ScoreVFXData data = root.gameObject.AddComponent<ScoreVFXData>();
            data.label = label;
            data.comboColour = UIStyle.Gold;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, ScoreVfxPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        // ------------------------------------------------------------------ Wiring

        private static bool WireHud(RectTransform scorePill, TextMeshProUGUI scoreText,
            TextMeshProUGUI bestText, RectTransform arrowPill, RectTransform arrowIcon,
            TextMeshProUGUI arrowText)
        {
            GamePlayHUD hud = Object.FindAnyObjectByType<GamePlayHUD>(FindObjectsInactive.Include);
            if (hud == null)
            {
                Debug.LogError("[HUDBuilder] No GamePlayHUD in GameScene - aborting before save.");
                return false;
            }

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("scorePill").objectReferenceValue = scorePill;
            so.FindProperty("score").objectReferenceValue = scoreText;
            so.FindProperty("bestScore").objectReferenceValue = bestText;
            so.FindProperty("arrowPill").objectReferenceValue = arrowPill;
            so.FindProperty("arrowIcon").objectReferenceValue = arrowIcon;
            so.FindProperty("arrowCount").objectReferenceValue = arrowText;
            // The BEST line sits over the scenery, not on the cream pill.
            so.FindProperty("bestColour").colorValue = UIStyle.OnDark;
            so.FindProperty("newBestColour").colorValue = UIStyle.Gold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool WireScoreVfx(GameObject vfxPrefab, RectTransform scorePill)
        {
            ScoreAddingVFXHandler handler =
                Object.FindAnyObjectByType<ScoreAddingVFXHandler>(FindObjectsInactive.Include);
            if (handler == null)
            {
                Debug.LogError("[HUDBuilder] No ScoreAddingVFXHandler in GameScene - aborting before save.");
                return false;
            }

            SerializedObject so = new SerializedObject(handler);
            so.FindProperty("vfxObj").objectReferenceValue = vfxPrefab;
            so.FindProperty("scoreTarget").objectReferenceValue = scorePill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>The Extra Arrows bonus flies its quiver into the arrow pill, which was just rebuilt.</summary>
        private static void WireBonusEffects(RectTransform arrowPill)
        {
            BonusEffectController controller =
                Object.FindAnyObjectByType<BonusEffectController>(FindObjectsInactive.Include);
            if (controller == null) return;

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("arrowTarget").objectReferenceValue = arrowPill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireGameFeel(RectTransform callout, TextMeshProUGUI calloutText,
            RectTransform comboBadge, TextMeshProUGUI comboText, Image flash)
        {
            GameFeelController feel =
                Object.FindAnyObjectByType<GameFeelController>(FindObjectsInactive.Include);
            if (feel == null)
                feel = new GameObject("GameFeel").AddComponent<GameFeelController>();

            PopBurstFX burst = AssetDatabase.LoadAssetAtPath<PopBurstFX>(FeelAssetBuilder.PopBurstPrefabPath);
            if (burst == null)
                Debug.LogWarning("[HUDBuilder] PopBurst prefab missing - run Archer/FX/Build Feel Assets first.");

            SerializedObject so = new SerializedObject(feel);
            so.FindProperty("popBurstPrefab").objectReferenceValue = burst;
            so.FindProperty("calloutRoot").objectReferenceValue = callout;
            so.FindProperty("calloutText").objectReferenceValue = calloutText;
            so.FindProperty("comboBadge").objectReferenceValue = comboBadge;
            so.FindProperty("comboText").objectReferenceValue = comboText;
            so.FindProperty("screenFlash").objectReferenceValue = flash;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }
    }
}
