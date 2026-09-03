using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Core
{
    public struct ArenaRival
    {
        public string   name;
        public string[] heroIds;
        public int      level;
        public int      reward;
    }

    // Generates one AI-controlled "rival" squad from the same hero pool
    // players summon from, scaled roughly to the player's own team level.
    // There's no server or other real players behind this — a stand-in for
    // genuine asynchronous PvP until/unless this game ever gets a backend.
    public static class ArenaService
    {
        private const int TeamSize = 4;

        private static readonly string[] NamePrefixes =
            { "Iron", "Shadow", "Storm", "Crimson", "Silent", "Golden", "Frost", "Ember" };
        private static readonly string[] NameSuffixes =
            { "Vanguard", "Legion", "Order", "Pact", "Watch", "Fang", "Circle", "Host" };

        public static ArenaRival GenerateRival(GachaPool pool)
        {
            int level = Mathf.Clamp(AverageTeamLevel() + Random.Range(-2, 4), 1, ProgressionService.BaseMaxLevel + 50);
            return new ArenaRival
            {
                name    = $"{NamePrefixes[Random.Range(0, NamePrefixes.Length)]} " +
                          $"{NameSuffixes[Random.Range(0, NameSuffixes.Length)]}",
                heroIds = RandomTeam(pool),
                level   = level,
                reward  = 80 + level * 6,
            };
        }

        private static string[] RandomTeam(GachaPool pool)
        {
            var result = new List<string>();
            if (pool == null || pool.heroes == null) return result.ToArray();

            var pickPool = new List<HeroData>(pool.heroes);
            int count    = Mathf.Min(TeamSize, pickPool.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, pickPool.Count);
                if (pickPool[idx] != null) result.Add(pickPool[idx].heroId);
                pickPool.RemoveAt(idx);
            }
            return result.ToArray();
        }

        private static int AverageTeamLevel()
        {
            var ids = SaveSystem.Profile.teamHeroIds;
            if (ids == null || ids.Count == 0) return 1;
            int total = 0;
            foreach (var id in ids) total += ProgressionService.GetLevel(id);
            return Mathf.Max(1, total / ids.Count);
        }
    }
}
