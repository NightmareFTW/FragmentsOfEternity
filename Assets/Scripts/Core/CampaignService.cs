using Data;

namespace Core
{
    // Grants post-battle campaign rewards and advances stage unlocks.
    public static class CampaignService
    {
        public const int DefaultReward = 150;

        // Awards the stage's gems and unlocks the next stage when the current
        // frontier is cleared. Returns the gems granted.
        public static int GrantStageVictory(CampaignData campaign, int stageIndex)
        {
            var profile = SaveSystem.Profile;
            int reward  = DefaultReward;

            if (campaign != null && campaign.stages != null &&
                stageIndex >= 0 && stageIndex < campaign.stages.Length)
            {
                reward = campaign.stages[stageIndex].gemReward;
                if (stageIndex == profile.campaignProgress &&
                    profile.campaignProgress < campaign.stages.Length)
                    profile.campaignProgress++;
            }

            profile.gems += reward;
            SaveSystem.Save();
            return reward;
        }
    }
}
