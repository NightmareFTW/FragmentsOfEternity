using UnityEngine;
using Core;
using Data;

namespace Combat
{
    public class CombatController : MonoBehaviour
    {
        private BattleManager _battle;

        private void Start()
        {
            GameManager.Instance?.SetState(GameState.Combat);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<SkillSelectedEvent>(OnSkillSelected);

            var player = new Unit("Hero",   hp: 500, speed: 120);
            var enemy  = new Unit("Goblin", hp: 300, speed:  90);

            var skills = new SkillData[3];
            skills[0] = SkillData.Make("Slash",      SkillType.Damage, 15, 30, "A quick sword strike.",      cooldown: 0);

            skills[1] = SkillData.Make("Heavy Blow", SkillType.Damage, 25, 45, "A powerful overhead smash.", cooldown: 2);
            skills[1].onSelfEffects = new[] { new StatusEffectEntry { type = StatusEffectType.AttackUp,  duration = 2, value = 20 } };

            skills[2] = SkillData.Make("Recover",    SkillType.Heal,   20, 35, "Restore some health.",       cooldown: 3);
            skills[2].onSelfEffects = new[] { new StatusEffectEntry { type = StatusEffectType.DefenseUp, duration = 2, value = 15 } };

            _battle = gameObject.AddComponent<BattleManager>();
            _battle.Init(player, enemy, skills);
        }

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
