using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core;
using Data;

namespace Combat
{
    public class BattleManager : MonoBehaviour
    {
        public IReadOnlyList<Unit> Allies  => _allies;
        public IReadOnlyList<Unit> Enemies => _enemies;

        private readonly List<Unit> _allies  = new();
        private readonly List<Unit> _enemies = new();
        private readonly List<Unit> _units   = new();   // everyone, for turn order

        private Unit _activeAlly;      // ally currently awaiting player input
        private Unit _currentTarget;   // highlighted enemy target
        private bool _awaitingInput;
        private bool _combatOver;

        public void Init(List<Unit> allies, List<Unit> enemies)
        {
            RegisterTeam(allies,  Team.Player);
            RegisterTeam(enemies, Team.Enemy);
            _units.AddRange(_allies);
            _units.AddRange(_enemies);

            EventBus.Subscribe<UnitClickedEvent>(OnUnitClicked);
            EventBus.Raise(new CombatInitEvent { Allies = _allies, Enemies = _enemies });
            StartCoroutine(TickLoop());
        }

        private void OnDestroy() => EventBus.Unsubscribe<UnitClickedEvent>(OnUnitClicked);

        // Player tapped a unit — retarget if it's a legal choice this turn.
        private void OnUnitClicked(UnitClickedEvent evt)
        {
            if (_combatOver || !_awaitingInput) return;
            if (evt.Unit == null || !evt.Unit.IsAlive) return;
            _currentTarget = evt.Unit;
            EventBus.Raise(new TargetChangedEvent { Target = _currentTarget });
        }

        private void RegisterTeam(List<Unit> team, Team side)
        {
            for (int i = 0; i < team.Count; i++)
            {
                team[i].Team = side;
                team[i].Slot = i;
                (side == Team.Player ? _allies : _enemies).Add(team[i]);
            }
        }

        // ── Player input ────────────────────────────────────────────────────

        public void UseSkill(int slot)
        {
            if (_combatOver || !_awaitingInput || _activeAlly == null) return;
            if (slot < 0 || slot >= _activeAlly.Skills.Length) return;
            if (!_activeAlly.IsSkillReady(slot)) return;

            _awaitingInput = false;

            var skill  = _activeAlly.Skills[slot];
            var target = ResolveTarget(skill, _activeAlly);
            ExecuteSkill(_activeAlly, target, skill);
            _activeAlly.PutOnCooldown(slot);
            RaiseCooldowns(_activeAlly);
            StartCoroutine(EndTurnDelayed());
        }

        // Auto-targeting for now (manual selection lands in the next part):
        // heal   → the most-wounded ally (self included);
        // damage → the highlighted enemy, else the first alive enemy.
        private Unit ResolveTarget(SkillData skill, Unit caster)
        {
            if (skill != null && skill.skillType == SkillType.Buff)
                return caster;

            if (skill != null && skill.skillType == SkillType.Heal)
            {
                // Honour a hand-picked ally; otherwise heal whoever is most hurt.
                if (_currentTarget != null && _currentTarget.IsAlive && _currentTarget.Team == Team.Player)
                    return _currentTarget;
                return MostWounded(_allies) ?? caster;
            }

            // Damage — honour a hand-picked enemy; otherwise the first alive enemy.
            if (_currentTarget != null && _currentTarget.IsAlive && _currentTarget.Team == Team.Enemy)
                return _currentTarget;
            return FirstAlive(_enemies);
        }

        private void ExecuteSkill(Unit caster, Unit target, SkillData skill)
        {
            if (skill == null || target == null) return;
            EventBus.Raise(new SkillUsedEvent { Skill = skill, Caster = caster, Target = target });

            switch (skill.skillType)
            {
                case SkillType.Damage:
                    int roll   = Random.Range(skill.minValue, skill.maxValue + 1);
                    int dmg    = ComputeDamage(caster, target, roll, skill.canCrit, out bool crit);
                    int actual = target.TakeDamage(dmg);
                    EventBus.Raise(new UnitDamagedEvent { Target = target, Damage = actual, IsCrit = crit });
                    ApplyEffects(skill.onHitEffects, target);
                    break;

                case SkillType.Heal:
                    int heal   = Random.Range(skill.minValue, skill.maxValue + 1);
                    int healed = target.Heal(heal);
                    EventBus.Raise(new UnitHealedEvent { Target = target, Amount = healed });
                    break;

                case SkillType.Buff:
                    break;
            }

            ApplyEffects(skill.onSelfEffects, caster);
        }

        private IEnumerator EndTurnDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            EndTurn();
        }

        // ── Tick loop ───────────────────────────────────────────────────────

        private IEnumerator TickLoop()
        {
            while (TeamAlive(_allies) && TeamAlive(_enemies))
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
            EventBus.Raise(new CombatEndEvent { Victory = !TeamAlive(_enemies) });
        }

