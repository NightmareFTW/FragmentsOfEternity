namespace Core
{
    // Grants post-battle rewards into the player's profile.
    public static class RewardService
    {
        public const int VictoryGems = 150;

        public static int GrantVictory()
        {
            var profile = SaveSystem.Profile;
            profile.gems += VictoryGems;
            SaveSystem.Save();
            return VictoryGems;
        }
    }
}
