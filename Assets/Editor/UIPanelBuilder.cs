using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Archer.Scripts.View;
using Archer.Scripts.View.Panels;
using Archer.Scripts.View.Manager;

namespace Archer.Editor
{
    /// <summary>
    /// Builds the Game Over and Pause UI as prefabs and wires them into GameScene.
    ///
    /// The layout lives in code rather than being hand-authored in the Inspector so that every
    /// anchor, pivot and colour is explicit and reviewable in a diff. Re-running a builder
    /// regenerates its prefab from scratch, so hand-tweaks made in the Inspector afterwards
    /// will be lost - tweak here, or stop running the builder once you start tuning by hand.
    /// </summary>
    public static class UIPanelBuilder
    {
        private const string PrefabDir = "Assets/Prefabs/UI";
        private const string GameOverPrefabPath = PrefabDir + "/GameOverPanel.prefab";
        private const string PausePrefabPath = PrefabDir + "/PauseMenuPanel.prefab";
        private const string MainMenuPrefabPath = PrefabDir + "/MainMenuPanel.prefab";
        private const string SettingsPrefabPath = PrefabDir + "/SettingsPanel.prefab";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        // Under Resources: SceneLoader is created lazily at runtime and has no scene to hold a
        // serialized reference, so it loads the screen by path.
        private const string LoadingScreenDir = "Assets/Resources/UI";
        private const string LoadingScreenPrefabPath = LoadingScreenDir + "/LoadingScreen.prefab";
        private const string SkyLayerPath = "Assets/Free 2D Cartoon Parallax Background/!_Moutain/Layer_0.png";
        private const string CloudLayerPath = "Assets/Free 2D Cartoon Parallax Background/!_Moutain/Layer_1.png";

        // UIState values, as serialized enum indices.
        private const int StateMainMenu = 1;
        private const int StateGameplay = 2;
        private const int StatePauseMenu = 3;
        private const int StateGameOver = 4;
        private const int StateSettings = 5;

        private const float MenuColumnWidth = 700f;
        private const float HeaderHeight = 128f;

        private const int TransitionFade = 1;

        [MenuItem("Archer/UI/1. Build UI Panel Prefabs")]
        public static void BuildPrefabs()
        {
            // In play mode AddComponent runs Awake, and BaseUIPanel.Awake deactivates the panel
            // (hideOnStart) before its fields are set - the prefabs get saved inactive at alpha 0.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIPanelBuilder] Exit play mode before building UI prefabs.");
                return;
            }

