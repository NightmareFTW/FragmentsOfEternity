using System.Collections.Generic;
using UnityEngine;
using Core;
using Data;

namespace Combat
{
    public class CombatController : MonoBehaviour
    {
        [Header("Encounter (fallback when no campaign stage is selected)")]
        [SerializeField] private EncounterData _encounter;

        [Header("Campaign — the selected stage's encounter wins if set")]
        [SerializeField] private CampaignData _campaign;

        [Header("Roster resolver — maps the player's saved team ids to HeroData")]
        [SerializeField] private GachaPool _heroPool;

        private BattleManager _battle;

        private void Start()
        {
            GameManager.Instance?.SetState(GameState.Combat);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<SkillSelectedEvent>(OnSkillSelected);

            var encounter = ResolveEncounter();

            // Enemies always come from the encounter (or the built-in fallback).
            List<Unit> enemies = (encounter != null && encounter.enemies != null && encounter.enemies.Length > 0)
                ? BuildTeam(encounter.enemies, encounter.enemyLevel)
                : DefaultEnemies();

            // Allies: the player's chosen roster if they have one, else the
            // encounter's fixed allies, else a built-in starter.
            List<Unit> allies = BuildPlayerTeam();
            if (allies == null || allies.Count == 0)
                allies = (encounter != null && encounter.allies != null && encounter.allies.Length > 0)
                    ? BuildTeam(encounter.allies, encounter.allyLevel)
                    : DefaultAllies();

            _battle = gameObject.AddComponent<BattleManager>();
            _battle.Init(allies, enemies);
        }

        // ── Team construction ───────────────────────────────────────────────

        // The campaign's selected-stage encounter wins; else the wired fallback.
        private EncounterData ResolveEncounter()
        {
            if (_campaign != null && _campaign.stages != null)
            {
                int i = CampaignState.SelectedStage;
                if (i >= 0 && i < _campaign.stages.Length && _campaign.stages[i].encounter != null)
                    return _campaign.stages[i].encounter;
            }
            return _encounter;
        }

        // The player's saved team, resolved from the gacha pool, each at its own
        // trained level. Null when there is no pool wired or no team picked yet.
        private List<Unit> BuildPlayerTeam()
        {
            if (_heroPool == null || _heroPool.heroes == null) return null;

            var ids = SaveSystem.Profile.teamHeroIds;
            if (ids == null || ids.Count == 0) return null;

            var units = new List<Unit>();
            foreach (var id in ids)
            {
                var h = HeroById(id);
                if (h == null) continue;
                var u = Unit.FromHeroData(h, ProgressionService.GetLevel(id));
                u.SetSkills(h.Skills());
                units.Add(u);
            }
            return units;
        }

        private HeroData HeroById(string id)
        {
            foreach (var h in _heroPool.heroes)
                if (h != null && h.heroId == id) return h;
            return null;
        }

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
            // The result overlay (CombatResultUI) grants rewards and returns Home.
        }
    }

    public struct CombatEndEvent     { public bool Victory; }
    public struct SkillSelectedEvent { public int  SkillSlot; }
}
