using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core;
using Data;

namespace Combat
{
    public class BattleManager : MonoBehaviour
    {
        public Unit Player { get; private set; }
        public Unit Enemy  { get; private set; }

        private readonly List<Unit> _units = new();
        private SkillData[] _skills;
        private int[]       _cooldowns;
        private Unit _currentTarget;
        private bool _awaitingInput;
        private bool _combatOver;

        public void Init(Unit player, Unit enemy, SkillData[] skills)
        {
            Player     = player;
            Enemy      = enemy;
            _skills    = skills;
            _cooldowns = new int[skills.Length];   // all zeros
            _units.Add(player);
            _units.Add(enemy);
            EventBus.Raise(new CombatInitEvent { Player = player, Enemy = enemy, PlayerSkills = skills });
            RaiseCooldowns();   // give HUD the initial zero state so labels are set up
            StartCoroutine(TickLoop());
        }

        public void UseSkill(int slot)
        {
            if (_combatOver) return;
            if (!_awaitingInput) return;
            if (_skills == null || slot < 0 || slot >= _skills.Length) return;

            var skill = _skills[slot];

            if (_cooldowns[slot] > 0)
                return;   // keep awaiting input — player can still choose another skill

            // Lock input immediately so any re-clicks during EndTurnDelayed are rejected.
            // This must happen before executing the skill so re-entrant calls fail the
            // !_awaitingInput guard even if they arrive in the same frame.
            _awaitingInput = false;

            var dmgTarget = _currentTarget ?? Enemy;
            EventBus.Raise(new SkillUsedEvent { Skill = skill, Caster = Player, Target = dmgTarget });

            switch (skill.skillType)
            {
                case SkillType.Damage:
                    int dmg    = Random.Range(skill.minValue, skill.maxValue + 1);
                    int actual = dmgTarget.TakeDamage(dmg);
                    EventBus.Raise(new UnitDamagedEvent { Target = dmgTarget, Damage = actual });
                    ApplyEffects(skill.onHitEffects, dmgTarget);
                    break;

                case SkillType.Heal:
                    int heal   = Random.Range(skill.minValue, skill.maxValue + 1);
                    int healed = Player.Heal(heal);
                    EventBus.Raise(new UnitHealedEvent { Target = Player, Amount = healed });
                    break;

                case SkillType.Buff:
                    break;
            }

            ApplyEffects(skill.onSelfEffects, Player);
            _cooldowns[slot] = skill.cooldownTurns;
            RaiseCooldowns();                    // UI sees cooldown set immediately
            StartCoroutine(EndTurnDelayed());
        }

        private IEnumerator EndTurnDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            EndTurn();
        }

        // ── Tick loop ─────────────────────────────────────────────────────

        private IEnumerator TickLoop()
        {
            while (Player.IsAlive && Enemy.IsAlive)
            {
                if (_awaitingInput) { yield return null; continue; }

                foreach (var u in _units) u.AdvanceMeter(Time.deltaTime);

                Unit ready = NextReady();
                if (ready != null)
                    yield return StartCoroutine(ExecuteTurn(ready));

                yield return null;
            }

            _combatOver = true;
            ClearTarget();
            EventBus.Raise(new CombatEndEvent { Victory = !Enemy.IsAlive });
        }

        private IEnumerator ExecuteTurn(Unit actor)
        {
            actor.ConsumeMeter();

            if (actor == Enemy)
            {
                actor.TickEffects();
                EventBus.Raise(new StatusEffectAppliedEvent { Target = actor });
                EventBus.Raise(new TurnStartedEvent { Actor = actor });
                yield return new WaitForSeconds(0.8f);
                int dmg    = Random.Range(8, 18);
                int actual = Player.TakeDamage(dmg);
                EventBus.Raise(new UnitDamagedEvent { Target = Player, Damage = actual });
                yield return new WaitForSeconds(0.5f);
                EndTurn();
            }
            else
            {
                actor.TickEffects();
                EventBus.Raise(new StatusEffectAppliedEvent { Target = actor });
                // Tick cooldowns BEFORE announcing the turn — UI caches ticked values
                // before TurnStartedEvent causes the HUD to enable buttons.
                TickCooldowns();
                EventBus.Raise(new TurnStartedEvent { Actor = actor });
                _currentTarget = Enemy;
                EventBus.Raise(new TargetChangedEvent { Target = Enemy });
                _awaitingInput = true;
            }
        }

        private void EndTurn()
        {
            _awaitingInput = false;
            ClearTarget();
        }

        private void ClearTarget()
        {
            _currentTarget = null;
            EventBus.Raise(new TargetChangedEvent { Target = null });
        }

        private void TickCooldowns()
        {
            for (int i = 0; i < _cooldowns.Length; i++)
                if (_cooldowns[i] > 0) _cooldowns[i]--;
            RaiseCooldowns();
        }

        private void RaiseCooldowns() =>
            EventBus.Raise(new SkillCooldownsChangedEvent
            {
                Skills    = _skills,
                Cooldowns = (int[])_cooldowns.Clone()
            });

        private void ApplyEffects(StatusEffectEntry[] effects, Unit target)
        {
            if (effects == null || effects.Length == 0) return;
            foreach (var e in effects)
            {
                target.AddEffect(e);
                EventBus.Raise(new StatusEffectAppliedEvent { Target = target });
            }
        }

        private Unit NextReady()
        {
            Unit best = null;
            foreach (var u in _units)
                if (u.IsReady && (best == null || u.TurnMeter > best.TurnMeter))
                    best = u;
            return best;
        }
    }

    public struct SkillUsedEvent              { public SkillData Skill; public Unit Caster; public Unit Target; }
    public struct CombatInitEvent            { public Unit Player; public Unit Enemy; public SkillData[] PlayerSkills; }
    public struct TurnStartedEvent           { public Unit Actor; }
    public struct UnitDamagedEvent           { public Unit Target; public int Damage; }
    public struct UnitHealedEvent            { public Unit Target; public int Amount; }
    public struct TargetChangedEvent         { public Unit Target; }
    public struct SkillCooldownsChangedEvent  { public SkillData[] Skills; public int[] Cooldowns; }
    public struct StatusEffectAppliedEvent    { public Unit Target; }
}
