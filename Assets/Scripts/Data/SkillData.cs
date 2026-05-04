using UnityEngine;

namespace Data
{
    public enum SkillType { Damage, Heal, Buff }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "RPG/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("Identity")]
        public string skillName;
        public string skillId;
        [TextArea(2, 4)] public string description;
        public Sprite  icon;

        [Header("Type & Targeting")]
        public SkillType  skillType;
        public TargetType targetType;

        [Header("Value Range")]
        public int  minValue;
        public int  maxValue;
        public bool canCrit;

        [Header("Cooldown")]
        [Min(0)] public int cooldownTurns;

        [Header("Effects")]
        public StatusEffectEntry[] onHitEffects;
        public StatusEffectEntry[] onSelfEffects;

        public static SkillData Make(string name, SkillType type, int min, int max, string desc = "", int cooldown = 0)
        {
            var sd           = CreateInstance<SkillData>();
            sd.skillName     = name;
            sd.skillType     = type;
            sd.minValue      = min;
            sd.maxValue      = max;
            sd.description   = desc;
            sd.cooldownTurns = cooldown;
            return sd;
        }
    }

    public enum TargetType { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }

    [System.Serializable]
    public class StatusEffectEntry
    {
        public StatusEffectType type;
        [Min(1)] public int duration;
        public int value;
    }

    public enum StatusEffectType
    {
        None,
        Stun, Sleep, Silence,
        Poison, Burn, Bleed,
        Shield, Barrier,
        AttackUp, DefenseUp, SpeedBuff,
        AttackDebuff, DefenseDebuff, SpeedDebuff
    }
}
