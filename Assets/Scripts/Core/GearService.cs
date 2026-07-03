using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    // Owns the gear inventory: equip/unequip, per-hero stat bonuses, and drops.
    public static class GearService
    {
        public static List<GearPiece> Inventory => SaveSystem.Profile.gear;

        // ── Bonuses ─────────────────────────────────────────────────────────

        public static int BonusATK(string heroId) => SumFor(heroId, g => g.atk);
        public static int BonusDEF(string heroId) => SumFor(heroId, g => g.def);
        public static int BonusHP (string heroId) => SumFor(heroId, g => g.hp);
        public static int BonusSPD(string heroId) => SumFor(heroId, g => g.spd);

        private static int SumFor(string heroId, Func<GearPiece, int> sel)
        {
            int total = 0;
            foreach (var g in Inventory)
                if (g.equippedHero == heroId) total += sel(g);
            return total;
        }

        public static GearPiece EquippedOn(string heroId, GearSlot slot)
        {
            foreach (var g in Inventory)
                if (g.equippedHero == heroId && g.slot == slot) return g;
            return null;
        }

        // ── Equip / unequip ─────────────────────────────────────────────────

        public static void Equip(string gearId, string heroId)
        {
            var piece = FindById(gearId);
            if (piece == null) return;

            var current = EquippedOn(heroId, piece.slot);
            if (current != null && current != piece) current.equippedHero = "";
            piece.equippedHero = heroId;
            SaveSystem.Save();
        }

        public static void UnequipAll(string heroId)
        {
            foreach (var g in Inventory)
                if (g.equippedHero == heroId) g.equippedHero = "";
            SaveSystem.Save();
        }

        // Fill each of the hero's slots with the strongest available piece.
        public static void AutoEquip(string heroId)
        {
            foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
            {
                GearPiece best = EquippedOn(heroId, slot);
                int bestScore  = best != null ? Score(best) : -1;
                foreach (var g in Inventory)
                {
                    if (g.slot != slot) continue;
                    if (!string.IsNullOrEmpty(g.equippedHero) && g.equippedHero != heroId) continue;
                    int s = Score(g);
                    if (s > bestScore) { best = g; bestScore = s; }
                }
                if (best != null) Equip(best.id, heroId);
            }
        }

        private static int Score(GearPiece g) => g.atk + g.def + g.hp / 5 + g.spd * 2;

        // ── Drops ───────────────────────────────────────────────────────────

        // 60% chance to drop a piece scaled by the stage. Returns null on no drop.
        public static GearPiece RollDrop(int stageIndex)
        {
            if (UnityEngine.Random.value > 0.6f) return null;

            int tier   = Mathf.Clamp(stageIndex + 1, 1, 5);
            var slot   = (GearSlot)UnityEngine.Random.Range(0, 3);
            int mag    = 8 + tier * 6;
            var piece  = new GearPiece
            {
                id     = Guid.NewGuid().ToString("N").Substring(0, 8),
                slot   = slot,
                rarity = Mathf.Clamp(2 + stageIndex, 3, 5),
            };

            switch (slot)
            {
                case GearSlot.Weapon:
                    piece.atk = mag + UnityEngine.Random.Range(0, mag / 2 + 1);
                    break;
                case GearSlot.Armor:
                    piece.def = mag / 2 + UnityEngine.Random.Range(0, mag / 3 + 1);
                    piece.hp  = mag * 4;
                    break;
                case GearSlot.Accessory:
                    piece.spd = 4 + tier * 2;
                    piece.atk = mag / 2;
                    break;
            }

            Inventory.Add(piece);
            SaveSystem.Save();
            return piece;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static GearPiece FindById(string id)
        {
            foreach (var g in Inventory) if (g.id == id) return g;
            return null;
        }

        public static string Describe(GearPiece g)
        {
            if (g == null) return "(empty)";
            var parts = new List<string>();
            if (g.atk > 0) parts.Add($"+{g.atk} ATK");
            if (g.def > 0) parts.Add($"+{g.def} DEF");
            if (g.hp  > 0) parts.Add($"+{g.hp} HP");
            if (g.spd > 0) parts.Add($"+{g.spd} SPD");
            return string.Join(", ", parts);
        }
    }
}
