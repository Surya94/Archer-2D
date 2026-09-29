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
    /// Replaces the flat FullBG/1_Mountain background in MainMenu and GameScene with the art
    /// pack's separate layers, and adds a CloudField.
    ///
    /// Why: the flat image has four clouds painted into it. Drifting clouds on top of frozen
    /// painted ones is what made the sky look fake. The layered version has a cloud-free sky
    /// (Layer_0), so every cloud on screen moves, and clouds can sort between the sky and the
    /// mountains so low ones pass behind the peaks.
    ///
    /// Safe to re-run: an existing Background / CloudField is replaced, not duplicated.
    /// </summary>
    public static class SceneBackgroundBuilder
    {
        private const string LayerDir = "Assets/Free 2D Cartoon Parallax Background/!_Moutain/";
        private const string CloudPrefabPath = "Assets/Prefabs/Cloud.prefab";
        private const string LegacyBackgroundName = "sky_background_mountains";
        private const string BackgroundName = "Background";
        private const string CloudFieldName = "CloudField";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/GameScene.unity"
        };

        // Back to front. Clouds (CloudField) sort between the sky and the mountains: -90..-80.
        private static readonly (string file, string name, int order)[] Layers =
        {
            ("Layer_0.png", "Sky", -100),
            ("Layer_2.png", "Mountains", -70),
            ("Layer_3.png", "Trees", -65),
            ("Layer_4.png", "Ground", -60),
        };

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
            // exactly: the layers share the flat image's 4320x2160 size and 108 PPU.
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
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(LayerDir + "Layer_1.png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            GameObject go = new GameObject(CloudFieldName);
            CloudField field = go.AddComponent<CloudField>();

            SerializedObject so = new SerializedObject(field);
            so.FindProperty("cloudPrefab").objectReferenceValue =
                prefab != null ? prefab.GetComponent<CloudDrifter>() : null;

            SerializedProperty spriteArray = so.FindProperty("cloudSprites");
            spriteArray.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                spriteArray.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            if (prefab == null || sprites.Length == 0)
                Debug.LogError("[SceneBackgroundBuilder] CloudField is missing its prefab or sprites.");
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
    }
}
