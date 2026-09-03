using UnityEngine;
using Data;

namespace Core
{
    // Flat stat contribution from ascension — folded into a hero's combat
    // stats alongside gear, the same way GearBonuses is.
    public struct AscensionBonuses
    {
        public int atk, def, hp, spd;
    }

    // Duplicate copies of a hero (from summoning one already owned) become
    // ascension material for that same hero: spend them to raise its star
    // count, which lifts its level cap and grants a flat stat bump — giving
    // pulls that aren't a new hero a use instead of just padding a count.
    public static class AscensionService
    {
        public const int MaxStars = 5;

        // +8% of base ATK/DEF/HP/SPD per star, up to +40% at 5 stars. A
        // simplification of "better skills" — skill damage/healing already
        // scales off these stats, so a stronger hero casts stronger skills
        // without needing per-hero copies of shared SkillData assets.
        private const float BonusPerStar = 0.08f;

        public static int GetStars(string heroId)
        {
            foreach (var e in SaveSystem.Profile.heroAscensions)
                if (e.id == heroId) return e.stars;
            return 0;
        }

        public static int OwnedCount(string heroId)
        {
            int n = 0;
            foreach (var id in SaveSystem.Profile.ownedHeroIds)
                if (id == heroId) n++;
            return n;
        }

        // Copies beyond the first — the ones actually spendable on ascension.
        public static int AvailableDupes(string heroId) => Mathf.Max(0, OwnedCount(heroId) - 1);

        public static int DupesNeededForStar(int currentStars) => currentStars + 1;

        public static bool CanAscend(string heroId)
        {
            int stars = GetStars(heroId);
            return stars < MaxStars && AvailableDupes(heroId) >= DupesNeededForStar(stars);
        }

        public static bool Ascend(string heroId)
        {
            if (!CanAscend(heroId)) return false;

            int stars = GetStars(heroId);
            int need  = DupesNeededForStar(stars);

            var profile = SaveSystem.Profile;
            for (int i = 0; i < need; i++) profile.ownedHeroIds.Remove(heroId);

            SetStars(profile, heroId, stars + 1);
            SaveSystem.Save();
            return true;
        }

        public static AscensionBonuses BonusesFor(string heroId, HeroData baseData)
        {
            int stars = GetStars(heroId);
            if (stars <= 0 || baseData == null) return default;

            float pct = stars * BonusPerStar;
            return new AscensionBonuses
            {
                atk = Mathf.RoundToInt(baseData.baseATK * pct),
                def = Mathf.RoundToInt(baseData.baseDEF * pct),
                hp  = Mathf.RoundToInt(baseData.baseHP  * pct),
                spd = Mathf.RoundToInt(baseData.baseSPD * pct),
            };
        }

        public static string StarsText(int stars) =>
            new string('★', stars) + new string('☆', MaxStars - stars);

        private static void SetStars(PlayerProfile profile, string heroId, int stars)
        {
            foreach (var e in profile.heroAscensions)
                if (e.id == heroId) { e.stars = stars; return; }
            profile.heroAscensions.Add(new HeroAscension { id = heroId, stars = stars });
        }
    }
}
