using System;
using UnityEngine;

namespace Core
{
    // A slowly-regenerating resource that gates stage attempts and sweeps,
    // giving players a reason to check back later instead of clearing
    // everything in one sitting. Regen is time-based so it keeps accruing
    // even while the game isn't running.
    public static class StaminaService
    {
        public const int MaxStamina       = 120;
        public const int RegenIntervalMin = 5;   // +1 stamina every 5 real minutes
        public const int StageCost        = 6;
        public const int ArenaCost        = 8;

        // Applies any regen owed since the last check, then returns current stamina.
        public static int Current()
        {
            var profile = SaveSystem.Profile;

            if (profile.staminaLastRegenTicks == 0)
            {
                profile.stamina               = MaxStamina;
                profile.staminaLastRegenTicks = DateTime.UtcNow.Ticks;
                SaveSystem.Save();
                return profile.stamina;
            }

            if (profile.stamina >= MaxStamina) return profile.stamina;

            var last    = new DateTime(profile.staminaLastRegenTicks, DateTimeKind.Utc);
            var elapsed = DateTime.UtcNow - last;
            int gained  = (int)(elapsed.TotalMinutes / RegenIntervalMin);
            if (gained <= 0) return profile.stamina;

            // Advance the clock by exactly the minutes consumed, not to "now"
            // — keeps partial progress toward the next tick instead of losing it.
            profile.stamina               = Mathf.Min(MaxStamina, profile.stamina + gained);
            profile.staminaLastRegenTicks += TimeSpan.FromMinutes(gained * RegenIntervalMin).Ticks;
            SaveSystem.Save();
            return profile.stamina;
        }

        public static bool CanAfford(int cost) => Current() >= cost;

        public static bool Spend(int cost)
        {
            if (!CanAfford(cost)) return false;
            SaveSystem.Profile.stamina -= cost;
            SaveSystem.Save();
            return true;
        }
    }
}
