using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Archer.Editor
{
    /// <summary>
    /// Builds the bonus balloons end to end: placeholder art, PoolableTypes assets, the three
    /// prefabs, and the GameScene wiring (EnemySpawner.bonusBalloons + a BonusEffectController).
    ///
    /// Like the UI builders, re-running regenerates everything it owns, so tune here rather than
    /// in the Inspector. The generated PNGs in Assets/Art/Bonus are placeholders: replace a file
    /// in place (same name) and re-run to swap in real art.
    /// </summary>
    public static class BonusBalloonBuilder
    {
        private const string ArtDir = "Assets/Art/Bonus";
        private const string PrefabDir = "Assets/Prefabs/Balloons/Bonus";
        private const string PoolTypeDir = "Assets/Resources/PoolableTypes";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        private const string BombPath = ArtDir + "/bomb.png";
        private const string BoltPath = ArtDir + "/bolt.png";
        private const string GlowPath = ArtDir + "/glow.png";
        private const string BlastPath = ArtDir + "/blast.png";
        private const string AdditiveMaterialPath = ArtDir + "/BonusAdditive.mat";

        private const string QuiverPath = "Assets/Art/Elf Archer/1_quiver.png";
        private const string StringPath = "Assets/Art/Balloonpoppinganimation--1g4a618w313b0r3t2n/string-1.png";
        private const string FeelExplosionPath = "Assets/Feel/MMTools/Accessories/MMVFX/MMParticles/MMParticlesExplosion.png";
        private const string BigBalloonDir = "Assets/Prefabs/Balloons/BigBallons/";

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
            new BonusSpec { type = BonusType.Bomb, name = "BombBonusBalloon", basePrefab = "RedBalloon 1.prefab", payloadPath = BombPath, glow = new Color(1f, 0.6f, 0.35f, 0.9f) },
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
            controller.bombSprite = Load(BombPath);
            controller.boltSprite = Load(BoltPath);
            controller.blastSprite = Load(BlastPath);
            controller.additiveMaterial = additive;

            GamePlayHUD hud = Object.FindAnyObjectByType<GamePlayHUD>(FindObjectsInactive.Include);
            if (hud != null)
                controller.arrowTarget = hud.arrowPill;
            else
                Debug.LogWarning("[BonusBalloonBuilder] No GamePlayHUD found; the quiver will fly to the top-left corner.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        // ------------------------------------------------------------------ Art

        private static Material EnsureAdditiveMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);
            if (material != null) return material;

            // A material asset (rather than Shader.Find at runtime) makes the build include the
            // shader. Additive is needed for the Feel blast texture, which is white-on-black.
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
            WritePng(BombPath, DrawBomb(256));
            WritePng(BoltPath, DrawBolt(256));
            WritePng(GlowPath, DrawGlow(256));

            // Copied rather than re-importing Feel's own texture as a sprite, so the third-party
            // package's import settings stay untouched.
            if (!File.Exists(BlastPath) && File.Exists(FeelExplosionPath))
                File.Copy(FeelExplosionPath, BlastPath);

            AssetDatabase.Refresh();
            ConfigureSprite(BombPath, true);
            ConfigureSprite(BoltPath, true);
            ConfigureSprite(GlowPath, true);
            ConfigureSprite(BlastPath, false);
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

        private static Texture2D DrawBomb(int size)
        {
            Color[] px = new Color[size * size];
            Vector2 centre = new Vector2(128f, 108f);

            // Fuse: two segments curling up and right from the cap, drawn first so the cap covers its root.
            Vector2 f0 = new Vector2(128f, 200f), f1 = new Vector2(140f, 226f), f2 = new Vector2(166f, 236f);
            Stamp(px, size, p => Mathf.Min(SegmentDistance(p, f0, f1), SegmentDistance(p, f1, f2)) - 7f, Ink);
            Stamp(px, size, p => Mathf.Min(SegmentDistance(p, f0, f1), SegmentDistance(p, f1, f2)) - 3.5f, new Color32(0xB9, 0x8B, 0x5A, 0xFF));

            // Cap.
            Stamp(px, size, p => BoxDistance(p, new Vector2(128f, 195f), new Vector2(24f, 16f)) - 5f, Ink);
            Stamp(px, size, p => BoxDistance(p, new Vector2(128f, 195f), new Vector2(20f, 12f)) - 3f, new Color32(0x6E, 0x6E, 0x78, 0xFF));

            // Body with outline and a glossy highlight.
            Stamp(px, size, p => Vector2.Distance(p, centre) - 92f, Ink);
            Stamp(px, size, p => Vector2.Distance(p, centre) - 84f, new Color32(0x3A, 0x3D, 0x4A, 0xFF));
            Stamp(px, size, p => Vector2.Distance(p, centre + new Vector2(18f, -14f)) - 62f, new Color32(0x2C, 0x2E, 0x38, 0xFF));
            Stamp(px, size, p => EllipseDistance(p, centre + new Vector2(-34f, 34f), new Vector2(22f, 14f), 0.6f), new Color(1f, 1f, 1f, 0.75f));

            // Spark at the fuse tip.
            Vector2 spark = new Vector2(170f, 238f);
            Stamp(px, size, p => StarDistance(p, spark, 18f, 8f, 5), new Color32(0xFF, 0x8A, 0x1E, 0xFF));
            Stamp(px, size, p => Vector2.Distance(p, spark) - 6f, new Color32(0xFF, 0xF1, 0x8C, 0xFF));

            return ToTexture(px, size);
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

        private static float StarDistance(Vector2 p, Vector2 centre, float outer, float inner, int points)
        {
            Vector2[] star = new Vector2[points * 2];
            for (int i = 0; i < star.Length; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                star[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            return PolygonDistance(p, star);
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
