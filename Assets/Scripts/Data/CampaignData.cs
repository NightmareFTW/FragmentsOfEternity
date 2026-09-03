using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    // An ordered list of chapters, each an ordered list of stages. Stages are
    // still unlocked/tracked as one flat sequence (PlayerProfile.campaignProgress
    // and CampaignState.SelectedStage are global indices into AllStages()) —
    // chapters are an authoring/display grouping on top of that, not a second
    // progress axis.
    [CreateAssetMenu(fileName = "Campaign", menuName = "RPG/Campaign Data")]
    public class CampaignData : ScriptableObject
    {
        public CampaignChapter[] chapters;

        public CampaignStage[] AllStages()
        {
            var list = new List<CampaignStage>();
            if (chapters == null) return list.ToArray();
            foreach (var c in chapters)
                if (c != null && c.stages != null)
                    list.AddRange(c.stages);
            return list.ToArray();
        }
    }

    [Serializable]
    public class CampaignChapter
    {
        public string          chapterName = "Chapter";
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
