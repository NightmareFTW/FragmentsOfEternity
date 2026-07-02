#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Data;

namespace RPG.EditorTools
{
    // Generates the starter ScriptableObject content (heroes + skills) so the
    // game runs from editable data assets instead of hardcoded values.
    // Run this BEFORE "RPG → Setup Combat Scene" so the scene can wire the assets.
    public static class ContentSetup
    {
        private const string SkillDir = "Assets/ScriptableObjects/Skills";
        private const string HeroDir  = "Assets/ScriptableObjects/Heroes";

        [MenuItem("RPG/Create Starter Content", priority = 0)]
        public static void CreateContent()
        {
            EnsureFolders();

            var slash = MakeSkill("Slash", "slash",
                SkillType.Damage, TargetType.SingleEnemy, 15, 30, canCrit: true, cooldown: 0,
                "A quick sword strike that leaves a wound.",
                onHit:  new[] { Effect(StatusEffectType.Bleed, 2, 12) });

            var heavy = MakeSkill("Heavy Blow", "heavy_blow",
                SkillType.Damage, TargetType.SingleEnemy, 25, 45, canCrit: true, cooldown: 2,
                "A powerful smash. Raises your attack and poisons the foe.",
                onHit:  new[] { Effect(StatusEffectType.Poison,   2, 20) },
                onSelf: new[] { Effect(StatusEffectType.AttackUp, 2, 25) });

            var recover = MakeSkill("Recover", "recover",
                SkillType.Heal, TargetType.Self, 60, 90, canCrit: false, cooldown: 3,
                "Restore health and steel your defences.",
                onSelf: new[] { Effect(StatusEffectType.DefenseUp, 2, 20) });

            MakeHero("Hero", "hero", Element.Light, HeroClass.Warrior, rarity: 5,
                hp: 500, atk: 110, def: 60, spd: 120, critRate: 0.15f, critDmg: 1.6f,
                hpGrowth: 55f, atkGrowth: 12f, defGrowth: 6f,
                slash, heavy, recover);

            MakeHero("Goblin", "goblin", Element.Earth, HeroClass.Warrior, rarity: 3,
                hp: 420, atk: 90, def: 40, spd: 90, critRate: 0.05f, critDmg: 1.5f,
                hpGrowth: 40f, atkGrowth: 9f, defGrowth: 4f,
                null, null, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RPG] Starter content created in Assets/ScriptableObjects/ " +
                      "(Hero, Goblin, Slash, Heavy Blow, Recover). " +
                      "Now run RPG → Setup Combat Scene to wire it into the scene.");
        }

        [MenuItem("RPG/Create Starter Content", validate = true)]
        static bool ValidateCreate() => !EditorApplication.isPlaying;

        // ── Builders ────────────────────────────────────────────────────────

        static SkillData MakeSkill(
            string name, string id, SkillType type, TargetType target,
            int min, int max, bool canCrit, int cooldown, string desc,
            StatusEffectEntry[] onHit = null, StatusEffectEntry[] onSelf = null)
        {
            string path = $"{SkillDir}/{Sanitize(name)}.asset";
            var sd = LoadOrCreate<SkillData>(path);

            sd.skillName     = name;
            sd.skillId       = id;
            sd.description   = desc;
            sd.skillType     = type;
            sd.targetType    = target;
            sd.minValue      = min;
            sd.maxValue      = max;
            sd.canCrit       = canCrit;
            sd.cooldownTurns = cooldown;
            sd.onHitEffects  = onHit  ?? new StatusEffectEntry[0];
            sd.onSelfEffects = onSelf ?? new StatusEffectEntry[0];

            EditorUtility.SetDirty(sd);
            return sd;
        }

        static HeroData MakeHero(
            string name, string id, Element element, HeroClass heroClass, int rarity,
            int hp, int atk, int def, int spd, float critRate, float critDmg,
            float hpGrowth, float atkGrowth, float defGrowth,
            SkillData s1, SkillData s2, SkillData s3)
        {
            string path = $"{HeroDir}/{Sanitize(name)}.asset";
            var hd = LoadOrCreate<HeroData>(path);

            hd.heroName       = name;
            hd.heroId         = id;
            hd.element        = element;
            hd.heroClass      = heroClass;
            hd.rarity         = Mathf.Clamp(rarity, 3, 5);
            hd.baseHP         = hp;
            hd.baseATK        = atk;
            hd.baseDEF        = def;
            hd.baseSPD        = spd;
            hd.baseCritRate   = critRate;
            hd.baseCritDamage = critDmg;
            hd.baseResistance = 0.15f;
            hd.baseAccuracy   = 0f;
            hd.hpGrowth       = hpGrowth;
            hd.atkGrowth      = atkGrowth;
            hd.defGrowth      = defGrowth;
            hd.skill1         = s1;
            hd.skill2         = s2;
            hd.skill3         = s3;

            EditorUtility.SetDirty(hd);
            return hd;
        }

        static StatusEffectEntry Effect(StatusEffectType type, int duration, int value) =>
            new StatusEffectEntry { type = type, duration = duration, value = value };

        // ── Helpers ─────────────────────────────────────────────────────────

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(SkillDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Skills");
            if (!AssetDatabase.IsValidFolder(HeroDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Heroes");
        }

        static string Sanitize(string name) => name.Replace(" ", "");
    }
}
#endif
