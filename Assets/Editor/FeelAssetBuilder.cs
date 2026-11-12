using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using Archer.Scripts.Manager.Feel;

namespace Archer.Editor
{
    /// <summary>
    /// Generates the art-side assets the polish pass needs, so none of them are hand-authored
    /// binaries: the TMP font assets (display font + icon font), the UI sprite kit, the pop
    /// particle prefab and the synthesized sound effects. Safe to re-run: existing font assets
    /// are kept (their GUIDs are referenced by every UI prefab), everything else is rebuilt.
    ///
    /// Run this before the UI builders (Archer/UI/1..4), which load what it produces.
    /// </summary>
    public static class FeelAssetBuilder
    {
        public const string FontDir = "Assets/Art/UI/Fonts";
        public const string KitDir = "Assets/Art/UI/Kit";
        public const string FxDir = "Assets/Art/FX";
        public const string SoundDir = "Assets/Sound/Generated";

        public const string DisplayFontPath = FontDir + "/LilitaOne SDF.asset";
        public const string DisplayShadowMaterialPath = FontDir + "/LilitaOne SDF - Shadow.mat";
        public const string DisplayOutlineMaterialPath = FontDir + "/LilitaOne SDF - Outline.mat";
        public const string IconFontPath = FontDir + "/MaterialIconsRound SDF.asset";
        public const string PopBurstPrefabPath = "Assets/Prefabs/PopBurst.prefab";

        private const string DisplayTtfPath = FontDir + "/LilitaOne-Regular.ttf";
        private const string IconOtfPath = FontDir + "/MaterialIconsRound-Regular.otf";
        private const string FallbackFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string PopPoolablePath = "Assets/Resources/PoolableTypes/PopBurst.asset";
        private const string SoundDataPath = "Assets/Resources/Scriptables/Sounds/BowSoundData.asset";
        private const string ArrowVfxPrefabPath = "Assets/Prefabs/Adding Arrow VFX.prefab";

        /// <summary>Material Icons Round code points used by the UI builders.</summary>
        public static class Icon
        {
            public const string Pause = "";
            public const string Play = "";
            public const string Home = "";
            public const string Restart = "";
            public const string Settings = "";
            public const string Back = "";
            public const string SoundOn = "";
            public const string SoundOff = "";
            public const string MusicOn = "";
            public const string MusicOff = "";
            public const string Vibration = "";
            public const string Trophy = "";
            public const string Gift = "";
            public const string Video = "";
            public const string Star = "";
            public const string Touch = "";
            public const string Bolt = "";
            public const string Fire = "";
            public const string Cart = "";
            public const string Block = "";
            public const string Lock = "";
            public const string Close = "";
            public const string Check = "";
            public const string Exit = "";

            public static readonly string[] All =
            {
                Pause, Play, Home, Restart, Settings, Back, SoundOn, SoundOff, MusicOn, MusicOff,
                Vibration, Trophy, Gift, Video, Star, Touch, Bolt, Fire, Cart, Block, Lock, Close,
                Check, Exit,
            };
        }

        [MenuItem("Archer/FX/Build Feel Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[FeelAssetBuilder] Exit play mode before building feel assets.");
                return;
            }

            foreach (string dir in new[] { FontDir, KitDir, FxDir, SoundDir })
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            AssetDatabase.Refresh();

