using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Archer.Editor
{
    /// <summary>
    /// Builds the in-game HUD (score pill + arrow counter) under GameScene's "HUD Canvas" and
    /// regenerates the Score VFX popup prefab, then wires both into GamePlayHUD and
    /// ScoreAddingVFXHandler.
    ///
    /// Like UIPanelBuilder, re-running this rebuilds from scratch, so Inspector tweaks to these
    /// objects are lost - tune here. PauseButton stays owned by UIPanelBuilder.
    /// </summary>
    public static class HUDBuilder
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string ScoreVfxPrefabPath = "Assets/Prefabs/Score VFX.prefab";
        private const string ArrowSpritePath = "Assets/Art/Elf Archer/arrow.png";
        private const string DropShadowMaterialPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat";

        // Matches PauseButton (120x120, inset 40) so the whole top row shares one baseline.
        private const float Inset = 40f;
        private const float PillHeight = 120f;

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
            Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);

            RemoveLegacyHud(hudRoot, arrowSprite);
            RaiseHudAboveGameplay(hudCanvasGo.GetComponent<Canvas>());

            TextMeshProUGUI scoreText, bestText;
            RectTransform scorePill = BuildScorePill(hudRoot, out scoreText, out bestText);

            RectTransform arrowIcon;
            TextMeshProUGUI arrowText;
            RectTransform arrowPill = BuildArrowPill(hudRoot, arrowSprite, out arrowIcon, out arrowText);

            if (!WireHud(scorePill, scoreText, bestText, arrowPill, arrowIcon, arrowText)) return;
            if (!WireScoreVfx(vfxPrefab, scorePill)) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HUDBuilder] Gameplay HUD built, " + ScoreVfxPrefabPath + " regenerated, GameScene saved.");
        }

        // ------------------------------------------------------------------ Legacy cleanup

        /// <summary>
        /// The original legacy-Text HUD: "Score" (anchored top-right, overlapping PauseButton),
        /// "ArrowCoun" (anchored right but pushed 1632px left, so it drifts off non-16:9
        /// screens) and a centre-anchored arrow "Image". The Image is only removed when it
        /// actually shows the arrow sprite, so an unrelated object of that name survives.
        /// </summary>
        private static void RemoveLegacyHud(RectTransform hudRoot, Sprite arrowSprite)
        {
            DestroyChild(hudRoot, "Score");
            DestroyChild(hudRoot, "ArrowCoun");
            DestroyChild(hudRoot, "ScorePill");
            DestroyChild(hudRoot, "ArrowPill");

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

        // ------------------------------------------------------------------ Score pill

        private static RectTransform BuildScorePill(RectTransform hudRoot,
            out TextMeshProUGUI scoreText, out TextMeshProUGUI bestText)
        {
            // Top-centre: clear of PauseButton (top-right) and the arrow pill (top-left) on
            // every aspect ratio, since each is pinned to its own anchor.
            RectTransform pill = CreatePill("ScorePill", hudRoot, 400f);
            pill.anchorMin = new Vector2(0.5f, 1f);
            pill.anchorMax = new Vector2(0.5f, 1f);
            pill.pivot = new Vector2(0.5f, 1f);
            pill.anchoredPosition = new Vector2(0f, -Inset);

            scoreText = UIStyle.CreateText("ScoreValue", pill, "0", 80f, UIStyle.ValueSlate);
            scoreText.fontStyle = FontStyles.Bold;
            // Autosize so a six-digit score shrinks to fit instead of overflowing the pill.
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 80f;
            RectTransform scoreRt = UIStyle.Stretch((RectTransform)scoreText.transform);
            scoreRt.offsetMin = new Vector2(24f, 8f);
            scoreRt.offsetMax = new Vector2(-24f, -8f);

            // Hangs below the pill rather than inside it, so hiding it on a first run (nothing
            // to beat yet) doesn't leave the score off-centre.
            bestText = UIStyle.CreateText("BestLabel", pill, "BEST 0", 34f, UIStyle.LabelBrown);
            bestText.fontStyle = FontStyles.Bold;
            RectTransform bestRt = (RectTransform)bestText.transform;
            bestRt.anchorMin = new Vector2(0f, 0f);
            bestRt.anchorMax = new Vector2(1f, 0f);
            bestRt.pivot = new Vector2(0.5f, 1f);
            bestRt.anchoredPosition = new Vector2(0f, -6f);
            bestRt.sizeDelta = new Vector2(0f, 44f);

            return pill;
        }

        // ------------------------------------------------------------------ Arrow pill

        private static RectTransform BuildArrowPill(RectTransform hudRoot, Sprite arrowSprite,
            out RectTransform iconRt, out TextMeshProUGUI countText)
        {
            RectTransform pill = CreatePill("ArrowPill", hudRoot, 330f);
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

            countText = UIStyle.CreateText("ArrowCount", pill, "×10", 72f, UIStyle.ValueSlate,
                TextAlignmentOptions.Left);
            countText.fontStyle = FontStyles.Bold;
            // Never wrap "×10" onto two lines; shrink instead once a streak pushes it wider.
            countText.textWrappingMode = TextWrappingModes.NoWrap;
            countText.enableAutoSizing = true;
            countText.fontSizeMin = 40f;
            countText.fontSizeMax = 72f;
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

        private static RectTransform CreatePill(string name, RectTransform parent, float width)
        {
            RectTransform pill = UIStyle.CreateRect(name, parent);
            pill.sizeDelta = new Vector2(width, PillHeight);

            Image image = pill.gameObject.AddComponent<Image>();
            image.sprite = UIStyle.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = UIStyle.Card;
            // Display-only: must not swallow the drag that aims the bow.
            image.raycastTarget = false;

            Shadow shadow = pill.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UIStyle.CardShadow;
            shadow.effectDistance = new Vector2(0f, -6f);

            // Behind PauseButton and any runtime popups in draw order.
            pill.SetAsFirstSibling();
            return pill;
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
            root.sizeDelta = new Vector2(320f, 100f);

            TextMeshProUGUI label = UIStyle.CreateText("Label", root, "+10", 68f, Color.white);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.Stretch((RectTransform)label.transform);

            // A shared preset material built for this font atlas, so no per-object instance
            // is created and the font's own default material is left untouched.
            Material dropShadow = AssetDatabase.LoadAssetAtPath<Material>(DropShadowMaterialPath);
            if (dropShadow != null)
                label.fontSharedMaterial = dropShadow;
            else
                Debug.LogWarning("[HUDBuilder] Drop-shadow material not found; score popups will have no shadow.");

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
            GamePlayHUD hud = Object.FindFirstObjectByType<GamePlayHUD>(FindObjectsInactive.Include);
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
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool WireScoreVfx(GameObject vfxPrefab, RectTransform scorePill)
        {
            ScoreAddingVFXHandler handler =
                Object.FindFirstObjectByType<ScoreAddingVFXHandler>(FindObjectsInactive.Include);
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
