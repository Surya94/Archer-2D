using System.Collections.Generic;
using System.IO;
using Archer.Scripts.Manager;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Archer.Editor
{
    /// <summary>
    /// Builds the bonus balloons end to end: placeholder art and sounds, PoolableTypes assets,
    /// the three prefabs, and the GameScene wiring (EnemySpawner.bonusBalloons, a
    /// BonusEffectController, and the Time bonus FreezeOverlay on the HUD canvas).
    ///
    /// Like the UI builders, re-running regenerates everything it owns, so tune here rather than
    /// in the Inspector. The generated PNGs/WAVs in Assets/Art/Bonus are placeholders: replace a
    /// file in place (same name) and re-run to swap in real art or audio.
    /// </summary>
    public static class BonusBalloonBuilder
    {
        private const string ArtDir = "Assets/Art/Bonus";
        private const string PrefabDir = "Assets/Prefabs/Balloons/Bonus";
        private const string PoolTypeDir = "Assets/Resources/PoolableTypes";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        private const string ClockPath = ArtDir + "/clock.png";
        private const string BoltPath = ArtDir + "/bolt.png";
        private const string GlowPath = ArtDir + "/glow.png";
        private const string SnowflakePath = ArtDir + "/snowflake.png";
        private const string FrostPath = ArtDir + "/frost.png";
        private const string FreezeSoundPath = ArtDir + "/freeze.wav";
        private const string ThawSoundPath = ArtDir + "/thaw.wav";
        private const string AdditiveMaterialPath = ArtDir + "/BonusAdditive.mat";

        private const string QuiverPath = "Assets/Art/Elf Archer/1_quiver.png";
        private const string StringPath = "Assets/Art/Balloonpoppinganimation--1g4a618w313b0r3t2n/string-1.png";
        private const string BigBalloonDir = "Assets/Prefabs/Balloons/BigBallons/";
        private const string SoundDataPath = "Assets/Resources/Scriptables/Sounds/BowSoundData.asset";

        // Balloon art: the knot sits ~206px below the centre of the 512px sprite (100 PPU).
        private const float KnotLocalY = -2.0f;
        // Tether-space sizes. The balloon root is scaled 0.3, so world = local * 0.3.
        private const float StringScale = 0.75f;
        private const float PayloadLocalHeight = 3.3f;

        private struct BonusSpec
        {
            public BonusType type;
            public string name;
            public string basePrefab;
            public string payloadPath;
            public Color glow;
        }

        private static readonly BonusSpec[] Specs =
        {
            new BonusSpec { type = BonusType.ExtraArrows, name = "ArrowsBonusBalloon", basePrefab = "GreenBalloon 1.prefab", payloadPath = QuiverPath, glow = new Color(0.55f, 1f, 0.45f, 0.9f) },
            new BonusSpec { type = BonusType.TimeFreeze, name = "TimeBonusBalloon", basePrefab = "PurpleBalloon 1.prefab", payloadPath = ClockPath, glow = new Color(0.6f, 0.95f, 1f, 0.95f) },
            new BonusSpec { type = BonusType.Lightning, name = "LightningBonusBalloon", basePrefab = "BlueBalloon 1.prefab", payloadPath = BoltPath, glow = new Color(0.55f, 0.9f, 1f, 0.9f) },
        };

        [MenuItem("Archer/Gameplay/Build Bonus Balloons")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[BonusBalloonBuilder] Exit play mode first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(ArtDir);
            EnsureFolder(PrefabDir);

            GenerateArt();
            GenerateSounds();
            EnsureAdditiveMaterial();

            List<string> prefabPaths = new List<string>();
            foreach (BonusSpec spec in Specs)
            {
                string path = BuildPrefab(spec);
                if (path == null) return;
                prefabPaths.Add(path);
            }

            AssetDatabase.SaveAssets();

            if (!WireScene(prefabPaths)) return;
            Debug.Log("[BonusBalloonBuilder] Built " + prefabPaths.Count + " bonus balloons and wired GameScene.");
        }

        // ------------------------------------------------------------------ Prefabs

        /// <summary>Builds and saves one prefab; returns its asset path, or null on failure.</summary>
        private static string BuildPrefab(BonusSpec spec)
        {
            GameObject baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BigBalloonDir + spec.basePrefab);
            if (baseAsset == null)
            {
                Debug.LogError("[BonusBalloonBuilder] Missing base prefab " + spec.basePrefab);
                return null;
            }

            // A plain clone, not a prefab instance/variant: the bonus prefab should not change
            // when the normal balloon is tweaked, and vice versa.
            GameObject go = Object.Instantiate(baseAsset);
            go.name = spec.name;

            Enemy source = go.GetComponent<Enemy>();
            BonusBalloon bonus = go.AddComponent<BonusBalloon>();
            bonus.rb = source.rb;
            bonus.animator = source.animator;
            bonus.destoryTime = source.destoryTime;
            // Slower rise than normal balloons (movementSpeed is the tween duration), so there
            // is time to line up the shot; one arrow (damage 10) pops it.
            bonus.movementSpeed = source.movementSpeed + 3;
            bonus.maxHealth = 10;
            bonus.expToGive = 25;
            bonus.bonusType = spec.type;
            bonus.Poolable = EnsurePoolType(spec.name);
            Object.DestroyImmediate(source);

            SpriteRenderer balloonRenderer = go.GetComponent<SpriteRenderer>();
            string layer = balloonRenderer != null ? balloonRenderer.sortingLayerName : "Enemy";
            int order = balloonRenderer != null ? balloonRenderer.sortingOrder : 0;

            // Glow halo behind the balloon body.
            SpriteRenderer glow = CreateChildSprite("Glow", go.transform, Load(GlowPath), layer, order - 2);
            glow.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            glow.transform.localScale = Vector3.one * 2.4f;
            glow.color = spec.glow;
            bonus.glow = glow;

            // Tether pivot at the knot; the string and item hang from it and sway together.
            Transform tether = new GameObject("Tether").transform;
            tether.SetParent(go.transform, false);
            tether.localPosition = new Vector3(0.05f, KnotLocalY, 0f);
            bonus.tether = tether;

            Sprite stringSprite = Load(StringPath);
            SpriteRenderer stringRenderer = CreateChildSprite("String", tether, stringSprite, layer, order - 1);
            float stringLength = stringSprite != null ? stringSprite.bounds.size.y * StringScale : 2.8f;
            stringRenderer.transform.localScale = Vector3.one * StringScale;
            stringRenderer.transform.localPosition = new Vector3(0f, -stringLength * 0.5f, 0f);

            Sprite payloadSprite = Load(spec.payloadPath);
            SpriteRenderer payload = CreateChildSprite("Payload", tether, payloadSprite, layer, order + 1);
            float payloadScale = payloadSprite != null ? PayloadLocalHeight / payloadSprite.bounds.size.y : 1f;
            payload.transform.localScale = Vector3.one * payloadScale;
            // Overlap the string's end slightly so the item reads as tied on.
            payload.transform.localPosition = new Vector3(0f, -stringLength - PayloadLocalHeight * 0.5f + 0.25f, 0f);
            bonus.payload = payload.transform;

            string path = PrefabDir + "/" + spec.name + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved != null ? path : null;
        }

        private static SpriteRenderer CreateChildSprite(string name, Transform parent, Sprite sprite, string layer, int order)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>
        /// One PoolableTypes asset per bonus. The pool is keyed by this asset and casts what it
        /// pops to the requested type, so sharing a key with a normal balloon would make the pool
        /// discard instances instead of reusing them.
        /// </summary>
        private static PoolableTypes EnsurePoolType(string name)
        {
            string path = PoolTypeDir + "/" + name + ".asset";
            PoolableTypes type = AssetDatabase.LoadAssetAtPath<PoolableTypes>(path);
            if (type != null) return type;

            type = ScriptableObject.CreateInstance<PoolableTypes>();
            AssetDatabase.CreateAsset(type, path);
            SerializedObject so = new SerializedObject(type);
            so.FindProperty("description").stringValue = name + " (bonus balloon)";
            so.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        // ------------------------------------------------------------------ Scene

        /// <summary>
        /// Takes asset paths, not objects: OpenScene(Single) unloads assets nothing references,
        /// which turned the just-saved prefab objects into dead references and serialized them
        /// as fileID 0. Everything the scene needs is loaded after the scene is open.
        /// </summary>
        private static bool WireScene(List<string> prefabPaths)
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            List<BonusBalloon> prefabs = new List<BonusBalloon>();
            foreach (string path in prefabPaths)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                BonusBalloon bonus = asset != null ? asset.GetComponent<BonusBalloon>() : null;
                if (bonus == null)
                {
                    Debug.LogError("[BonusBalloonBuilder] Could not load " + path + " - aborting before save.");
                    return false;
                }
                prefabs.Add(bonus);
            }

            Material additive = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);

            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (spawner == null)
            {
                Debug.LogError("[BonusBalloonBuilder] No EnemySpawner in GameScene - aborting before save.");
                return false;
            }

            SerializedObject spawnerSo = new SerializedObject(spawner);
            SerializedProperty list = spawnerSo.FindProperty("bonusBalloons");
            list.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject existing = GameObject.Find("BonusEffects");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject effectsGo = new GameObject("BonusEffects");
            BonusEffectController controller = effectsGo.AddComponent<BonusEffectController>();
            controller.quiverSprite = Load(QuiverPath);
            controller.clockSprite = Load(ClockPath);
            controller.sparkleSprite = Load(GlowPath);
            controller.boltSprite = Load(BoltPath);
            controller.additiveMaterial = additive;

            GamePlayHUD hud = Object.FindAnyObjectByType<GamePlayHUD>(FindObjectsInactive.Include);
            if (hud != null)
                controller.arrowTarget = hud.arrowPill;
            else
                Debug.LogWarning("[BonusBalloonBuilder] No GamePlayHUD found; the quiver will fly to the top-left corner.");

            if (!BuildFreezeOverlay(scene)) return false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        // ------------------------------------------------------------------ Freeze overlay

        private const float HudInset = 40f;
        private const float CountdownWidth = 230f;
        // Below the score pill (120 tall) and the BEST line that hangs under it.
        private const float CountdownTop = HudInset + 120f + 64f;

        /// <summary>
        /// Frost vignette, flash and countdown pill under the HUD canvas, so they draw over the
        /// balloons but below the pause/game-over panels (UI Canvas sorts above HUD Canvas).
        /// </summary>
        private static bool BuildFreezeOverlay(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject hudCanvas = null;
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == "HUD Canvas") { hudCanvas = rootGo; break; }
            }
            if (hudCanvas == null)
            {
                Debug.LogError("[BonusBalloonBuilder] 'HUD Canvas' not found in GameScene - aborting before save.");
                return false;
            }

            RectTransform hudRoot = (RectTransform)hudCanvas.transform;
            Transform existing = hudRoot.Find("FreezeOverlay");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            RectTransform root = UIStyle.Stretch(UIStyle.CreateRect("FreezeOverlay", hudRoot));
            // Behind the HUD pills and PauseButton.
            root.SetAsFirstSibling();
            FreezeOverlay overlay = root.gameObject.AddComponent<FreezeOverlay>();

            overlay.frost = CreateFullScreenImage("Frost", root, Load(FrostPath), Color.white);
            overlay.flash = CreateFullScreenImage("Flash", root, null, new Color(0.92f, 0.98f, 1f, 1f));

            RectTransform pill = HUDBuilder.CreatePill("FreezeCountdown", root, CountdownWidth);
            pill.anchorMin = new Vector2(0.5f, 1f);
            pill.anchorMax = new Vector2(0.5f, 1f);
            pill.pivot = new Vector2(0.5f, 1f);
            pill.anchoredPosition = new Vector2(0f, -CountdownTop);
            // CreatePill sends it to the back; it must draw over the frost.
            pill.SetAsLastSibling();
            // Icy rather than cream, so it reads as part of the freeze, not a permanent HUD item.
            pill.GetComponent<Image>().color = new Color32(0xE4, 0xF6, 0xFF, 0xFF);

            RectTransform icon = UIStyle.CreateRect("Snowflake", pill);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = Load(SnowflakePath);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(70f, 0f);
            icon.sizeDelta = new Vector2(84f, 84f);

            TextMeshProUGUI text = UIStyle.CreateText("Seconds", pill, "5", 72f, UIStyle.ValueSlate);
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform textRt = (RectTransform)text.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(120f, 0f);
            textRt.offsetMax = new Vector2(-16f, 0f);

            overlay.countdownPill = pill;
            overlay.countdownIcon = icon;
            overlay.countdownText = text;
            overlay.normalColour = UIStyle.ValueSlate;
            overlay.warningColour = UIStyle.TitleRed;

            // Inactive until a freeze, so nothing shows in the editor or on the first frame.
            overlay.frost.gameObject.SetActive(false);
            overlay.flash.gameObject.SetActive(false);
            pill.gameObject.SetActive(false);
            return true;
        }

        private static Image CreateFullScreenImage(string name, RectTransform parent, Sprite sprite, Color colour)
        {
            RectTransform rt = UIStyle.Stretch(UIStyle.CreateRect(name, parent));
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            // Display-only: must not swallow the drag that aims the bow.
            image.raycastTarget = false;
            return image;
        }

        // ------------------------------------------------------------------ Sounds

        private const int SampleRate = 44100;

        private static void GenerateSounds()
        {
            WriteWav(FreezeSoundPath, SynthFreeze());
            WriteWav(ThawSoundPath, SynthThaw());
            AssetDatabase.Refresh();

            BowSoundManager data = AssetDatabase.LoadAssetAtPath<BowSoundManager>(SoundDataPath);
            if (data == null)
            {
                Debug.LogWarning("[BonusBalloonBuilder] No BowSoundData at " + SoundDataPath + "; freeze sounds not assigned.");
                return;
            }

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("freezeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FreezeSoundPath);
            so.FindProperty("thawSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ThawSoundPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A rising glassy arpeggio over a crackle of ice forming.</summary>
        private static float[] SynthFreeze()
        {
            float[] notes = { 1318.5f, 1760f, 2093f, 2637f, 3136f };
            float[] buffer = new float[(int)(SampleRate * 1.3f)];

            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(SampleRate * n * 0.055f);
                for (int i = start; i < buffer.Length; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 400f);
                    // A slightly inharmonic overtone gives the bell/glass colour.
                    float v = Mathf.Sin(2f * Mathf.PI * notes[n] * t)
                              + 0.35f * Mathf.Sin(2f * Mathf.PI * notes[n] * 2.76f * t);
                    buffer[i] += v * env * 0.16f;
                }
            }

            AddCrackle(buffer, new System.Random(7), 0f, 0.55f, 60, 0.35f);
            return buffer;
        }

        /// <summary>A short crack and a falling tinkle as the ice breaks.</summary>
        private static float[] SynthThaw()
        {
            float[] notes = { 2637f, 2093f, 1568f };
            float[] buffer = new float[(int)(SampleRate * 0.7f)];

            AddCrackle(buffer, new System.Random(11), 0f, 0.18f, 45, 0.55f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(SampleRate * (0.05f + n * 0.07f));
                for (int i = start; i < buffer.Length; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 400f);
                    buffer[i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * env * 0.14f;
                }
            }

            return buffer;
        }

        /// <summary>Tiny high-passed noise clicks scattered over [from, to) seconds.</summary>
        private static void AddCrackle(float[] buffer, System.Random rng, float from, float to, int clicks, float gain)
        {
            for (int c = 0; c < clicks; c++)
            {
                float at = from + (float)rng.NextDouble() * (to - from);
                int start = (int)(at * SampleRate);
                int len = (int)(SampleRate * (0.002f + 0.006f * (float)rng.NextDouble()));
                // Later clicks are quieter, so the crackle settles.
                float amp = gain * (1f - 0.7f * (at - from) / (to - from)) * (0.4f + 0.6f * (float)rng.NextDouble());
                float prev = 0f;
                for (int i = 0; i < len && start + i < buffer.Length; i++)
                {
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    buffer[start + i] += (noise - prev) * amp * Mathf.Exp(-i / (len * 0.3f));
                    prev = noise;
                }
            }
        }

        /// <summary>16-bit mono PCM WAV, normalised to about -1 dBFS.</summary>
        private static void WriteWav(string path, float[] samples)
        {
            float peak = 0.0001f;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            float scale = 0.89f / peak;

            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                int dataBytes = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);       // PCM
                w.Write((short)1);       // mono
                w.Write(SampleRate);
                w.Write(SampleRate * 2); // byte rate
                w.Write((short)2);       // block align
                w.Write((short)16);      // bits per sample
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (float s in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * scale * short.MaxValue), short.MinValue, short.MaxValue));
            }
        }

        // ------------------------------------------------------------------ Art

        private static Material EnsureAdditiveMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);
            if (material != null) return material;

            // A material asset (rather than Shader.Find at runtime) makes the build include the
            // shader. Additive gives the bolts and thaw sparkles their glow.
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
            {
                Debug.LogWarning("[BonusBalloonBuilder] Additive particle shader not found; falling back to Sprites/Default.");
                shader = Shader.Find("Sprites/Default");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, AdditiveMaterialPath);
            return material;
        }

        private static void GenerateArt()
        {
            WritePng(ClockPath, DrawClock(256));
            WritePng(BoltPath, DrawBolt(256));
            WritePng(GlowPath, DrawGlow(256));
            WritePng(SnowflakePath, DrawSnowflake(256));
            WritePng(FrostPath, DrawFrostVignette(512));

            AssetDatabase.Refresh();
            ConfigureSprite(ClockPath, true);
            ConfigureSprite(BoltPath, true);
            ConfigureSprite(GlowPath, true);
            ConfigureSprite(SnowflakePath, true);
            ConfigureSprite(FrostPath, true);
        }

        private static void ConfigureSprite(string path, bool alphaIsTransparency)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void WritePng(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Sprite Load(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning("[BonusBalloonBuilder] Sprite not found at " + path);
            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ Placeholder drawing
        // Tiny signed-distance rasteriser: each shape is a distance function, antialiased over
        // ~1.5px and alpha-blended onto the canvas. Coordinates are pixels, y up.

        private static readonly Color Ink = new Color32(0x2A, 0x1E, 0x14, 0xFF);

        private static readonly Color IceDeep = new Color32(0x2B, 0x7F, 0xC4, 0xFF);
        private static readonly Color IceLight = new Color32(0xD8, 0xF3, 0xFF, 0xFF);

        /// <summary>A stopwatch: icy-blue rim, pale face, ticks and hands.</summary>
        private static Texture2D DrawClock(int size)
        {
            Color[] px = new Color[size * size];
            Vector2 centre = new Vector2(128f, 112f);

            // Crown and stem, drawn first so the rim covers their roots.
            Stamp(px, size, p => BoxDistance(p, new Vector2(128f, 214f), new Vector2(10f, 14f)) - 4f, Ink);
            Stamp(px, size, p => BoxDistance(p, new Vector2(128f, 236f), new Vector2(26f, 8f)) - 5f, Ink);
            Stamp(px, size, p => BoxDistance(p, new Vector2(128f, 236f), new Vector2(22f, 4f)) - 3f, IceDeep);
            // Side button.
            Vector2 side = centre + new Vector2(Mathf.Cos(0.8f), Mathf.Sin(0.8f)) * 104f;
            Stamp(px, size, p => SegmentDistance(p, centre, side) - 10f, Ink);

            // Rim and face.
            Stamp(px, size, p => Vector2.Distance(p, centre) - 98f, Ink);
            Stamp(px, size, p => Vector2.Distance(p, centre) - 90f, IceDeep);
            Stamp(px, size, p => Vector2.Distance(p, centre) - 74f, Ink);
            Stamp(px, size, p => Vector2.Distance(p, centre) - 70f, new Color32(0xF4, 0xFB, 0xFF, 0xFF));

            // Twelve ticks, the quarters longer.
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                bool quarter = i % 3 == 0;
                Vector2 t0 = centre + dir * (quarter ? 50f : 58f), t1 = centre + dir * 64f;
                float width = quarter ? 4f : 2.5f;
                Stamp(px, size, p => SegmentDistance(p, t0, t1) - width, Ink);
            }

            // Hands and hub.
            Vector2 minute = centre + new Vector2(Mathf.Sin(-0.5f), Mathf.Cos(-0.5f)) * 54f;
            Vector2 hour = centre + new Vector2(Mathf.Sin(1.9f), Mathf.Cos(1.9f)) * 34f;
            Stamp(px, size, p => SegmentDistance(p, centre, minute) - 4.5f, Ink);
            Stamp(px, size, p => SegmentDistance(p, centre, hour) - 6f, Ink);
            Stamp(px, size, p => Vector2.Distance(p, centre) - 9f, IceDeep);

            // Glint on the rim.
            Stamp(px, size, p => EllipseDistance(p, centre + new Vector2(-58f, 58f), new Vector2(16f, 8f), -0.8f), new Color(1f, 1f, 1f, 0.8f));

            return ToTexture(px, size);
        }

        /// <summary>Six-armed snowflake with side branches: blue outline under a white core.</summary>
        private static Texture2D DrawSnowflake(int size)
        {
            Color[] px = new Color[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            List<Vector2[]> segments = new List<Vector2[]>();
            for (int arm = 0; arm < 6; arm++)
            {
                float a = arm * Mathf.PI / 3f + Mathf.PI * 0.5f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                segments.Add(new[] { c, c + dir * 112f });
                foreach (float along in new[] { 52f, 82f })
                {
                    Vector2 root = c + dir * along;
                    float len = along < 60f ? 34f : 24f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float b = a + s * Mathf.PI / 3f;
                        segments.Add(new[] { root, root + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * len });
                    }
                }
            }

            Sdf arms = p =>
            {
                float d = float.MaxValue;
                foreach (Vector2[] seg in segments) d = Mathf.Min(d, SegmentDistance(p, seg[0], seg[1]));
                return d;
            };
            Stamp(px, size, p => arms(p) - 13f, IceDeep);
            Stamp(px, size, p => arms(p) - 6f, Color.white);
            Stamp(px, size, p => Vector2.Distance(p, c) - 20f, IceDeep);
            Stamp(px, size, p => Vector2.Distance(p, c) - 13f, IceLight);

            return ToTexture(px, size);
        }

        /// <summary>
        /// Full-screen frost: clear in the middle, icy at the edges, with crystal "ferns" growing
        /// in from the border. Stretched over the screen, so the exact aspect doesn't matter.
        /// </summary>
        private static Texture2D DrawFrostVignette(int size)
        {
            Color[] px = new Color[size * size];
            Color frost = new Color(0.86f, 0.95f, 1f, 1f);

            // Soft edge band, a little thicker in the corners (superellipse distance).
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 1.02f, d)) * 0.7f;
                px[y * size + x] = new Color(frost.r, frost.g, frost.b, a);
            }

            // Crystal ferns: branching strokes growing inward from random border points. Seeded,
            // so re-running the builder gives the same texture.
            System.Random rng = new System.Random(1234);
            Color fern = new Color(1f, 1f, 1f, 0.6f);
            Vector2 middle = new Vector2(size * 0.5f, size * 0.5f);
            for (int i = 0; i < 45; i++)
            {
                float t = (float)rng.NextDouble();
                int edge = rng.Next(4);
                Vector2 start = edge == 0 ? new Vector2(t * size, 0f)
                    : edge == 1 ? new Vector2(t * size, size)
                    : edge == 2 ? new Vector2(0f, t * size)
                    : new Vector2(size, t * size);
                Vector2 inward = (middle - start).normalized;
                float angle = Mathf.Atan2(inward.y, inward.x) + ((float)rng.NextDouble() - 0.5f) * 1.2f;
                float length = 25f + (float)rng.NextDouble() * 35f;
                DrawFern(px, size, rng, start, angle, length, 1.6f, fern, 2);
            }

            return ToTexture(px, size);
        }

        private static void DrawFern(Color[] px, int size, System.Random rng, Vector2 from, float angle,
            float length, float width, Color colour, int depth)
        {
            Vector2 to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
            StampSegment(px, size, from, to, width, colour);
            if (depth == 0) return;

            int branches = 2 + rng.Next(3);
            for (int b = 0; b < branches; b++)
            {
                float along = 0.25f + (float)rng.NextDouble() * 0.6f;
                float side = rng.Next(2) == 0 ? -1f : 1f;
                DrawFern(px, size, rng, Vector2.Lerp(from, to, along),
                    angle + side * (0.6f + (float)rng.NextDouble() * 0.4f),
                    length * (0.3f + (float)rng.NextDouble() * 0.25f), width * 0.7f, colour, depth - 1);
            }
        }

        /// <summary>
        /// Stamp for one line segment, limited to its bounding box - a full-canvas Stamp per fern
        /// stroke would be hundreds of 512x512 passes.
        /// </summary>
        private static void StampSegment(Color[] px, int size, Vector2 a, Vector2 b, float radius, Color colour)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 2f));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 2f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 2f));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 2f));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float d = SegmentDistance(new Vector2(x + 0.5f, y + 0.5f), a, b) - radius;
                float coverage = Mathf.Clamp01(0.5f - d / 1.5f);
                if (coverage <= 0f) continue;

                float alpha = coverage * colour.a;
                Color dst = px[y * size + x];
                float outA = alpha + dst.a * (1f - alpha);
                Color rgb = outA > 0f ? (colour * alpha + dst * dst.a * (1f - alpha)) / outA : Color.clear;
                rgb.a = outA;
                px[y * size + x] = rgb;
            }
        }

        private static Texture2D DrawBolt(int size)
        {
            Color[] px = new Color[size * size];
            Vector2[] bolt =
            {
                new Vector2(158f, 248f), new Vector2(66f, 118f), new Vector2(122f, 118f),
                new Vector2(92f, 8f), new Vector2(194f, 150f), new Vector2(136f, 150f),
                new Vector2(184f, 248f),
            };

            Stamp(px, size, p => PolygonDistance(p, bolt) - 6f, Ink);
            Stamp(px, size, p => PolygonDistance(p, bolt) + 2f, new Color32(0xFF, 0xD2, 0x3F, 0xFF));
            // Light inner face for a bit of shape.
            Stamp(px, size, p => Mathf.Max(PolygonDistance(p, bolt) + 10f, p.x - 128f), new Color32(0xFF, 0xEC, 0x8A, 0xFF));

            return ToTexture(px, size);
        }

        private static Texture2D DrawGlow(int size)
        {
            Color[] px = new Color[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                // The balloon body covers the inner ~60% of the halo, so the visible part is the
                // outer ring: keep it bright out to there, then fade (a plain (1-d)^2 left the
                // ring nearly transparent - the glow was invisible in play).
                float a = Mathf.Pow(Mathf.Clamp01((1f - d) / 0.45f), 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            return ToTexture(px, size);
        }

        private delegate float Sdf(Vector2 p);

        /// <summary>Alpha-blend a shape (negative distance = inside) onto the canvas.</summary>
        private static void Stamp(Color[] px, int size, Sdf sdf, Color colour)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = sdf(new Vector2(x + 0.5f, y + 0.5f));
                float coverage = Mathf.Clamp01(0.5f - d / 1.5f);
                if (coverage <= 0f) continue;

                float a = coverage * colour.a;
                Color dst = px[y * size + x];
                float outA = a + dst.a * (1f - a);
                Color rgb = outA > 0f ? (colour * a + dst * dst.a * (1f - a)) / outA : Color.clear;
                rgb.a = outA;
                px[y * size + x] = rgb;
            }
        }

        private static Texture2D ToTexture(Color[] px, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(px);
            texture.Apply();
            return texture;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float BoxDistance(Vector2 p, Vector2 centre, Vector2 halfSize)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - halfSize;
            Vector2 outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        /// <summary>Approximate distance to a rotated ellipse - fine for a highlight blob.</summary>
        private static float EllipseDistance(Vector2 p, Vector2 centre, Vector2 radii, float angle)
        {
            Vector2 q = p - centre;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            q = new Vector2(c * q.x + s * q.y, -s * q.x + c * q.y);
            float k = new Vector2(q.x / radii.x, q.y / radii.y).magnitude;
            return (k - 1f) * Mathf.Min(radii.x, radii.y);
        }

        /// <summary>Signed distance to a simple polygon (even-odd inside test).</summary>
        private static float PolygonDistance(Vector2 p, Vector2[] poly)
        {
            float distance = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                distance = Mathf.Min(distance, SegmentDistance(p, poly[j], poly[i]));
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }

            return inside ? -distance : distance;
        }
    }
}
