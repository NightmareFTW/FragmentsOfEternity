using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "NewHero", menuName = "RPG/Hero Data")]
    public class HeroData : ScriptableObject
    {
        [Header("Identity")]
        public string heroName;
        public string heroId;
        public Sprite portrait;
        public Element element;
        public HeroClass heroClass;
        [Range(3, 5)] public int rarity;

        [Header("Base Stats — Level 1")]
        public int baseHP;
        public int baseATK;
        public int baseDEF;
        public int baseSPD;
        [Range(0f, 1f)] public float baseCritRate;
        public float baseCritDamage;
        [Range(0f, 1f)] public float baseResistance;
        [Range(0f, 1f)] public float baseAccuracy;

        [Header("Stat Growth per Level")]
        public float hpGrowth;
        public float atkGrowth;
        public float defGrowth;

        [Header("Skills")]
        public SkillData skill1;
        public SkillData skill2;
        public SkillData skill3;

        // The assigned skill slots, in order, skipping any left empty.
        public SkillData[] Skills()
        {
            var list = new List<SkillData>(3);
            if (skill1 != null) list.Add(skill1);
            if (skill2 != null) list.Add(skill2);
            if (skill3 != null) list.Add(skill3);
            return list.ToArray();
        }
    }

    public enum Element  { Fire, Ice, Earth, Light, Dark }
    public enum HeroClass { Warrior, Mage, Ranger, Knight, Thief, Manauser }
}
