using UnityEngine;
using Data;

namespace Core
{
    public struct SummonResult
    {
        public bool     Success;
        public HeroData Hero;
        public bool     IsNew;
        public string   Message;
    }

    // Spends gems and rolls a hero from a pool. Rarer heroes are less likely.
    public static class GachaService
    {
        public static SummonResult Summon(GachaPool pool)
        {
            if (pool == null || pool.heroes == null || pool.heroes.Length == 0)
                return Fail("No summon pool configured.");

            var profile = SaveSystem.Profile;
            if (profile.gems < pool.summonCost)
                return Fail("Not enough gems.");

            var hero = RollWeighted(pool.heroes);
            if (hero == null)
                return Fail("Summon pool is empty.");

            profile.gems -= pool.summonCost;
            bool isNew = !profile.ownedHeroIds.Contains(hero.heroId);
            profile.ownedHeroIds.Add(hero.heroId);
            SaveSystem.Save();

            return new SummonResult
            {
                Success = true,
                Hero    = hero,
                IsNew   = isNew,
                Message = $"You summoned {hero.heroName}!"
            };
        }

        private static SummonResult Fail(string message) =>
            new SummonResult { Success = false, Message = message };

        private static HeroData RollWeighted(HeroData[] heroes)
        {
            int total = 0;
            foreach (var h in heroes) if (h != null) total += Weight(h.rarity);
            if (total <= 0) return null;

            int roll = Random.Range(0, total);
            foreach (var h in heroes)
            {
                if (h == null) continue;
                roll -= Weight(h.rarity);
                if (roll < 0) return h;
            }
            return null;
        }

        // 5★ are the rarest, 3★ the most common.
        private static int Weight(int rarity) => rarity switch
        {
            5 => 1,
            4 => 4,
            _ => 10,
        };
    }
}
