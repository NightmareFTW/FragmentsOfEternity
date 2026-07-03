using System;
using UnityEngine;

namespace Data
{
    // An ordered list of campaign stages. Stage i unlocks once stage i-1 is
    // cleared (tracked by PlayerProfile.campaignProgress).
    [CreateAssetMenu(fileName = "Campaign", menuName = "RPG/Campaign Data")]
    public class CampaignData : ScriptableObject
    {
        public CampaignStage[] stages;
    }

    [Serializable]
    public class CampaignStage
    {
        public string        stageName = "Stage";
        public EncounterData encounter;
        [Min(0)] public int  gemReward = 150;
    }
}
