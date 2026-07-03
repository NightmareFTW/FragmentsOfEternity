using System.Collections.Generic;
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

    // Spends gems and rolls a hero from a pool. Rarer heroes are less likely,
    // with a pity guarantee that forces a 5★ after enough dry pulls.
    public static class GachaService
    {
        public const int PityThreshold = 15;

        public static SummonResult Summon(GachaPool pool)
        {
            if (pool == null || pool.heroes == null || pool.heroes.Length == 0)
                return Fail("No summon pool configured.");

            var profile = SaveSystem.Profile;
            if (profile.gems < pool.summonCost)
                return Fail("Not enough gems.");

            bool pityHit = profile.pityCounter >= PityThreshold;
            var  hero    = pityHit ? RollOfRarity(pool.heroes, 5) ?? RollWeighted(pool.heroes)
                                   : RollWeighted(pool.heroes);
            if (hero == null)
                return Fail("Summon pool is empty.");

            profile.gems -= pool.summonCost;
            if (hero.rarity >= 5) profile.pityCounter = 0;
            else                  profile.pityCounter++;

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

        public static List<SummonResult> SummonMany(GachaPool pool, int count)
        {
            var results = new List<SummonResult>();
            for (int i = 0; i < count; i++)
            {
                var r = Summon(pool);
                if (!r.Success)
                {
                    if (results.Count == 0) results.Add(r);   // surface the reason
                    break;
                }
                results.Add(r);
            }
            return results;
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

        private static HeroData RollOfRarity(HeroData[] heroes, int rarity)
        {
            var pool = new List<HeroData>();
            foreach (var h in heroes) if (h != null && h.rarity == rarity) pool.Add(h);
            return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
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
