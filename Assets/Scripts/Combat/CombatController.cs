using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Core;
using Data;

namespace Combat
{
    public class CombatController : MonoBehaviour
    {
        [Header("Encounter (optional — falls back to a built-in duel if empty)")]
        [SerializeField] private EncounterData _encounter;

        [Header("Roster resolver — maps the player's saved team ids to HeroData")]
        [SerializeField] private GachaPool _heroPool;

        private BattleManager _battle;

        private void Start()
        {
            GameManager.Instance?.SetState(GameState.Combat);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<SkillSelectedEvent>(OnSkillSelected);

            // Enemies always come from the encounter (or the built-in fallback).
            List<Unit> enemies = (_encounter != null && _encounter.enemies != null && _encounter.enemies.Length > 0)
                ? BuildTeam(_encounter.enemies, _encounter.enemyLevel)
                : DefaultEnemies();

            // Allies: the player's chosen roster if they have one, else the
            // encounter's fixed allies, else a built-in starter.
            List<Unit> allies = BuildPlayerTeam();
            if (allies == null || allies.Count == 0)
                allies = (_encounter != null && _encounter.allies != null && _encounter.allies.Length > 0)
                    ? BuildTeam(_encounter.allies, _encounter.allyLevel)
                    : DefaultAllies();

            _battle = gameObject.AddComponent<BattleManager>();
            _battle.Init(allies, enemies);
        }

        // ── Team construction ───────────────────────────────────────────────

        // The player's saved team, resolved from the gacha pool. Null when there
        // is no pool wired or no team picked yet.
        private List<Unit> BuildPlayerTeam()
        {
            if (_heroPool == null || _heroPool.heroes == null) return null;

            var ids = SaveSystem.Profile.teamHeroIds;
            if (ids == null || ids.Count == 0) return null;

            int level = _encounter != null ? _encounter.allyLevel : 1;
            var units = new List<Unit>();
            foreach (var id in ids)
            {
                var h = HeroById(id);
                if (h == null) continue;
                var u = Unit.FromHeroData(h, level);
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
            StartCoroutine(ReturnHome());
        }

        // After the result sinks in, head back to the Home hub (if it exists in
        // the build — guarded so opening Combat.unity directly still works).
        private IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(2.5f);
            if (Application.CanStreamedLevelBeLoaded("Home"))
                SceneManager.LoadScene("Home");
        }
    }

    public struct CombatEndEvent     { public bool Victory; }
    public struct SkillSelectedEvent { public int  SkillSlot; }
}
