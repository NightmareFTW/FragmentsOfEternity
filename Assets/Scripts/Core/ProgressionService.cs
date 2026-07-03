namespace Core
{
    // Hero leveling: spend gems to raise an owned hero's level, which scales its
    // stats via HeroData growth when the unit is built for battle.
    public static class ProgressionService
    {
        public const int MaxLevel = 20;

        public static int GetLevel(string heroId)
        {
            foreach (var e in SaveSystem.Profile.heroLevels)
                if (e.id == heroId) return e.level;
            return 1;
        }

        public static int CostToLevel(int currentLevel) => 50 * currentLevel;

        // Returns the new level, or -1 when it can't level up (max or too poor).
        public static int LevelUp(string heroId)
        {
            var profile = SaveSystem.Profile;
            int level   = GetLevel(heroId);
            if (level >= MaxLevel) return -1;

            int cost = CostToLevel(level);
            if (profile.gems < cost) return -1;

            profile.gems -= cost;
            SetLevel(profile, heroId, level + 1);
            SaveSystem.Save();
            return level + 1;
        }

        private static void SetLevel(PlayerProfile profile, string heroId, int level)
        {
            foreach (var e in profile.heroLevels)
                if (e.id == heroId) { e.level = level; return; }
            profile.heroLevels.Add(new HeroLevel { id = heroId, level = level });
        }
    }
}
