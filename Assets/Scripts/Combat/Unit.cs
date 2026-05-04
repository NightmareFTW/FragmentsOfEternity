using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Combat
{
    public class ActiveStatusEffect
    {
        public StatusEffectType Type;
        public int              Duration;
        public int              Value;
    }

    public class Unit
    {
        public string Name      { get; }
        public int    MaxHP     { get; }
        public int    HP        { get; private set; }
        public int    Speed     { get; }
        public float  TurnMeter { get; private set; }

        public bool IsAlive => HP > 0;
        public bool IsReady => TurnMeter >= 100f;

        private readonly List<ActiveStatusEffect> _activeEffects = new();

        public Unit(string name, int hp, int speed)
        {
            Name  = name;
            MaxHP = hp;
            HP    = hp;
            Speed = speed;
        }

        public void AdvanceMeter(float dt)
        {
            if (IsAlive) TurnMeter += Speed * dt;
        }

        public void ConsumeMeter() => TurnMeter = 0f;

        public int TakeDamage(int amount)
        {
            int actual = Mathf.Min(amount, HP);
            HP = Mathf.Max(0, HP - amount);
            return actual;
        }

        public int Heal(int amount)
        {
            int actual = Mathf.Min(amount, MaxHP - HP);
            HP = Mathf.Min(MaxHP, HP + amount);
            return actual;
        }

        public void AddEffect(StatusEffectEntry entry)
        {
            _activeEffects.Add(new ActiveStatusEffect
            {
                Type     = entry.type,
                Duration = entry.duration,
                Value    = entry.value
            });
            Debug.Log($"{Name} gained {entry.type} ({entry.duration}T)");
        }

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

        public List<ActiveStatusEffect> GetEffects() => _activeEffects;

        public override string ToString() => $"{Name} ({HP}/{MaxHP} HP)";
    }
}