        private IEnumerator ExecuteTurn(Unit actor)
        {
            actor.ConsumeMeter();

            // Damage-over-time resolves first — a unit can die on its own turn.
            int dot = actor.TickDamageOverTime();
            if (dot > 0)
            {
                EventBus.Raise(new UnitDamagedEvent { Target = actor, Damage = dot, IsDoT = true });
                yield return new WaitForSeconds(0.45f);
            }
            if (!actor.IsAlive) { EndTurn(); yield break; }

            // Snapshot control state BEFORE durations tick down this turn.
            bool stunned = actor.IsStunned;
            actor.TickEffects();
            EventBus.Raise(new StatusEffectAppliedEvent { Target = actor });

            if (stunned)
            {
                EventBus.Raise(new TurnStartedEvent { Actor = actor });
                EventBus.Raise(new UnitStunnedEvent  { Actor = actor });
                yield return new WaitForSeconds(0.7f);
                EndTurn();
                yield break;
            }

            if (actor.Team == Team.Enemy)
            {
                EventBus.Raise(new TurnStartedEvent { Actor = actor });
                yield return new WaitForSeconds(0.7f);
                EnemyAct(actor);
                yield return new WaitForSeconds(0.5f);
                EndTurn();
            }
            else
            {
                actor.TickCooldowns();
                _activeAlly = actor;
                RaiseCooldowns(actor);                                    // HUD caches ticked cooldowns
                EventBus.Raise(new TurnStartedEvent { Actor = actor });   // then enables buttons
                _currentTarget = FirstAlive(_enemies);
                EventBus.Raise(new TargetChangedEvent { Target = _currentTarget });
                _awaitingInput = true;
            }
        }

        private void EndTurn()
        {
            _awaitingInput = false;
            _activeAlly    = null;
            ClearTarget();
        }

        private void ClearTarget()
        {
            _currentTarget = null;
            EventBus.Raise(new TargetChangedEvent { Target = null });
        }

        // ── Enemy AI ────────────────────────────────────────────────────────

        private void EnemyAct(Unit enemy)
        {
            Unit hero = RandomAlive(_allies);
            if (hero == null) return;

            SkillData skill = PickEnemySkill(enemy);
            if (skill != null)
            {
                Unit target = skill.skillType == SkillType.Heal ? enemy : hero;
                ExecuteSkill(enemy, target, skill);
                enemy.PutOnCooldown(System.Array.IndexOf(enemy.Skills, skill));
            }
            else
            {
                // No skills authored → a plain basic attack.
                int roll   = Random.Range(10, 17);
                int dmg    = ComputeDamage(enemy, hero, roll, true, out bool crit);
                int actual = hero.TakeDamage(dmg);
                EventBus.Raise(new UnitDamagedEvent { Target = hero, Damage = actual, IsCrit = crit });
            }
        }

        private SkillData PickEnemySkill(Unit enemy)
        {
            var ready = new List<int>();
            for (int i = 0; i < enemy.Skills.Length; i++)
                if (enemy.Skills[i] != null && enemy.IsSkillReady(i)) ready.Add(i);
            if (ready.Count == 0) return null;
            return enemy.Skills[ready[Random.Range(0, ready.Count)]];
        }

        // ── Combat math ──────────────────────────────────────────────────────

        private int ComputeDamage(Unit caster, Unit target, int roll, bool canCrit, out bool isCrit)
        {
            float raw       = roll * (caster.EffectiveAttack / 100f);
            float mitigated = raw * (100f / (100f + target.EffectiveDefense));
            isCrit = canCrit && Random.value < caster.CritRate;
            if (isCrit) mitigated *= caster.CritDamage;
            return Mathf.Max(1, Mathf.RoundToInt(mitigated));
        }

        private void ApplyEffects(StatusEffectEntry[] effects, Unit target)
        {
            if (effects == null || effects.Length == 0 || target == null) return;
            foreach (var e in effects)
            {
                target.AddEffect(e);
                EventBus.Raise(new StatusEffectAppliedEvent { Target = target });
            }
        }

        private void RaiseCooldowns(Unit ally) =>
            EventBus.Raise(new SkillCooldownsChangedEvent
            {
                Owner     = ally,
                Skills    = ally.Skills,
                Cooldowns = ally.CooldownSnapshot()
            });

        // ── Team helpers ──────────────────────────────────────────────────────

        private Unit NextReady()
        {
            Unit best = null;
            foreach (var u in _units)
                if (u.IsAlive && u.IsReady && (best == null || u.TurnMeter > best.TurnMeter))
                    best = u;
            return best;
        }

        private static bool TeamAlive(List<Unit> team)
        {
            foreach (var u in team) if (u.IsAlive) return true;
            return false;
        }

        private static Unit FirstAlive(List<Unit> team)
        {
            foreach (var u in team) if (u.IsAlive) return u;
            return null;
        }

        private static Unit RandomAlive(List<Unit> team)
        {
            var alive = new List<Unit>();
            foreach (var u in team) if (u.IsAlive) alive.Add(u);
            return alive.Count == 0 ? null : alive[Random.Range(0, alive.Count)];
        }

        private static Unit MostWounded(List<Unit> team)
        {
            Unit  worst      = null;
            float worstRatio = 1f;
            foreach (var u in team)
            {
                if (!u.IsAlive) continue;
                float ratio = (float)u.HP / u.MaxHP;
                if (worst == null || ratio < worstRatio) { worst = u; worstRatio = ratio; }
            }
            return worst;
        }
    }

    public struct SkillUsedEvent              { public SkillData Skill; public Unit Caster; public Unit Target; }
    public struct CombatInitEvent            { public List<Unit> Allies; public List<Unit> Enemies; }
    public struct TurnStartedEvent           { public Unit Actor; }
    public struct UnitDamagedEvent           { public Unit Target; public int Damage; public bool IsCrit; public bool IsDoT; }
    public struct UnitHealedEvent            { public Unit Target; public int Amount; }
    public struct UnitStunnedEvent           { public Unit Actor; }
    public struct TargetChangedEvent         { public Unit Target; }
    public struct UnitClickedEvent           { public Unit Unit; }
    public struct SkillCooldownsChangedEvent  { public Unit Owner; public SkillData[] Skills; public int[] Cooldowns; }
    public struct StatusEffectAppliedEvent    { public Unit Target; }
}