            EnsureDirectory();
            BuildGameOverPrefab();
            BuildPausePrefab();
            BuildMainMenuPrefab();
            BuildSettingsPrefab();
            BuildLoadingScreenPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UIPanelBuilder] Built " + GameOverPrefabPath + ", " + PausePrefabPath +
                      ", " + MainMenuPrefabPath + ", " + SettingsPrefabPath + " and " + LoadingScreenPrefabPath);
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();
            }
        }

        // ------------------------------------------------------------------ Game Over

        private static void BuildGameOverPrefab()
        {
            RectTransform root = CreatePanelRoot("GameOverPanel");
            GameOverPanel panel = root.gameObject.AddComponent<GameOverPanel>();

            RectTransform card = CreateCard(root, "OUT OF ARROWS", UIStyle.TitleRed);

            TextMeshProUGUI scoreValue = UIStyle.CreateStatRow("ScoreRow", card, "SCORE", FeelAssetBuilder.Icon.Star);
            TextMeshProUGUI bestValue = UIStyle.CreateStatRow("BestRow", card, "BEST", FeelAssetBuilder.Icon.Trophy);
            RectTransform scoreRow = (RectTransform)scoreValue.transform.parent;
            RectTransform bestRow = (RectTransform)bestValue.transform.parent;

            // All three buttons share one CanvasGroup, so the panel can hold them inert until
            // the card has landed.
            RectTransform buttons = UIStyle.CreateRect("Buttons", card);
            VerticalLayoutGroup buttonsLayout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            buttonsLayout.padding = new RectOffset(0, 0, 10, 0);
            buttonsLayout.spacing = 20f;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = false;
            CanvasGroup buttonGroup = buttons.gameObject.AddComponent<CanvasGroup>();

            TextMeshProUGUI adLabel;
            Button adButton = UIStyle.CreateButton("WatchAdButton", buttons, "WATCH AD  +5",
                UIStyle.Gold, out adLabel, UIStyle.AdButtonHeight, FeelAssetBuilder.Icon.Video);

            TextMeshProUGUI ignored;
            RectTransform buttonRow = CreateButtonRow(buttons);
            Button restart = UIStyle.CreateButton("RestartButton", buttonRow, "RESTART", UIStyle.Green,
                out ignored, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Restart);
            Button home = UIStyle.CreateButton("HomeButton", buttonRow, "HOME", UIStyle.Sky,
                out ignored, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Home);

            RectTransform ribbon = CreateNewBestRibbon(card);

            // On the panel root, not the card: the pieces must not be scaled by the card's
            // pop-in, and they fly well outside its bounds.
            RectTransform confettiRt = UIStyle.Centre(UIStyle.CreateRect("Confetti", root), 0f, 0f);
            confettiRt.anchoredPosition = new Vector2(0f, 200f);
            UIConfetti confetti = confettiRt.gameObject.AddComponent<UIConfetti>();

            PanelIntro intro = AddIntro(root, card, scoreRow, bestRow, buttons);

            SerializedObject so = new SerializedObject(panel);
            ApplyBasePanelFields(so, root, StateGameOver);
            so.FindProperty("scoreText").objectReferenceValue = scoreValue;
            so.FindProperty("bestText").objectReferenceValue = bestValue;
            so.FindProperty("watchAdButton").objectReferenceValue = adButton;
            so.FindProperty("watchAdLabel").objectReferenceValue = adLabel;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("homeButton").objectReferenceValue = home;
            so.FindProperty("reviveArrowCount").intValue = 5;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("buttonGroup").objectReferenceValue = buttonGroup;
            so.FindProperty("newBestRibbon").objectReferenceValue = ribbon;
            so.FindProperty("confetti").objectReferenceValue = confetti;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAndDiscard(root.gameObject, GameOverPrefabPath);
        }

        // ---------------------------------------------------------------------- Pause

        private static void BuildPausePrefab()
        {
            RectTransform root = CreatePanelRoot("PauseMenuPanel");
            PauseMenuPanel panel = root.gameObject.AddComponent<PauseMenuPanel>();

            RectTransform card = CreateCard(root, "PAUSED", UIStyle.ValueSlate);

            TextMeshProUGUI ignored;
            Button resume = UIStyle.CreateButton("ResumeButton", card, "RESUME", UIStyle.Green,
                out ignored, UIStyle.AdButtonHeight, FeelAssetBuilder.Icon.Play);
            Button settings = UIStyle.CreateButton("SettingsButton", card, "SETTINGS", UIStyle.Gold,
                out ignored, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Settings);

            RectTransform buttonRow = CreateButtonRow(card);
            Button restart = UIStyle.CreateButton("RestartButton", buttonRow, "RESTART", UIStyle.Sky,
                out ignored, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Restart);
            Button home = UIStyle.CreateButton("HomeButton", buttonRow, "HOME", UIStyle.Sky,
                out ignored, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Home);

            PanelIntro intro = AddIntro(root, card, (RectTransform)resume.transform,
                (RectTransform)settings.transform, buttonRow);

            SerializedObject so = new SerializedObject(panel);
            ApplyBasePanelFields(so, root, StatePauseMenu);
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("homeButton").objectReferenceValue = home;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAndDiscard(root.gameObject, PausePrefabPath);
        }

        // ------------------------------------------------------------------ Main Menu

        /// <summary>
        /// Full-screen landing layout rather than a card: no dimmer, so the scene's mountain
        /// background stays the backdrop, with a centred column of title, best score and buttons.
        /// </summary>
        private static void BuildMainMenuPrefab()
        {
            GameObject go = new GameObject("MainMenuPanel", typeof(RectTransform));
            RectTransform root = UIStyle.Stretch((RectTransform)go.transform);
            go.AddComponent<CanvasGroup>();
            MainMenuPanel panel = go.AddComponent<MainMenuPanel>();

            RectTransform column = UIStyle.Centre(UIStyle.CreateRect("Column", root), MenuColumnWidth, 0f);

            VerticalLayoutGroup group = column.gameObject.AddComponent<VerticalLayoutGroup>();
            // Sized so the column (~850px) keeps a comfortable margin on a 20:9 phone, where the
            // 1980x1080 match-0.5 canvas is only ~980px tall.
            group.spacing = 18f;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The title is drawn straight over the scenery, so it carries its own outline.
            TextMeshProUGUI title = UIStyle.CreateText("Title", column, "ARCHER", 176f, UIStyle.TitleRed);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.UseOutline(title);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;

            RectTransform bestPill;
            TextMeshProUGUI best = CreateBestPill(column, out bestPill);

            AddSpacer(column, 6f);

            TextMeshProUGUI label;
            Button play = UIStyle.CreateButton("PlayButton", column, "PLAY", UIStyle.Green, out label,
                160f, FeelAssetBuilder.Icon.Play);
            label.fontSize = 68f;
            Button settings = UIStyle.CreateButton("SettingsButton", column, "SETTINGS", UIStyle.Gold,
                out label, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Settings);

            RectTransform soonRow = CreateButtonRow(column);
            soonRow.name = "ComingSoonRow";
            Button shop = UIStyle.CreateButton("ShopButton", soonRow, "SHOP (SOON)", UIStyle.Sky, out label);
            label.fontSize = 36f;
            Button removeAds = UIStyle.CreateButton("RemoveAdsButton", soonRow, "NO ADS (SOON)", UIStyle.Sky, out label);
            label.fontSize = 36f;
            // Disabled in the prefab too, not only at runtime, so the edit-mode preview is honest.
            shop.interactable = false;
            removeAds.interactable = false;

            Button quit = UIStyle.CreateButton("QuitButton", column, "QUIT", UIStyle.Sky, out label,
                UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Exit, UIButtonFeel.ClickSound.Back);

            // No card on this screen: the rows pop in over the scenery. The title is animated by
            // MainMenuPanel itself, because it keeps moving afterwards.
            PanelIntro intro = AddIntro(root, null, bestPill, (RectTransform)play.transform,
                (RectTransform)settings.transform, soonRow, (RectTransform)quit.transform);

            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("title").objectReferenceValue = (RectTransform)title.transform;
            so.FindProperty("intro").objectReferenceValue = intro;
            ApplyBasePanelFields(so, root, StateMainMenu);
            // The menu is the scene's starting state, so it must NOT hide itself on Awake.
            so.FindProperty("hideOnStart").boolValue = false;
            so.FindProperty("bestText").objectReferenceValue = best;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("showComingSoonButtons").boolValue = true;
            so.FindProperty("comingSoonRow").objectReferenceValue = soonRow.gameObject;
            so.FindProperty("shopButton").objectReferenceValue = shop;
            so.FindProperty("removeAdsButton").objectReferenceValue = removeAds;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAndDiscard(go, MainMenuPrefabPath);
        }

        private static TextMeshProUGUI CreateBestPill(RectTransform parent, out RectTransform pill)
        {
            pill = UIStyle.CreateRect("BestPill", parent);

            Image image = pill.gameObject.AddComponent<Image>();
            image.sprite = UIStyle.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = UIStyle.Card;
            image.raycastTarget = false;

            Shadow shadow = pill.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0x4A, 0x3B, 0x2A, 0xB4);
            shadow.effectDistance = new Vector2(0f, -8f);

            pill.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            HorizontalLayoutGroup row = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            TextMeshProUGUI icon = UIStyle.CreateIcon("Icon", pill, FeelAssetBuilder.Icon.Trophy, 58f, UIStyle.Gold);
            LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 58f;
            iconLayout.preferredHeight = 58f;

            TextMeshProUGUI text = UIStyle.CreateText("Value", pill, "BEST  0", 52f, UIStyle.ValueSlate);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>
        /// The NEW BEST stamp: pinned over the card's top-right corner and outside its layout,
        /// hidden until GameOverPanel slams it on.
        /// </summary>
        private static RectTransform CreateNewBestRibbon(RectTransform card)
        {
            RectTransform ribbon = UIStyle.CreateRect("NewBestRibbon", card);
            ribbon.anchorMin = new Vector2(1f, 1f);
            ribbon.anchorMax = new Vector2(1f, 1f);
            ribbon.pivot = new Vector2(0.5f, 0.5f);
            // Hangs off the corner, mostly outside the card, so it never covers the score.
            ribbon.anchoredPosition = new Vector2(-30f, 14f);
            ribbon.sizeDelta = new Vector2(340f, 96f);
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            Image image = ribbon.gameObject.AddComponent<Image>();
            image.sprite = UIStyle.PillSprite;
            image.type = Image.Type.Sliced;
            image.color = UIStyle.Gold;
            image.raycastTarget = false;

            Shadow shadow = ribbon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0x8A, 0x5A, 0x10, 0xFF);
            shadow.effectDistance = new Vector2(0f, -8f);

            TextMeshProUGUI text = UIStyle.CreateText("Label", ribbon, "NEW BEST!", 54f, UIStyle.ButtonInk);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.Stretch((RectTransform)text.transform);

            ribbon.gameObject.SetActive(false);
            return ribbon;
        }

        private static PanelIntro AddIntro(RectTransform panelRoot, RectTransform card, params RectTransform[] items)
        {
            PanelIntro intro = panelRoot.gameObject.AddComponent<PanelIntro>();
            intro.card = card;
            intro.items = items;
            return intro;
        }

        // ------------------------------------------------------------------- Settings

        /// <summary>
        /// Sound / Music / Vibration switches. Each row is one big button - the whole row
        /// toggles, not just the ON/OFF chip - bound to GameSettings by SettingsPanel.
        /// </summary>
        private static void BuildSettingsPrefab()
        {
            RectTransform root = CreatePanelRoot("SettingsPanel");
            SettingsPanel panel = root.gameObject.AddComponent<SettingsPanel>();

            RectTransform card = CreateCard(root, "SETTINGS", UIStyle.ValueSlate);

            SerializedObject so = new SerializedObject(panel);
            ApplyBasePanelFields(so, root, StateSettings);

            RectTransform sound = CreateToggleRow(card, "SoundRow", "SOUND", so.FindProperty("soundRow"),
                FeelAssetBuilder.Icon.SoundOn, FeelAssetBuilder.Icon.SoundOff);
            RectTransform music = CreateToggleRow(card, "MusicRow", "MUSIC", so.FindProperty("musicRow"),
                FeelAssetBuilder.Icon.MusicOn, FeelAssetBuilder.Icon.MusicOff);
            RectTransform vibration = CreateToggleRow(card, "VibrationRow", "VIBRATION", so.FindProperty("vibrationRow"),
                FeelAssetBuilder.Icon.Vibration, FeelAssetBuilder.Icon.Vibration);

            AddSpacer(card, 4f);

            TextMeshProUGUI ignored;
            Button back = UIStyle.CreateButton("BackButton", card, "BACK", UIStyle.Sky, out ignored,
                UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Back, UIButtonFeel.ClickSound.Back);

            PanelIntro intro = AddIntro(root, card, sound, music, vibration, (RectTransform)back.transform);

            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("onColour").colorValue = UIStyle.Green;
            so.FindProperty("offColour").colorValue = UIStyle.Disabled;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAndDiscard(root.gameObject, SettingsPrefabPath);
        }

        private static RectTransform CreateToggleRow(RectTransform card, string name, string label,
            SerializedProperty row, string onGlyph, string offGlyph)
        {
            RectTransform rt = UIStyle.CreateRect(name, card);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 112f;

            Image well = UIStyle.AddWell(rt);
            well.color = UIStyle.CardEdge;
            // The whole row is the touch target.
            well.raycastTarget = true;

            HorizontalLayoutGroup group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(34, 22, 0, 0);
            group.spacing = 22f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            TextMeshProUGUI icon = UIStyle.CreateIcon("Icon", rt, onGlyph, 64f, UIStyle.ValueSlate);
            LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 64f;
            iconLayout.preferredHeight = 64f;

            TextMeshProUGUI caption = UIStyle.CreateText("Label", rt, label, 50f, UIStyle.ValueSlate,
                TextAlignmentOptions.Left);
            caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            RectTransform chip = UIStyle.CreateRect("State", rt);
            Image chipImage = chip.gameObject.AddComponent<Image>();
            chipImage.sprite = UIStyle.PillSprite;
            chipImage.type = Image.Type.Sliced;
            chipImage.color = UIStyle.Green;
            chipImage.raycastTarget = false;
            LayoutElement chipLayout = chip.gameObject.AddComponent<LayoutElement>();
            chipLayout.preferredWidth = 170f;
            chipLayout.preferredHeight = 76f;

            TextMeshProUGUI state = UIStyle.CreateText("Value", chip, "ON", 46f, UIStyle.ButtonInk);
            UIStyle.Stretch((RectTransform)state.transform);

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = well;
            // The chip carries the state; tinting the well as well would fight its colour.
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            // No face to sink (content left empty) - this just supplies the switch sound and haptic.
            rt.gameObject.AddComponent<UIButtonFeel>().sound = UIButtonFeel.ClickSound.Toggle;

            row.FindPropertyRelative("root").objectReferenceValue = rt.gameObject;
            row.FindPropertyRelative("button").objectReferenceValue = button;
            row.FindPropertyRelative("icon").objectReferenceValue = icon;
            row.FindPropertyRelative("state").objectReferenceValue = state;
            row.FindPropertyRelative("stateBackground").objectReferenceValue = chipImage;
            row.FindPropertyRelative("onGlyph").stringValue = onGlyph;
            row.FindPropertyRelative("offGlyph").stringValue = offGlyph;
            return rt;
        }

        // ------------------------------------------------------------- Loading screen

        /// <summary>
        /// Persistent overlay driven by SceneLoader, not UIManager. It carries its own Screen
        /// Space - Overlay canvas because it outlives every scene's camera and UI canvas.
        /// </summary>
        private static void BuildLoadingScreenPrefab()
        {
            if (!Directory.Exists(LoadingScreenDir))
            {
                Directory.CreateDirectory(LoadingScreenDir);
                AssetDatabase.Refresh();
            }

            GameObject go = new GameObject("LoadingScreen",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform root = (RectTransform)go.transform;

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above every scene canvas, including the UI Canvas panels.
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1980f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Match height: the layout is always 1080 units tall and wide phones (2.2:1) just
            // get more room at the sides. 0.5 shrank the usable height on those screens.
            scaler.matchWidthOrHeight = 1f;

            CanvasGroup group = go.AddComponent<CanvasGroup>();
            LoadingScreenView view = go.AddComponent<LoadingScreenView>();

            // Sky fills the screen and blocks every tap to the scene underneath.
            RectTransform sky = UIStyle.Stretch(UIStyle.CreateRect("Sky", root));
            Image skyImage = sky.gameObject.AddComponent<Image>();
            skyImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkyLayerPath);
            skyImage.raycastTarget = true;

            Sprite[] cloudSprites = AssetDatabase.LoadAllAssetsAtPath(CloudLayerPath)
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();

            // (x, y, scale, alpha): a far cloud high and faint, two nearer ones lower.
            var cloudLayout = new[]
            {
                new Vector4(-620f, 330f, 0.32f, 0.8f),
                new Vector4(380f, 250f, 0.45f, 0.95f),
                new Vector4(-80f, -330f, 0.4f, 0.9f),
            };

            RectTransform[] clouds = new RectTransform[cloudLayout.Length];
            for (int i = 0; i < cloudLayout.Length; i++)
            {
                Vector4 c = cloudLayout[i];
                RectTransform cloud = UIStyle.CreateRect("Cloud_" + i, root);
                Image image = cloud.gameObject.AddComponent<Image>();
                if (cloudSprites.Length > 0)
                {
                    image.sprite = cloudSprites[i % cloudSprites.Length];
                    image.SetNativeSize();
                }
                image.color = new Color(1f, 1f, 1f, c.w);
                image.raycastTarget = false;

                UIStyle.Centre(cloud, cloud.sizeDelta.x, cloud.sizeDelta.y);
                cloud.anchoredPosition = new Vector2(c.x, c.y);
                cloud.localScale = new Vector3(c.z, c.z, 1f);
                clouds[i] = cloud;
            }

            RectTransform column = UIStyle.Centre(UIStyle.CreateRect("Column", root), 640f, 0f);
            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 26f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = UIStyle.CreateText("Title", column, "ARCHER", 176f, UIStyle.TitleRed);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UIStyle.UseOutline(title);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;

            RectTransform track = UIStyle.CreateRect("ProgressTrack", column);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = UIStyle.RoundedSprite;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = UIStyle.Card;
            trackImage.raycastTarget = false;
            Shadow trackShadow = track.gameObject.AddComponent<Shadow>();
            trackShadow.effectColor = UIStyle.CardShadow;
            trackShadow.effectDistance = new Vector2(0f, -6f);
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            // Width is driven by anchorMax.x (0..1) from LoadingScreenView.SetProgress, inset
            // so the green sits inside the cream track at every screen width.
            RectTransform fill = UIStyle.CreateRect("Fill", track);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(7f, 7f);
            fill.offsetMax = new Vector2(-7f, -7f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = UIStyle.RoundedSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = UIStyle.Green;
            fillImage.raycastTarget = false;

            TextMeshProUGUI status = UIStyle.CreateText("Status", column, "LOADING...", 48f, UIStyle.OnDark);
            UIStyle.UseShadow(status);
            status.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.FindProperty("progressFill").objectReferenceValue = fill;
            SerializedProperty cloudArray = so.FindProperty("clouds");
            cloudArray.arraySize = clouds.Length;
            for (int i = 0; i < clouds.Length; i++)
                cloudArray.GetArrayElementAtIndex(i).objectReferenceValue = clouds[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAndDiscard(go, LoadingScreenPrefabPath);
        }

        // ------------------------------------------------------------- Building blocks

        /// <summary>Full-bleed root carrying the CanvasGroup the fade transition drives.</summary>
        private static RectTransform CreatePanelRoot(string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = UIStyle.Stretch((RectTransform)go.transform);
            go.AddComponent<CanvasGroup>();

            RectTransform dimmer = UIStyle.Stretch(UIStyle.CreateRect("Dimmer", rt));
            Image dimImage = dimmer.gameObject.AddComponent<Image>();
            dimImage.color = UIStyle.Dim;
            // Blocks taps reaching the gameplay behind the panel.
            dimImage.raycastTarget = true;

            return rt;
        }

        /// <summary>
        /// Centre-anchored card. Width is fixed; height is driven by ContentSizeFitter so the
        /// card grows with its content instead of relying on a hardcoded height that would
        /// clip or leave a gap when labels change.
        /// </summary>
        private static RectTransform CreateCard(RectTransform parent, string title, Color headerColour)
        {
            RectTransform card = UIStyle.Centre(UIStyle.CreateRect("Window", parent), UIStyle.CardWidth, 0f);

            // Soft shadow + cream fill, as children that sit behind the content. The fill takes
            // raycasts so taps on the card's padding don't fall through to the dimmer.
            UIStyle.AddSurface(card, UIStyle.Card, 14f, true);

            // A coloured title plate inset into the top of the card. Outside the layout group:
            // the group's top padding reserves its space.
            RectTransform header = UIStyle.CreateRect("Header", card);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(20f, -HeaderHeight - 20f);
            header.offsetMax = new Vector2(-20f, -20f);
            header.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.sprite = UIStyle.RoundedSprite;
            headerImage.type = Image.Type.Sliced;
            headerImage.color = headerColour;
            headerImage.raycastTarget = false;

            TextMeshProUGUI titleText = UIStyle.CreateText("Title", header, title, 78f, UIStyle.OnDark);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 48f;
            titleText.fontSizeMax = 78f;
            UIStyle.UseShadow(titleText);
            RectTransform titleRt = UIStyle.Stretch((RectTransform)titleText.transform);
            titleRt.offsetMin = new Vector2(24f, 0f);
            titleRt.offsetMax = new Vector2(-24f, 0f);

            VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(48, 48, (int)HeaderHeight + 52, 48);
            group.spacing = 20f;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return card;
        }

        private static RectTransform CreateButtonRow(RectTransform parent)
        {
            RectTransform row = UIStyle.CreateRect("ButtonRow", parent);

            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 24f;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;

            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = UIStyle.ButtonHeight;

            return row;
        }

        private static void AddSpacer(RectTransform parent, float height)
        {
            RectTransform spacer = UIStyle.CreateRect("Spacer", parent);
            spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static void ApplyBasePanelFields(SerializedObject so, RectTransform root, int state)
        {
            so.FindProperty("panelState").enumValueIndex = state;
            so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("rectTransform").objectReferenceValue = root;
            so.FindProperty("hideOnStart").boolValue = true;
            so.FindProperty("defaultTransitionDuration").floatValue = 0.25f;
        }

        private static void SaveAndDiscard(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ Scene wiring

        [MenuItem("Archer/UI/2. Wire GameScene UI")]
        public static void WireGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            GameObject hudCanvasGo = FindInScene(scene, "HUD Canvas");
            if (hudCanvasGo == null)
            {
                Debug.LogError("[UIPanelBuilder] 'HUD Canvas' not found in GameScene - aborting.");
                return;
            }

            Canvas hudCanvas = hudCanvasGo.GetComponent<Canvas>();

            RemoveDeadProtoCanvas(scene);
            SetUpHudPanel(hudCanvasGo);
            RectTransform uiRoot = CreateUiCanvas(hudCanvas);
            InstantiatePanel(GameOverPrefabPath, uiRoot);
            InstantiatePanel(PausePrefabPath, uiRoot);
            // Pause -> SETTINGS needs a panel registered for the Settings state in this scene too.
            InstantiatePanel(SettingsPrefabPath, uiRoot);
            CreateUiManager(StateGameplay);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UIPanelBuilder] GameScene wired and saved.");
        }

        [MenuItem("Archer/UI/3. Wire MainMenu UI")]
        public static void WireMainMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            // The prototype canvas: plain Play/Quit buttons wired by Inspector onClick bindings
            // to MainMenuController. Used once as the template for the new canvas (camera,
            // render mode, scaler) so both scenes scale identically, then removed.
            //
            // On a re-run the prototype is long gone and the UI Canvas already exists; keep it
            // and just swap the panel instances for the freshly built prefabs.
            RectTransform uiRoot;
            GameObject uiCanvasGo = FindInScene(scene, "UI Canvas");
            if (uiCanvasGo != null)
            {
                uiRoot = (RectTransform)uiCanvasGo.transform;
            }
            else
            {
                GameObject protoCanvasGo = FindInScene(scene, "Canvas");
                Canvas protoCanvas = protoCanvasGo != null ? protoCanvasGo.GetComponent<Canvas>() : null;
                if (protoCanvas == null)
                {
                    Debug.LogError("[UIPanelBuilder] Neither 'UI Canvas' nor the prototype 'Canvas' found in MainMenu - aborting.");
                    return;
                }

                uiRoot = CreateUiCanvas(protoCanvas);
                Object.DestroyImmediate(protoCanvasGo);
            }

            InstantiatePanel(MainMenuPrefabPath, uiRoot);
            InstantiatePanel(SettingsPrefabPath, uiRoot);
            CreateUiManager(StateMainMenu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UIPanelBuilder] MainMenu wired and saved.");
        }

        /// <summary>
        /// The old hand-built game-over screen: inactive, localScale 0, buttons with empty
        /// onClick lists and stale absolute anchoring. Superseded by the prefabs above.
        /// </summary>
        private static void RemoveDeadProtoCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject dead = FindInScene(scene, "Canvas (1)");
            if (dead == null) return;

            Object.DestroyImmediate(dead);
            Debug.Log("[UIPanelBuilder] Removed dead proto game-over screen 'Canvas (1)'.");
        }

        private static void SetUpHudPanel(GameObject hudCanvasGo)
        {
            if (hudCanvasGo.GetComponent<CanvasGroup>() == null)
                hudCanvasGo.AddComponent<CanvasGroup>();

            GameplayHUDPanel panel = hudCanvasGo.GetComponent<GameplayHUDPanel>();
            if (panel == null)
                panel = hudCanvasGo.AddComponent<GameplayHUDPanel>();

            Button pauseButton = CreatePauseButton((RectTransform)hudCanvasGo.transform);

            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("panelState").enumValueIndex = StateGameplay;
            so.FindProperty("canvasGroup").objectReferenceValue = hudCanvasGo.GetComponent<CanvasGroup>();
            so.FindProperty("rectTransform").objectReferenceValue = hudCanvasGo.GetComponent<RectTransform>();
            // The HUD is the scene's starting state, so it must NOT hide itself on Awake.
            so.FindProperty("hideOnStart").boolValue = false;
            so.FindProperty("defaultTransitionDuration").floatValue = 0.25f;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreatePauseButton(RectTransform hudRoot)
        {
            // HUDBuilder moves the button into the HUD's safe-area container; look in both places
            // and rebuild it where the rest of the corner HUD lives.
            Transform safeArea = hudRoot.Find(HUDBuilder.SafeAreaName);
            foreach (Transform parent in new[] { hudRoot, safeArea })
            {
                Transform existing = parent != null ? parent.Find("PauseButton") : null;
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
            }

            TextMeshProUGUI ignored;
            Button button = UIStyle.CreateButton("PauseButton", safeArea != null ? safeArea : hudRoot,
                "", UIStyle.Sky, out ignored, UIStyle.ButtonHeight, FeelAssetBuilder.Icon.Pause);
            RectTransform rt = (RectTransform)button.transform;

            // Pinned to the top-right corner: anchor and pivot both at (1,1) so the inset stays
            // constant and the button never drifts or clips as the aspect ratio changes.
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -40f);
            rt.sizeDelta = new Vector2(124f, 124f);

            // The LayoutElement the factory adds is meaningless outside a layout group.
            Object.DestroyImmediate(button.GetComponent<LayoutElement>());

            return button;
        }

        /// <summary>
        /// Panels live on their own canvas, not under the HUD canvas. BaseUIPanel hides by
        /// calling SetActive(false) on its own GameObject, so parenting them under the HUD
        /// would deactivate them the moment the HUD hides - and they would never come back.
        /// </summary>
        private static RectTransform CreateUiCanvas(Canvas hudCanvas)
        {
            GameObject existing = GameObject.Find("UI Canvas");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject go = new GameObject("UI Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = hudCanvas.renderMode;
            canvas.worldCamera = hudCanvas.worldCamera;
            canvas.planeDistance = hudCanvas.planeDistance;
            // Above the HUD, so panels are never occluded by it.
            canvas.sortingOrder = hudCanvas.sortingOrder + 10;

            // Sorting layer matters more than sorting order here. The HUD canvas sits on
            // Default, but gameplay sprites live on the Player and Enemy layers, both of which
            // sort above Default - so a Default-layer canvas gets drawn over by balloons and
            // the bow, dimmer included. The project already defines a UI layer for exactly this.
            if (HasSortingLayer("UI"))
                canvas.sortingLayerName = "UI";
            else
                canvas.sortingLayerID = hudCanvas.sortingLayerID;

            CanvasScaler hudScaler = hudCanvas.GetComponent<CanvasScaler>();
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            if (hudScaler != null)
            {
                // Match the HUD exactly rather than inventing new values, so both canvases
                // scale identically on every device.
                scaler.uiScaleMode = hudScaler.uiScaleMode;
                scaler.referenceResolution = hudScaler.referenceResolution;
                scaler.screenMatchMode = hudScaler.screenMatchMode;
                scaler.matchWidthOrHeight = hudScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = hudScaler.referencePixelsPerUnit;
            }

            return (RectTransform)go.transform;
        }

        private static void InstantiatePanel(string prefabPath, RectTransform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("[UIPanelBuilder] Missing prefab " + prefabPath +
                               " - run 'Build UI Panel Prefabs' first.");
                return;
            }

            Transform existing = parent.Find(prefab.name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            UIStyle.Stretch((RectTransform)instance.transform);
        }

        private static void CreateUiManager(int initialState)
        {
            GameObject existing = GameObject.Find("UIManager");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject go = new GameObject("UIManager");
            UIManager manager = go.AddComponent<UIManager>();

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("defaultTransition").enumValueIndex = TransitionFade;
            // Per scene. GameScene has no MainMenu panel; starting there would log an error and
            // leave currentState at None, which breaks GoBack() out of the pause menu.
            so.FindProperty("initialState").enumValueIndex = initialState;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static bool HasSortingLayer(string name)
        {
            foreach (SortingLayer layer in SortingLayer.layers)
            {
                if (layer.name == name) return true;
            }

            return false;
        }

        private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;

                Transform found = root.transform.Find(name);
                if (found != null) return found.gameObject;
            }

            return null;
        }
    }
}
