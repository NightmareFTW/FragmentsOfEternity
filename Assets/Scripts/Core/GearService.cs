using System;
using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Core
{
    // The combined stat contribution of a hero's equipped gear: each piece's own
    // flat roll (scaled by enhancement) plus substats, plus any active set bonus.
    public struct GearBonuses
    {
        public int   atk, def, hp, spd;
        public float critRate, critDamage, resistance, accuracy;
    }

    // Owns the gear inventory: equip/unequip, per-hero stat bonuses (including
    // substats and set bonuses), enhancement, and drops.
    public static class GearService
    {
        public const int MaxEnhance = 15;

        public static List<GearPiece> Inventory => SaveSystem.Profile.gear;

        // ── Bonuses ─────────────────────────────────────────────────────────

        // All stat contributions from a hero's equipped gear. `baseData` supplies
        // the hero's level-1 base stats, which set bonuses scale off of (a
        // simplification — set bonuses don't compound with hero level, keeping
        // the maths easy to reason about at a glance).
        public static GearBonuses BonusesFor(string heroId, HeroData baseData)
        {
            var result     = new GearBonuses();
            var setCounts  = new Dictionary<GearSet, int>();

            foreach (var g in Inventory)
            {
                if (g.equippedHero != heroId) continue;
                ApplyPiece(g, ref result);
                if (g.set != GearSet.None)
                    setCounts[g.set] = setCounts.TryGetValue(g.set, out var c) ? c + 1 : 1;
            }

            if (baseData != null)
                foreach (var kv in setCounts)
                    ApplySetBonus(kv.Key, kv.Value, baseData, ref result);

            return result;
        }

        private static void ApplyPiece(GearPiece g, ref GearBonuses b)
        {
            float mult = 1f + g.enhanceLevel * 0.10f;   // +10% of the base roll per enhance level
            b.atk += Mathf.RoundToInt(g.atk * mult);
            b.def += Mathf.RoundToInt(g.def * mult);
            b.hp  += Mathf.RoundToInt(g.hp  * mult);
            b.spd += Mathf.RoundToInt(g.spd * mult);

            if (g.substats == null) return;
            foreach (var s in g.substats)
            {
                switch (s.type)
                {
                    case GearStatType.ATK:        b.atk        += s.value; break;
                    case GearStatType.DEF:        b.def        += s.value; break;
                    case GearStatType.HP:         b.hp         += s.value * 5; break;
                    case GearStatType.SPD:        b.spd        += s.value; break;
                    case GearStatType.CritRate:   b.critRate   += s.value / 100f; break;
                    case GearStatType.CritDamage: b.critDamage += s.value / 100f; break;
                    case GearStatType.Resistance: b.resistance += s.value / 100f; break;
                    case GearStatType.Accuracy:   b.accuracy   += s.value / 100f; break;
                }
            }
        }

        // 2 matching pieces activate most sets; Fatal wants the full 3-piece loadout.
        private static void ApplySetBonus(GearSet set, int count, HeroData data, ref GearBonuses b)
        {
            switch (set)
            {
                case GearSet.Speed:    if (count >= 2) b.spd        += Mathf.RoundToInt(data.baseSPD * 0.25f); break;
                case GearSet.Rage:     if (count >= 2) b.atk        += Mathf.RoundToInt(data.baseATK * 0.20f); break;
                case GearSet.Guard:    if (count >= 2) b.def        += Mathf.RoundToInt(data.baseDEF * 0.20f); break;
                case GearSet.Life:     if (count >= 2) b.hp         += Mathf.RoundToInt(data.baseHP  * 0.20f); break;
                case GearSet.Crit:     if (count >= 2) b.critRate   += 0.12f; break;
                case GearSet.Immunity: if (count >= 2) b.resistance += 0.30f; break;
                case GearSet.Focus:    if (count >= 2) b.accuracy   += 0.25f; break;
                case GearSet.Fatal:    if (count >= 3) b.critDamage += 0.40f; break;
            }
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

        private static int Score(GearPiece g)
        {
            var flat = g.atk + g.def + g.hp / 5 + g.spd * 2;
            int sub  = 0;
            if (g.substats != null)
                foreach (var s in g.substats) sub += s.value;
            int enhanceBonus = Mathf.RoundToInt((g.atk + g.def + g.spd * 2) * g.enhanceLevel * 0.10f);
            int setBonus     = g.set != GearSet.None ? 15 : 0;   // nudge set pieces when tied
            return flat + sub + enhanceBonus + setBonus;
        }

        // ── Enhancement ──────────────────────────────────────────────────────

        public static int EnhanceCost(int currentLevel) => 100 + currentLevel * 60;

        // Enhances one specific piece by a single level, spending gems.
        public static bool Enhance(string gearId)
        {
            var piece = FindById(gearId);
            if (piece == null || piece.enhanceLevel >= MaxEnhance) return false;

            int cost    = EnhanceCost(piece.enhanceLevel);
            var profile = SaveSystem.Profile;
            if (profile.gems < cost) return false;

            profile.gems -= cost;
            piece.enhanceLevel++;

            // Every +3 levels, a random substat also gets a bump — the classic
            // "gear grows sharper as you invest" beat.
            if (piece.enhanceLevel % 3 == 0 && piece.substats != null && piece.substats.Count > 0)
            {
                var s = piece.substats[UnityEngine.Random.Range(0, piece.substats.Count)];
                s.value += Mathf.Max(1, s.value / 4);
            }

            SaveSystem.Save();
            return true;
        }

        // The lowest-enhanced piece currently equipped on this hero — always
        // makes progress, without needing a piece-picker in the UI.
        public static bool TryGetWeakestEquipped(string heroId, out GearPiece piece)
        {
            piece = null;
            foreach (var g in Inventory)
            {
                if (g.equippedHero != heroId || g.enhanceLevel >= MaxEnhance) continue;
                if (piece == null || g.enhanceLevel < piece.enhanceLevel) piece = g;
            }
            return piece != null;
        }

        public static bool EnhanceWeakestEquipped(string heroId) =>
            TryGetWeakestEquipped(heroId, out var piece) && Enhance(piece.id);

        // ── Drops ───────────────────────────────────────────────────────────

        // 60% chance to drop a piece scaled by the stage. Returns null on no drop.
        public static GearPiece RollDrop(int stageIndex)
        {
            if (UnityEngine.Random.value > 0.6f) return null;

            int tier   = Mathf.Clamp(stageIndex + 1, 1, 5);
            var slots  = (GearSlot[])Enum.GetValues(typeof(GearSlot));
            var slot   = slots[UnityEngine.Random.Range(0, slots.Length)];
            int mag    = 8 + tier * 6;
            var piece  = new GearPiece
            {
                id     = Guid.NewGuid().ToString("N").Substring(0, 8),
                slot   = slot,
                rarity = Mathf.Clamp(2 + stageIndex, 3, 5),
                set    = RandomSet(),
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

            piece.substats = RollSubstats(piece.rarity);
            Inventory.Add(piece);
            SaveSystem.Save();
            return piece;
        }

        private static GearSet RandomSet()
        {
            var sets = (GearSet[])Enum.GetValues(typeof(GearSet));
            return sets[UnityEngine.Random.Range(1, sets.Length)];   // skip None at index 0
        }

        // 3★→1 substat, 4★→2, 5★→3, each a distinct stat type.
        private static List<GearSubstat> RollSubstats(int rarity)
        {
            int count = Mathf.Clamp(rarity - 2, 1, 4);
            var pool  = new List<GearStatType>
            {
                GearStatType.ATK, GearStatType.DEF, GearStatType.HP, GearStatType.SPD,
                GearStatType.CritRate, GearStatType.CritDamage, GearStatType.Resistance, GearStatType.Accuracy
            };

            var result = new List<GearSubstat>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx  = UnityEngine.Random.Range(0, pool.Count);
                var type = pool[idx];
                pool.RemoveAt(idx);
                result.Add(new GearSubstat { type = type, value = RollSubstatValue(type) });
            }
            return result;
        }

        private static int RollSubstatValue(GearStatType type) => type switch
        {
            GearStatType.ATK        => UnityEngine.Random.Range(3, 9),
            GearStatType.DEF        => UnityEngine.Random.Range(3, 9),
            GearStatType.HP         => UnityEngine.Random.Range(2, 6),
            GearStatType.SPD        => UnityEngine.Random.Range(1, 4),
            GearStatType.CritRate   => UnityEngine.Random.Range(2, 6),
            GearStatType.CritDamage => UnityEngine.Random.Range(3, 8),
            GearStatType.Resistance => UnityEngine.Random.Range(2, 6),
            GearStatType.Accuracy   => UnityEngine.Random.Range(2, 6),
            _                       => 1,
        };

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
            string main = string.Join(", ", parts);
            string enh  = g.enhanceLevel > 0 ? $" +{g.enhanceLevel}" : "";
            string set  = g.set != GearSet.None ? $" [{g.set} Set]" : "";
            return $"{main}{enh}{set}";
        }

        public static string DescribeSubstats(GearPiece g)
        {
            if (g == null || g.substats == null || g.substats.Count == 0) return "";
            var parts = new List<string>();
            foreach (var s in g.substats) parts.Add(DescribeSubstat(s));
            return string.Join(", ", parts);
        }

        private static string DescribeSubstat(GearSubstat s) => s.type switch
        {
            GearStatType.ATK        => $"+{s.value} ATK",
            GearStatType.DEF        => $"+{s.value} DEF",
            GearStatType.HP         => $"+{s.value * 5} HP",
            GearStatType.SPD        => $"+{s.value} SPD",
            GearStatType.CritRate   => $"+{s.value}% CR",
            GearStatType.CritDamage => $"+{s.value}% CD",
            GearStatType.Resistance => $"+{s.value}% RES",
            GearStatType.Accuracy   => $"+{s.value}% ACC",
            _                       => "",
        };
    }
}
