using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Archer.Scripts.Manager;
using Archer.Scripts.Utility;

namespace Archer.Editor
{
    /// <summary>
    /// Builds the layered "sunny meadow" background (Assets/Art/Background) in MainMenu and
    /// GameScene, and adds a CloudField.
    ///
    /// The sky layer is cloud-free, so every cloud on screen moves, and clouds sort between the
    /// sky and the landscape so low ones pass behind the hills. Each layer is a full 2:1 frame
    /// imported at 40x20 units (PPU = width / 40), the size the old 4320x2160 / 108 PPU art had,
    /// so the Background root's 0.5 scale still fills the camera exactly.
    ///
    /// Safe to re-run: an existing Background / CloudField is replaced, not duplicated.
    /// </summary>
    public static class SceneBackgroundBuilder
    {
        private const string LayerDir = "Assets/Art/Background/";
        private const string CloudDir = LayerDir + "Clouds/";
        private const string CloudPrefabPath = "Assets/Prefabs/Cloud.prefab";
        private const string LegacyBackgroundName = "sky_background_mountains";
        private const string BackgroundName = "Background";
        private const string CloudFieldName = "CloudField";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/GameScene.unity"
        };

        // Back to front. Clouds (CloudField) sort between the sky and the landscape: -90..-80.
        private static readonly (string file, string name, int order)[] Layers =
        {
            ("Sky.png", "Sky", -100),
            ("Landscape.png", "Landscape", -70),
        };

        /// <summary>The drifting cloud sprites (Clouds/Cloud_1..n.png), in name order.</summary>
        public static Sprite[] LoadCloudSprites()
        {
            return AssetDatabase.FindAssets("t:Sprite Cloud_", new[] { CloudDir.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .ToArray();
        }

        [MenuItem("Archer/Scenes/Build Layered Backgrounds")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SceneBackgroundBuilder] Exit play mode before building backgrounds.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (string path in ScenePaths)
                BuildScene(path);
        }

        private static void BuildScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject legacy = FindRoot(scene, LegacyBackgroundName);
            GameObject existing = FindRoot(scene, BackgroundName);
            GameObject source = legacy != null ? legacy : existing;
            if (source == null)
            {
                Debug.LogError("[SceneBackgroundBuilder] No '" + LegacyBackgroundName + "' or '" +
                               BackgroundName + "' in " + scenePath + " - skipped.");
                return;
            }

            // Take placement from whatever currently fills the view, so the new stack lines up
            // exactly: every layer is imported at the same 40x20 world units.
            Vector3 position = source.transform.position;
            Vector3 scale = source.transform.localScale;
            int sortingLayerId = source.GetComponentInChildren<SpriteRenderer>().sortingLayerID;

            GameObject background = new GameObject(BackgroundName);
            background.transform.position = position;
            background.transform.localScale = scale;

            // Scales the art up on screens wider than 16:9 so no gap shows at the edges.
            AspectFitter fitter = background.AddComponent<AspectFitter>();
            SerializedObject fitterSo = new SerializedObject(fitter);
            fitterSo.FindProperty("mode").enumValueIndex = (int)AspectFitter.Mode.CoverBackground;
            fitterSo.ApplyModifiedPropertiesWithoutUndo();

            foreach (var layer in Layers)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerDir + layer.file);
                if (sprite == null)
                {
                    Debug.LogError("[SceneBackgroundBuilder] Missing sprite " + LayerDir + layer.file);
                    continue;
                }

                GameObject go = new GameObject(layer.name);
                go.transform.SetParent(background.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerID = sortingLayerId;
                sr.sortingOrder = layer.order;
            }

            if (legacy != null) Object.DestroyImmediate(legacy);
            if (existing != null) Object.DestroyImmediate(existing);

            RemoveLooseClouds(scene);
            CreateCloudField(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneBackgroundBuilder] Layered background + CloudField built in " + scenePath);
        }

        /// <summary>Hand-placed drifting clouds, now superseded by the CloudField.</summary>
        private static void RemoveLooseClouds(Scene scene)
        {
            List<GameObject> loose = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<CloudDrifter>(true))
                .Where(c => c.GetComponentInParent<CloudField>(true) == null)
                .Select(c => c.gameObject)
                .ToList();

            foreach (GameObject go in loose)
                Object.DestroyImmediate(go);

            if (loose.Count > 0)
                Debug.Log("[SceneBackgroundBuilder] Removed " + loose.Count + " hand-placed clouds from " + scene.name);
        }

        private static void CreateCloudField(Scene scene)
        {
            GameObject old = FindRoot(scene, CloudFieldName);
            if (old != null) Object.DestroyImmediate(old);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath);
            Sprite[] sprites = LoadCloudSprites();

            GameObject go = new GameObject(CloudFieldName);
            CloudField field = go.AddComponent<CloudField>();

            SerializedObject so = new SerializedObject(field);
            so.FindProperty("cloudPrefab").objectReferenceValue =
                prefab != null ? prefab.GetComponent<CloudDrifter>() : null;

            SerializedProperty spriteArray = so.FindProperty("cloudSprites");
            spriteArray.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                spriteArray.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];

            // Subtle sky: few, small, half-transparent clouds kept in the top band, drifting
            // slowly, so they never compete with the balloons crossing the middle of the view.
            so.FindProperty("cloudCount").intValue = 4;
            so.FindProperty("scaleRange").vector2Value = new Vector2(0.22f, 0.45f);
            so.FindProperty("alphaRange").vector2Value = new Vector2(0.45f, 0.75f);
            so.FindProperty("farHeightRange").vector2Value = new Vector2(0.82f, 0.95f);
            so.FindProperty("nearHeightRange").vector2Value = new Vector2(0.72f, 0.88f);
            so.FindProperty("speedRange").vector2Value = new Vector2(0.05f, 0.22f);
            // Twilight sky: far clouds fade toward dusk lavender, not daytime pale blue.
            so.FindProperty("hazeColour").colorValue = new Color32(0x8A, 0x86, 0xC4, 0xFF);

            so.ApplyModifiedPropertiesWithoutUndo();

            if (prefab == null || sprites.Length == 0)
                Debug.LogError("[SceneBackgroundBuilder] CloudField is missing its prefab or sprites.");
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
    }
}
