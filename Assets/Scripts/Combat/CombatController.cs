using UnityEngine;
using Core;
using Data;

namespace Combat
{
    public class CombatController : MonoBehaviour
    {
        [Header("Combatants (optional — falls back to built-in starters if empty)")]
        [SerializeField] private HeroData _playerHero;
        [SerializeField] private HeroData _enemyHero;
        [SerializeField] private int _playerLevel = 1;
        [SerializeField] private int _enemyLevel  = 1;

        private BattleManager _battle;

        private void Start()
        {
            GameManager.Instance?.SetState(GameState.Combat);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<SkillSelectedEvent>(OnSkillSelected);

            Unit player = BuildPlayer(out SkillData[] skills);
            Unit enemy  = BuildEnemy();

            _battle = gameObject.AddComponent<BattleManager>();
            _battle.Init(player, enemy, skills);
        }

        // ── Combatant construction ──────────────────────────────────────────

        private Unit BuildPlayer(out SkillData[] skills)
        {
            if (_playerHero != null)
            {
                skills = _playerHero.Skills();
                return Unit.FromHeroData(_playerHero, _playerLevel);
            }
            skills = DefaultPlayerSkills();
            return new Unit("Hero", 500, 120, 110, 60, 0.15f, 1.6f);
        }

        private Unit BuildEnemy()
        {
            if (_enemyHero != null)
                return Unit.FromHeroData(_enemyHero, _enemyLevel);
            return new Unit("Goblin", 420, 90, 90, 40, 0.05f, 1.5f);
        }

        // Built-in starter kit — only used when no HeroData asset is assigned, so
        // the scene still plays before "RPG → Create Starter Content" has run.
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
