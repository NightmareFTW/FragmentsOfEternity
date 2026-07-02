using System.Collections.Generic;
using UnityEngine;
using Core;
using Data;

namespace Combat
{
    public class CombatController : MonoBehaviour
    {
        [Header("Encounter (optional — falls back to a built-in duel if empty)")]
        [SerializeField] private EncounterData _encounter;

        private BattleManager _battle;

        private void Start()
        {
            GameManager.Instance?.SetState(GameState.Combat);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<SkillSelectedEvent>(OnSkillSelected);

            List<Unit> allies, enemies;
            if (_encounter != null && HasTeams(_encounter))
            {
                allies  = BuildTeam(_encounter.allies,  _encounter.allyLevel);
                enemies = BuildTeam(_encounter.enemies, _encounter.enemyLevel);
            }
            else
            {
                allies  = DefaultAllies();
                enemies = DefaultEnemies();
            }

            _battle = gameObject.AddComponent<BattleManager>();
            _battle.Init(allies, enemies);
        }

        // ── Team construction ───────────────────────────────────────────────

        private static bool HasTeams(EncounterData e) =>
            e.allies != null && e.allies.Length > 0 && e.enemies != null && e.enemies.Length > 0;

        private static List<Unit> BuildTeam(HeroData[] heroes, int level)
        {
            var team = new List<Unit>();
            if (heroes == null) return team;
            foreach (var h in heroes)
            {
                if (h == null) continue;
                var unit = Unit.FromHeroData(h, level);
                unit.SetSkills(h.Skills());
                team.Add(unit);
            }
            return team;
        }

        // ── Built-in fallback (used only when no EncounterData is assigned) ──

        private static List<Unit> DefaultAllies()
        {
            var hero = new Unit("Hero", 500, 120, 110, 60, 0.15f, 1.6f);
            hero.SetSkills(DefaultPlayerSkills());
            return new List<Unit> { hero };
        }

        private static List<Unit> DefaultEnemies()
        {
            var goblin = new Unit("Goblin", 420, 90, 90, 40, 0.05f, 1.5f);
            goblin.SetSkills(new SkillData[0]);   // no skills → basic-attack AI
            return new List<Unit> { goblin };
        }

        private static SkillData[] DefaultPlayerSkills()
        {
            var slash = SkillData.Make("Slash", SkillType.Damage, 15, 30,
                "A quick sword strike that leaves a wound.", cooldown: 0);
            slash.canCrit      = true;
            slash.onHitEffects = new[] { new StatusEffectEntry { type = StatusEffectType.Bleed, duration = 2, value = 12 } };

            var heavy = SkillData.Make("Heavy Blow", SkillType.Damage, 25, 45,
                "A powerful smash. Raises your attack and poisons the foe.", cooldown: 2);
            heavy.canCrit       = true;
            heavy.onSelfEffects = new[] { new StatusEffectEntry { type = StatusEffectType.AttackUp, duration = 2, value = 25 } };
            heavy.onHitEffects  = new[] { new StatusEffectEntry { type = StatusEffectType.Poison,   duration = 2, value = 20 } };

            var recover = SkillData.Make("Recover", SkillType.Heal, 60, 90,
                "Restore health and steel your defences.", cooldown: 3);
            recover.onSelfEffects = new[] { new StatusEffectEntry { type = StatusEffectType.DefenseUp, duration = 2, value = 20 } };

            return new[] { slash, heavy, recover };
        }

        // ── Lifecycle / events ──────────────────────────────────────────────

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Unsubscribe<SkillSelectedEvent>(OnSkillSelected);
        }

        private void OnSkillSelected(SkillSelectedEvent evt) => _battle?.UseSkill(evt.SkillSlot);

        private void OnCombatEnd(CombatEndEvent evt)
        {
            Debug.Log($"[Combat] ── {(evt.Victory ? "VICTORY" : "DEFEAT")} ──");
            GameManager.Instance?.SetState(evt.Victory ? GameState.Results : GameState.MainMenu);
        }
    }

    public struct CombatEndEvent     { public bool Victory; }
    public struct SkillSelectedEvent { public int  SkillSlot; }
}
