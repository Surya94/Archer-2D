using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Ad network settings, kept in a Resources asset so the ids are editable in the Inspector
    /// and never hardcoded in a script. Loaded the same way SoundManger loads its sound data.
    /// </summary>
    [CreateAssetMenu(fileName = "AdsConfig", menuName = "Archer/Ads Config")]
    public class AdsConfig : ScriptableObject
    {
        public const string ResourcesPath = "Scriptables/Ads/AdsConfig";

        [Tooltip("Your AppLovin SDK key. NOTE: the canonical copy must be entered in " +
                 "AppLovin > Integration Manager, which writes it into AppLovinSettings; the " +
                 "plugin reads it from there at init. This field is kept for reference and is " +
                 "not applied in code, because the SetSdkKey API differs across plugin majors.")]
        [SerializeField] private string sdkKey = string.Empty;

        [Tooltip("Rewarded ad unit id from your AppLovin dashboard. While this is blank the " +
                 "simulated provider is used, so the revive flow stays testable in the Editor.")]
        [SerializeField] private string rewardedAdUnitId = string.Empty;

        public string SdkKey => sdkKey;
        public string RewardedAdUnitId => rewardedAdUnitId;

        /// <summary>True when there is a real ad unit to serve from.</summary>
        public bool HasRewardedUnit => !string.IsNullOrWhiteSpace(rewardedAdUnitId);

        public static AdsConfig Load()
        {
            return Resources.Load<AdsConfig>(ResourcesPath);
        }
    }
}
