using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Combat
{
    public enum Team { Player, Enemy }

    public class ActiveStatusEffect
    {
        public StatusEffectType Type;
        public int              Duration;
        public int              Value;
    }

    public class Unit
    {
        public string Name       { get; }
        public int    MaxHP      { get; }
        public int    HP         { get; private set; }
        public int    BaseAttack  { get; }
        public int    BaseDefense { get; }
        public int    BaseSpeed   { get; }
        public float  CritRate   { get; }
        public float  CritDamage { get; }
        public float  Resistance     { get; }   // chance to resist incoming debuffs
        public float  EffectAccuracy { get; }   // chance to bypass a target's resistance
        public Element Element   { get; }
        public Sprite Portrait   { get; }
        public float  TurnMeter  { get; private set; }

        public Team Team { get; set; }
        public int  Slot { get; set; }   // index within the unit's own team, for UI mapping

        public bool IsAlive => HP > 0;
        public bool IsReady => TurnMeter >= 100f;

        public SkillData[] Skills { get; private set; } = System.Array.Empty<SkillData>();
        private int[] _cooldowns = System.Array.Empty<int>();

        private readonly List<ActiveStatusEffect> _activeEffects = new();

        public Unit(string name, int hp, int speed,
                    int attack = 100, int defense = 50,
                    float critRate = 0.05f, float critDamage = 1.5f,
                    float resistance = 0.15f, float effectAccuracy = 0f,
                    Element element = Element.Fire, Sprite portrait = null)
        {
            Name           = name;
            MaxHP          = hp;
            HP             = hp;
            BaseSpeed      = speed;
            BaseAttack     = attack;
            BaseDefense    = defense;
            CritRate       = critRate;
            CritDamage     = critDamage;
            Resistance     = resistance;
            EffectAccuracy = effectAccuracy;
            Element        = element;
            Portrait       = portrait;
        }

        // Builds a combat Unit from a HeroData asset, applying per-level growth.
        // Level 1 uses the base stats as-authored.
        public static Unit FromHeroData(HeroData data, int level = 1,
            int bonusHP = 0, int bonusATK = 0, int bonusDEF = 0, int bonusSPD = 0)
        {
            int lv  = Mathf.Max(1, level);
            int hp  = Mathf.RoundToInt(data.baseHP  + data.hpGrowth  * (lv - 1)) + bonusHP;
            int atk = Mathf.RoundToInt(data.baseATK + data.atkGrowth * (lv - 1)) + bonusATK;
            int def = Mathf.RoundToInt(data.baseDEF + data.defGrowth * (lv - 1)) + bonusDEF;
            int spd = data.baseSPD + bonusSPD;
            string name = string.IsNullOrEmpty(data.heroName) ? data.name : data.heroName;

            return new Unit(name, hp, spd, atk, def,
                            data.baseCritRate, data.baseCritDamage,
                            data.baseResistance, data.baseAccuracy,
                            data.element, data.portrait);
        }

        // ── Effective stats (base × active status modifiers) ────────────────

        public int EffectiveAttack =>
            Mathf.Max(1, Mathf.RoundToInt(BaseAttack *
                (1f + PercentMod(StatusEffectType.AttackUp, StatusEffectType.AttackDebuff))));

        public int EffectiveDefense =>
            Mathf.Max(0, Mathf.RoundToInt(BaseDefense *
                (1f + PercentMod(StatusEffectType.DefenseUp, StatusEffectType.DefenseDebuff))));

        public float EffectiveSpeed =>
            Mathf.Max(1f, BaseSpeed *
                (1f + PercentMod(StatusEffectType.SpeedBuff, StatusEffectType.SpeedDebuff)));

        // Net modifier as a fraction: sum of "up" percents minus "down" percents.
        // Effect.Value is stored as whole percent (e.g. 20 → +0.20).
        private float PercentMod(StatusEffectType up, StatusEffectType down)
        {
            float m = 0f;
            foreach (var e in _activeEffects)
            {
                if (e.Type == up)   m += e.Value / 100f;
                if (e.Type == down) m -= e.Value / 100f;
            }
            return m;
        }

        // ── Meter / HP ──────────────────────────────────────────────────────

        public void AdvanceMeter(float dt)
        {
            if (IsAlive) TurnMeter += EffectiveSpeed * dt;
        }

        public void ConsumeMeter() => TurnMeter = 0f;

        // Incoming damage is absorbed by any Shield/Barrier effects first
        // (consuming their Value), then the remainder comes off HP. Returns the
        // HP actually lost (0 when fully shielded).
        public int TakeDamage(int amount)
        {
            if (amount > 0) RemoveEffects(StatusEffectType.Sleep);   // a landed hit wakes

            int remaining = amount;
            for (int i = _activeEffects.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var e = _activeEffects[i];
                if (e.Type != StatusEffectType.Shield && e.Type != StatusEffectType.Barrier) continue;
                int absorb = Mathf.Min(e.Value, remaining);
                e.Value   -= absorb;
                remaining -= absorb;
                if (e.Value <= 0) _activeEffects.RemoveAt(i);
            }

            int actual = Mathf.Min(remaining, HP);
            HP = Mathf.Max(0, HP - remaining);
            return actual;
        }

        public int Heal(int amount)
        {
            int actual = Mathf.Min(amount, MaxHP - HP);
            HP = Mathf.Min(MaxHP, HP + amount);
            return actual;
        }

        // ── Skills & cooldowns ──────────────────────────────────────────────

        public void SetSkills(SkillData[] skills)
        {
            Skills     = skills ?? System.Array.Empty<SkillData>();
            _cooldowns = new int[Skills.Length];
        }

        public int[] CooldownSnapshot() => (int[])_cooldowns.Clone();

        public int CooldownOf(int slot) =>
            (slot >= 0 && slot < _cooldowns.Length) ? _cooldowns[slot] : 0;

        public bool IsSkillReady(int slot) =>
            slot >= 0 && slot < Skills.Length && _cooldowns[slot] == 0;

        public bool HasReadySkill()
        {
            for (int i = 0; i < Skills.Length; i++)
                if (_cooldowns[i] == 0) return true;
            return false;
        }

        public void PutOnCooldown(int slot)
        {
            if (slot >= 0 && slot < Skills.Length && Skills[slot] != null)
                _cooldowns[slot] = Skills[slot].cooldownTurns;
        }

        public void TickCooldowns()
        {
            for (int i = 0; i < _cooldowns.Length; i++)
                if (_cooldowns[i] > 0) _cooldowns[i]--;
        }

        // ── Status effects ──────────────────────────────────────────────────

        public bool IsStunned  => HasEffect(StatusEffectType.Stun) || HasEffect(StatusEffectType.Sleep);
        public bool IsSilenced => HasEffect(StatusEffectType.Silence);

        public void AddEffect(StatusEffectEntry entry)
        {
            if (entry == null || entry.type == StatusEffectType.None) return;
            _activeEffects.Add(new ActiveStatusEffect
            {
                Type     = entry.type,
                Duration = Mathf.Max(1, entry.duration),
                Value    = entry.value
            });
            Debug.Log($"{Name} gained {entry.type} ({entry.duration}T, {entry.value})");
        }

        // Poison/Burn/Bleed each deal their Value straight to HP (ignores DEF).
        // Returns the total dealt so the caller can drive UI feedback.
        public int TickDamageOverTime()
        {
            int total = 0;
            foreach (var e in _activeEffects)
                if (e.Type == StatusEffectType.Poison ||
                    e.Type == StatusEffectType.Burn   ||
                    e.Type == StatusEffectType.Bleed)
                    total += Mathf.Max(0, e.Value);

            if (total <= 0) return 0;
            int actual = Mathf.Min(total, HP);
            HP = Mathf.Max(0, HP - total);
            return actual;
        }

        // Decrement every effect's remaining duration; drop the expired ones.
        // Called exactly once per the unit's own turn.
        public void TickEffects()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                _activeEffects[i].Duration--;
                if (_activeEffects[i].Duration <= 0)
                {
                    Debug.Log($"{Name} lost {_activeEffects[i].Type}");
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        private bool HasEffect(StatusEffectType t)
        {
            foreach (var e in _activeEffects) if (e.Type == t) return true;
            return false;
        }

        private void RemoveEffects(StatusEffectType t)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
                if (_activeEffects[i].Type == t) _activeEffects.RemoveAt(i);
        }

        public List<ActiveStatusEffect> GetEffects() => _activeEffects;

        public override string ToString() => $"{Name} ({HP}/{MaxHP} HP)";
    }
}
