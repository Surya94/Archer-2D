using System.IO;
using UnityEditor;
using UnityEngine;
using Archer.Scripts.Manager.Ads;

namespace Archer.Editor
{
    /// <summary>
    /// Creates the AdsConfig asset in the exact Resources path AdsManager loads from.
    /// The ids are left blank on purpose: while the rewarded ad unit id is empty, AdsManager
    /// uses the simulated provider, so the revive flow is testable before any AppLovin setup
    /// exists - and no real ad ids end up committed to the repository.
    /// </summary>
    public static class AdsConfigBuilder
    {
        private const string Dir = "Assets/Resources/Scriptables/Ads";
        private const string Path = Dir + "/AdsConfig.asset";

        [MenuItem("Archer/Ads/Create AdsConfig Asset")]
        public static void CreateAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<AdsConfig>(Path) != null)
            {
                Debug.Log("[AdsConfigBuilder] AdsConfig already exists at " + Path);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<AdsConfig>(Path);
                return;
            }

            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                AssetDatabase.Refresh();
            }

            AdsConfig config = ScriptableObject.CreateInstance<AdsConfig>();
            AssetDatabase.CreateAsset(config, Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            Debug.Log("[AdsConfigBuilder] Created " + Path + " (ids blank - simulated ads active).");
        }
    }
}