            BuildFonts();
            BuildSpriteKit();
            BuildSounds();
            BuildPopBurst();
            RestyleArrowPopup();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FeelAssetBuilder] Feel assets built.");
        }

        // ================================================================== Fonts

        private static void BuildFonts()
        {
            // ASCII plus the few extra glyphs the UI uses.
            var display = new List<uint>();
            for (uint c = 32; c <= 126; c++) display.Add(c);
            display.AddRange(new uint[] { 0x00D7, 0x2019, 0x2013, 0x2026 });

            TMP_FontAsset displayFont = BuildFont(DisplayTtfPath, DisplayFontPath, display.ToArray(), 72, 8, 1024);
            if (displayFont != null)
            {
                TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
                if (fallback != null)
                {
                    if (displayFont.fallbackFontAssetTable == null)
                        displayFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (!displayFont.fallbackFontAssetTable.Contains(fallback))
                        displayFont.fallbackFontAssetTable.Add(fallback);
                    EditorUtility.SetDirty(displayFont);
                }

                BuildMaterialPreset(displayFont, DisplayShadowMaterialPath, material =>
                {
                    material.EnableKeyword("UNDERLAY_ON");
                    material.SetColor("_UnderlayColor", new Color(0.16f, 0.11f, 0.06f, 0.75f));
                    material.SetFloat("_UnderlayOffsetX", 0f);
                    material.SetFloat("_UnderlayOffsetY", -0.55f);
                    material.SetFloat("_UnderlayDilate", 0.15f);
                    material.SetFloat("_UnderlaySoftness", 0.1f);
                });

                BuildMaterialPreset(displayFont, DisplayOutlineMaterialPath, material =>
                {
                    material.EnableKeyword("OUTLINE_ON");
                    material.SetColor("_OutlineColor", new Color32(0x3A, 0x2A, 0x18, 0xFF));
                    material.SetFloat("_OutlineWidth", 0.22f);
                    material.SetFloat("_FaceDilate", 0.12f);
                    material.EnableKeyword("UNDERLAY_ON");
                    material.SetColor("_UnderlayColor", new Color32(0x3A, 0x2A, 0x18, 0xFF));
                    material.SetFloat("_UnderlayOffsetX", 0f);
                    material.SetFloat("_UnderlayOffsetY", -0.75f);
                    material.SetFloat("_UnderlayDilate", 0.35f);
                    material.SetFloat("_UnderlaySoftness", 0f);
                });
            }

            var icons = new List<uint>();
            foreach (string glyph in Icon.All) icons.Add(glyph[0]);
            BuildFont(IconOtfPath, IconFontPath, icons.ToArray(), 96, 10, 1024);
        }

        /// <summary>
        /// A static SDF font asset holding exactly the listed characters. Static, so the atlas
        /// is baked here and the source font file is not needed (or rewritten) at runtime.
        /// </summary>
        private static TMP_FontAsset BuildFont(string sourcePath, string assetPath, uint[] unicodes,
            int pointSize, int padding, int atlasSize)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError("[FeelAssetBuilder] Font not found: " + sourcePath);
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, pointSize, padding,
                GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            if (asset == null)
            {
                Debug.LogError("[FeelAssetBuilder] Could not create a font asset from " + sourcePath);
                return null;
            }

            asset.TryAddCharacters(unicodes, out uint[] missing);
            if (missing != null && missing.Length > 0)
            {
                var list = new List<string>();
                foreach (uint m in missing) list.Add("U+" + m.ToString("X4"));
                Debug.LogWarning("[FeelAssetBuilder] " + Path.GetFileName(sourcePath) +
                                 " has no glyph for: " + string.Join(", ", list));
            }

            string name = Path.GetFileNameWithoutExtension(assetPath);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, assetPath);

            asset.material.name = name + " Material";
            asset.atlasTexture.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);

            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void BuildMaterialPreset(TMP_FontAsset font, string path, Action<Material> configure)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(font.material);
                material.shaderKeywords = font.material.shaderKeywords;
            }

            configure(material);
            EditorUtility.SetDirty(material);
        }

        // ================================================================== Sprite kit

        private static void BuildSpriteKit()
        {
            // 9-slice rounded rectangle: cards, buttons, pills. Corner radius 40px at 100 ppu.
            WriteSprite("RoundRect", 128, 128, (x, y) => RoundRect(x, y, 128, 128, 40f, 1f),
                new Vector4(48, 48, 48, 48));

            // Fully round ends, for small tags and the toggle state chips.
            WriteSprite("Pill", 128, 128, (x, y) => RoundRect(x, y, 128, 128, 63f, 1f),
                new Vector4(63, 63, 63, 63));

            // Soft drop shadow: a rounded rect blurred outward over 20px.
            WriteSprite("Shadow", 168, 168, (x, y) =>
            {
                float d = RoundRectDistance(x, y, 168, 168, 40f, 22f);
                float a = Mathf.Clamp01(1f - d / 20f);
                return a * a * (3f - 2f * a);
            }, new Vector4(70, 70, 70, 70));

            WriteSprite("Circle", 128, 128, (x, y) =>
                Mathf.Clamp01(62.5f - Radius(x, y, 128)), Vector4.zero);

            WriteSprite("Glow", 128, 128, (x, y) =>
            {
                float a = Mathf.Clamp01(1f - Radius(x, y, 128) / 64f);
                return a * a;
            }, Vector4.zero);

            WriteSprite("Ring", 128, 128, (x, y) =>
            {
                float r = Radius(x, y, 128);
                return Mathf.Clamp01(Mathf.Min(r - 48f, 60f - r));
            }, Vector4.zero);

            // A scrap of rubber for the pop particles.
            WriteSprite("Shard", 32, 32, (x, y) => RoundRect(x, y, 32, 32, 6f, 4f), Vector4.zero);
        }

        public static Sprite LoadKitSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(KitDir + "/" + name + ".png");

        private static float Radius(int x, int y, int size)
        {
            float px = x + 0.5f - size * 0.5f;
            float py = y + 0.5f - size * 0.5f;
            return Mathf.Sqrt(px * px + py * py);
        }

        /// <summary>Signed distance from the edge of a rounded rectangle (negative inside).</summary>
        private static float RoundRectDistance(int x, int y, int w, int h, float radius, float inset)
        {
            float px = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - inset - radius);
            float py = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - inset - radius);
            float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(px, py), 0f);
            return outside + inside - radius;
        }

        private static float RoundRect(int x, int y, int w, int h, float radius, float inset) =>
            Mathf.Clamp01(0.5f - RoundRectDistance(x, y, w, h, radius, inset));

        /// <summary>White texture whose alpha comes from the callback, imported as a sprite.</summary>
        private static void WriteSprite(string name, int width, int height, Func<int, int, float> alpha,
            Vector4 border)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // RGB stays white everywhere so tinted edges never pick up a dark fringe.
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            string path = KitDir + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ================================================================== Pop burst

        private static void BuildPopBurst()
        {
            Material shardMaterial = ParticleMaterial("PopShard", "Shard");
            Material ringMaterial = ParticleMaterial("PopRing", "Ring");
            Material flashMaterial = ParticleMaterial("PopFlash", "Glow");

            PoolableTypes poolable = AssetDatabase.LoadAssetAtPath<PoolableTypes>(PopPoolablePath);
            if (poolable == null)
            {
                poolable = ScriptableObject.CreateInstance<PoolableTypes>();
                AssetDatabase.CreateAsset(poolable, PopPoolablePath);
                SerializedObject poolSo = new SerializedObject(poolable);
                poolSo.FindProperty("description").stringValue = "Balloon pop particle burst";
                poolSo.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject root = new GameObject("PopBurst");
            PopBurstFX fx = root.AddComponent<PopBurstFX>();
            fx.Poolable = poolable;

            // --- Shards: scraps of the balloon thrown outward, then dropped by gravity.
            ParticleSystem shards = root.AddComponent<ParticleSystem>();
            {
                ParticleSystem.MainModule main = shards.main;
                main.duration = 1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 1.3f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = 20;

                ParticleSystem.EmissionModule emission = shards.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

                ParticleSystem.ShapeModule shape = shards.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.22f;

                ParticleSystem.LimitVelocityOverLifetimeModule limit = shards.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1.5f;
                limit.dampen = 0.12f;

                ParticleSystem.RotationOverLifetimeModule rotation = shards.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

                ParticleSystem.SizeOverLifetimeModule size = shards.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

                ParticleSystem.ColorOverLifetimeModule colour = shards.colorOverLifetime;
                colour.enabled = true;
                colour.color = FadeOut(1f, 0.6f);

                ConfigureRenderer(shards, shardMaterial, 20);
            }

            // --- Ring: one expanding shockwave.
            ParticleSystem ring = CreateChildSystem(root.transform, "Ring");
            {
                ParticleSystem.MainModule main = ring.main;
                main.startLifetime = 0.28f;
                main.startSize = 0.55f;
                ConfigureSingleParticle(ring);

                ParticleSystem.SizeOverLifetimeModule size = ring.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 1f, 0f, 9f), new Keyframe(1f, 3.6f, 0f, 0f)));

                ParticleSystem.ColorOverLifetimeModule colour = ring.colorOverLifetime;
                colour.enabled = true;
                colour.color = FadeOut(0.9f, 0.15f);

                ConfigureRenderer(ring, ringMaterial, 19);
            }

            // --- Flash: a soft white blink at the moment of the pop.
            ParticleSystem flash = CreateChildSystem(root.transform, "Flash");
            {
                ParticleSystem.MainModule main = flash.main;
                main.startLifetime = 0.14f;
                main.startSize = 1.7f;
                main.startColor = new Color(1f, 1f, 1f, 0.85f);
                ConfigureSingleParticle(flash);

                ParticleSystem.ColorOverLifetimeModule colour = flash.colorOverLifetime;
                colour.enabled = true;
                colour.color = FadeOut(1f, 0f);

                ConfigureRenderer(flash, flashMaterial, 18);
            }

            fx.shards = shards;
            fx.ring = ring;
            fx.flash = flash;
            fx.lifetime = 0.9f;

            PrefabUtility.SaveAsPrefabAsset(root, PopBurstPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static ParticleSystem CreateChildSystem(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<ParticleSystem>();
        }

        private static void ConfigureSingleParticle(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 2;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
        }

        private static void ConfigureRenderer(ParticleSystem system, Material material, int order)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            // Above the balloons (Enemy layer, order 0) and below both UI canvases.
            renderer.sortingLayerName = "Enemy";
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static ParticleSystem.MinMaxGradient FadeOut(float startAlpha, float holdUntil)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(startAlpha, Mathf.Clamp01(holdUntil)),
                    new GradientAlphaKey(0f, 1f),
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Material ParticleMaterial(string name, string kitSprite)
        {
            string path = FxDir + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Sprites/Default: alpha blended, vertex-coloured, unlit - all the particles need.
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            Sprite sprite = LoadKitSprite(kitSprite);
            material.mainTexture = sprite != null ? sprite.texture : null;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ================================================================== Arrow popup

        /// <summary>
        /// The "+1 arrow" popup is an older Animator-driven prefab with a legacy UI Text. Only
        /// its typeface is changed, so its animation clip keeps working untouched.
        /// </summary>
        private static void RestyleArrowPopup()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(DisplayTtfPath);
            if (font == null || !File.Exists(ArrowVfxPrefabPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(ArrowVfxPrefabPath);
            bool changed = false;
            foreach (UnityEngine.UI.Text text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                if (text.font == font) continue;
                text.font = font;
                changed = true;
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, ArrowVfxPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ================================================================== Sounds

        /// <summary>
        /// Synthesized effects for every slot that has no hand-picked clip. A slot is only
        /// (re)assigned when it is empty or already points at a generated file, so dropping a
        /// real recording into BowSoundData survives a rebuild.
        /// </summary>
        private static void BuildSounds()
        {
            // Only the slots the Kenney packs have no match for (see AssignKenneySounds): there
            // is no balloon pop, electrical zap or musical sting in them.
            var clips = new Dictionary<string, string>
            {
                { "popSound", WriteClip("pop", Synth.Pop(false)) },
                { "bigPopSound", WriteClip("pop_big", Synth.Pop(true)) },
                { "lightningZapSound", WriteClip("lightning_zap", Synth.Zap()) },
                { "gameOverSound", WriteClip("game_over", Synth.Chime(new[] { 392f, 329.6f, 261.6f }, 1.1f, 0.2f)) },
                { "newBestSound", WriteClip("new_best",
                    Synth.Chime(new[] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 1.5f, 0.1f)) },
            };

            BowSoundManager data = AssetDatabase.LoadAssetAtPath<BowSoundManager>(SoundDataPath);
            if (data == null)
            {
                Debug.LogError("[FeelAssetBuilder] " + SoundDataPath + " not found; clips were written but not assigned.");
                return;
            }

            SerializedObject so = new SerializedObject(data);
            foreach (KeyValuePair<string, string> entry in clips)
            {
                SerializedProperty property = so.FindProperty(entry.Key);
                if (property == null) continue;

                UnityEngine.Object current = property.objectReferenceValue;
                bool replaceable = current == null ||
                                   AssetDatabase.GetAssetPath(current).StartsWith(SoundDir, StringComparison.Ordinal);
                if (replaceable)
                    property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.Value);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        // ------------------------------------------------------------------ Kenney packs

        private const string KenneyDir = "Assets/Audio";

        /// <summary>BowSoundData slot -> clip from the Kenney CC0 packs under Assets/Audio.</summary>
        private static readonly Dictionary<string, string> KenneyClips = new Dictionary<string, string>
        {
            { "uiClickSound", "kenney_ui-audio/Audio/click1.ogg" },
            { "uiBackSound", "kenney_ui-audio/Audio/switch2.ogg" },
            { "uiToggleSound", "kenney_ui-audio/Audio/switch7.ogg" },
            // An arrow sliding out of the quiver. The loudest of the three takes; the others
            // peak below 0.1 and stay faint even normalised.
            { "arrowGainSound", "kenney_rpg-audio/Audio/drawKnife3.ogg" },
            { "bonusCollectSound", "kenney_rpg-audio/Audio/handleCoins2.ogg" },
            { "missSound", "kenney_impact-sounds/Audio/impactSoft_medium_000.ogg" },
            // The shortest bell strike (0.3s), so rising callout tiers don't smear together.
            { "comboStepSound", "kenney_impact-sounds/Audio/impactBell_heavy_004.ogg" },
        };

        /// <summary>
        /// Point the sound slots at recorded clips from the Kenney packs. Kept as a table and a
        /// menu item so the choice of clip per slot is reviewable and repeatable; swap a path
        /// here and re-run to audition another take.
        /// </summary>
        [MenuItem("Archer/FX/Assign Kenney Sounds")]
        public static void AssignKenneySounds()
        {
            BowSoundManager data = AssetDatabase.LoadAssetAtPath<BowSoundManager>(SoundDataPath);
            if (data == null)
            {
                Debug.LogError("[FeelAssetBuilder] " + SoundDataPath + " not found.");
                return;
            }

            SerializedObject so = new SerializedObject(data);
            int assigned = 0;
            foreach (KeyValuePair<string, string> entry in KenneyClips)
            {
                string path = KenneyDir + "/" + entry.Value;
                SerializedProperty property = so.FindProperty(entry.Key);
                if (property == null)
                {
                    Debug.LogError("[FeelAssetBuilder] BowSoundData has no slot '" + entry.Key + "'.");
                    continue;
                }

                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[FeelAssetBuilder] Kenney clip missing, slot left as it was: " + path);
                    continue;
                }

                // Mono + normalise: the packs are stereo and recorded at very different levels
                // (some peak at 0.05), and Unity only normalises when it downmixes.
                importer.forceToMono = true;
                SerializedObject importerSo = new SerializedObject(importer);
                SerializedProperty normalize = importerSo.FindProperty("m_Normalize");
                if (normalize != null)
                {
                    normalize.boolValue = true;
                    importerSo.ApplyModifiedPropertiesWithoutUndo();
                }
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();

                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                assigned++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[FeelAssetBuilder] Assigned " + assigned + " of " + KenneyClips.Count + " Kenney clips to BowSoundData.");
        }

        private static string WriteClip(string name, float[] samples)
        {
            string path = SoundDir + "/" + name + ".wav";
            File.WriteAllBytes(path, Synth.ToWav(samples));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            // Short one-shots: decompress once at load so playback has no latency.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return path;
        }

        /// <summary>Tiny additive synthesizer - enough for pops, blips and chimes.</summary>
        private static class Synth
        {
            private const int Rate = 44100;

            public static float[] Pop(bool big)
            {
                float length = big ? 0.2f : 0.13f;
                float[] buffer = new float[(int)(Rate * length)];
                var random = new System.Random(big ? 7 : 3);

                float bodyFrom = big ? 150f : 230f;
                float bodyTo = big ? 55f : 80f;
                float noiseDecay = big ? 0.028f : 0.016f;
                float bodyDecay = big ? 0.07f : 0.04f;

                float phase = 0f;
                float low = 0f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    float t = i / (float)Rate;

                    // The snap: bright noise that dies almost instantly, lightly low-passed.
                    float white = (float)(random.NextDouble() * 2.0 - 1.0);
                    low += (white - low) * 0.55f;
                    float snap = low * Mathf.Exp(-t / noiseDecay);

                    // The body: a sine that drops in pitch as the air rushes out.
                    float frequency = Mathf.Lerp(bodyFrom, bodyTo, Mathf.Clamp01(t / (bodyDecay * 2f)));
                    phase += frequency / Rate;
                    float body = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t / bodyDecay);

                    buffer[i] = snap * 0.9f + body * 0.8f;
                }

                return Finish(buffer, 0.001f, 0.01f);
            }

            public static float[] Blip(float from, float to, float length)
            {
                float[] buffer = new float[(int)(Rate * length)];
                float phase = 0f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    float t = i / (float)Rate;
                    float k = t / length;
                    phase += Mathf.Lerp(from, to, k) / Rate;
                    float tone = Mathf.Sin(phase * Mathf.PI * 2f) + 0.25f * Mathf.Sin(phase * Mathf.PI * 4f);
                    buffer[i] = tone * Mathf.Exp(-t / (length * 0.35f));
                }

                return Finish(buffer, 0.002f, 0.01f);
            }

            /// <summary>Bell-like notes played one after another, each ringing over the next.</summary>
            public static float[] Chime(float[] notes, float length, float spacing)
            {
                float[] buffer = new float[(int)(Rate * length)];
                for (int n = 0; n < notes.Length; n++)
                {
                    int start = (int)(n * spacing * Rate);
                    float frequency = notes[n];
                    float decay = Mathf.Max(0.12f, (length - n * spacing) * 0.3f);

                    for (int i = start; i < buffer.Length; i++)
                    {
                        float t = (i - start) / (float)Rate;
                        float envelope = Mathf.Exp(-t / decay) * Mathf.Clamp01(t / 0.004f);
                        // Slightly inharmonic upper partials are what make it a bell, not a beep.
                        float tone = Mathf.Sin(t * frequency * Mathf.PI * 2f)
                                     + 0.45f * Mathf.Sin(t * frequency * 2.01f * Mathf.PI * 2f) * Mathf.Exp(-t / (decay * 0.6f))
                                     + 0.2f * Mathf.Sin(t * frequency * 3.02f * Mathf.PI * 2f) * Mathf.Exp(-t / (decay * 0.35f));
                        buffer[i] += tone * envelope;
                    }
                }

                return Finish(buffer, 0.001f, 0.04f);
            }

            public static float[] Thud()
            {
                float[] buffer = new float[(int)(Rate * 0.16f)];
                float phase = 0f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    float t = i / (float)Rate;
                    phase += Mathf.Lerp(210f, 110f, t / 0.16f) / Rate;
                    buffer[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t / 0.05f);
                }

                return Finish(buffer, 0.004f, 0.02f);
            }

            public static float[] Zap()
            {
                float[] buffer = new float[(int)(Rate * 0.2f)];
                var random = new System.Random(11);
                float phase = 0f;
                float held = 0f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    float t = i / (float)Rate;
                    float k = t / 0.2f;

                    // A falling sawtooth, ring-modulated so it buzzes, plus stepped noise crackle.
                    phase += Mathf.Lerp(1500f, 180f, k * k) / Rate;
                    float saw = (phase % 1f) * 2f - 1f;
                    float buzz = saw * Mathf.Sin(t * 95f * Mathf.PI * 2f);
                    if (i % 6 == 0) held = (float)(random.NextDouble() * 2.0 - 1.0);

                    buffer[i] = (buzz * 0.7f + held * 0.5f) * Mathf.Exp(-t / 0.07f);
                }

                return Finish(buffer, 0.001f, 0.02f);
            }

            /// <summary>Fade the ends (no clicks) and normalise to a common peak.</summary>
            private static float[] Finish(float[] buffer, float fadeIn, float fadeOut)
            {
                int inSamples = Mathf.Max(1, (int)(fadeIn * Rate));
                int outSamples = Mathf.Max(1, (int)(fadeOut * Rate));
                float peak = 0f;

                for (int i = 0; i < buffer.Length; i++)
                {
                    float gain = Mathf.Min(1f, i / (float)inSamples);
                    gain = Mathf.Min(gain, (buffer.Length - 1 - i) / (float)outSamples);
                    buffer[i] *= Mathf.Clamp01(gain);
                    peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
                }

                if (peak > 0f)
                {
                    float scale = 0.85f / peak;
                    for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
                }

                return buffer;
            }

            public static byte[] ToWav(float[] samples)
            {
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int dataSize = samples.Length * 2;
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + dataSize);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16);
                    writer.Write((short)1);       // PCM
                    writer.Write((short)1);       // mono
                    writer.Write(Rate);
                    writer.Write(Rate * 2);       // byte rate
                    writer.Write((short)2);       // block align
                    writer.Write((short)16);      // bits per sample
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(dataSize);

                    foreach (float sample in samples)
                        writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));

                    writer.Flush();
                    return stream.ToArray();
                }
            }
        }
    }
}
