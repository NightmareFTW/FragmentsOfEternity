#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Data;

namespace RPG.EditorTools
{
    // Generates the starter ScriptableObject content (skills, heroes, and a 4v4
    // encounter) so the game runs from editable data assets instead of hardcoded
    // values. Run this BEFORE "RPG → Setup Combat Scene" so the scene can size
    // itself to the encounter and wire the assets.
    public static class ContentSetup
    {
        private const string SkillDir     = "Assets/ScriptableObjects/Skills";
        private const string HeroDir      = "Assets/ScriptableObjects/Heroes";
        private const string EncounterDir = "Assets/ScriptableObjects/Encounters";

        [MenuItem("RPG/Create Starter Content", priority = 0)]
        public static void CreateContent()
        {
            EnsureFolders();

            // ── Skills ────────────────────────────────────────────────────
            var slash = MakeSkill("Slash", "slash",
                SkillType.Damage, TargetType.SingleEnemy, 15, 30, canCrit: true, cooldown: 0,
                "A quick sword strike that leaves a wound.",
                onHit: new[] { Effect(StatusEffectType.Bleed, 2, 12) });

            var heavy = MakeSkill("Heavy Blow", "heavy_blow",
                SkillType.Damage, TargetType.SingleEnemy, 25, 45, canCrit: true, cooldown: 2,
                "A powerful smash. Raises your attack and poisons the foe.",
                onHit:  new[] { Effect(StatusEffectType.Poison,   2, 20) },
                onSelf: new[] { Effect(StatusEffectType.AttackUp, 2, 25) });

            var recover = MakeSkill("Recover", "recover",
                SkillType.Heal, TargetType.Self, 60, 90, canCrit: false, cooldown: 3,
                "Restore health and steel your defences.",
                onSelf: new[] { Effect(StatusEffectType.DefenseUp, 2, 20) });

            var fireball = MakeSkill("Fireball", "fireball",
                SkillType.Damage, TargetType.SingleEnemy, 30, 55, canCrit: true, cooldown: 2,
                "A burst of flame that scorches the target.",
                onHit: new[] { Effect(StatusEffectType.Burn, 2, 18) });

            var mend = MakeSkill("Mend", "mend",
                SkillType.Heal, TargetType.SingleAlly, 70, 110, canCrit: false, cooldown: 2,
                "Channel restorative light to the most wounded ally.");

            var guard = MakeSkill("Guard", "guard",
                SkillType.Buff, TargetType.Self, 0, 0, canCrit: false, cooldown: 3,
                "Brace yourself, sharply raising defence.",
                onSelf: new[] { Effect(StatusEffectType.DefenseUp, 2, 40) });

            // ── Allies ────────────────────────────────────────────────────
            var hero = MakeHero("Hero", "hero", Element.Light, HeroClass.Warrior, rarity: 5,
                hp: 500, atk: 110, def: 60, spd: 120, critRate: 0.15f, critDmg: 1.6f,
                hpGrowth: 55f, atkGrowth: 12f, defGrowth: 6f,
                slash, heavy, recover);

            var knight = MakeHero("Knight", "knight", Element.Ice, HeroClass.Knight, rarity: 4,
                hp: 650, atk: 95, def: 85, spd: 95, critRate: 0.08f, critDmg: 1.5f,
                hpGrowth: 70f, atkGrowth: 9f, defGrowth: 9f,
                slash, guard, heavy);

            var mage = MakeHero("Mage", "mage", Element.Fire, HeroClass.Mage, rarity: 5,
                hp: 420, atk: 130, def: 45, spd: 105, critRate: 0.18f, critDmg: 1.7f,
                hpGrowth: 45f, atkGrowth: 15f, defGrowth: 4f,
                fireball, slash, recover);

            var cleric = MakeHero("Cleric", "cleric", Element.Light, HeroClass.Manauser, rarity: 4,
                hp: 480, atk: 85, def: 55, spd: 110, critRate: 0.10f, critDmg: 1.5f,
                hpGrowth: 50f, atkGrowth: 8f, defGrowth: 6f,
                mend, slash, recover);

            // ── Enemies ───────────────────────────────────────────────────
            var goblin = MakeHero("Goblin", "goblin", Element.Earth, HeroClass.Warrior, rarity: 3,
                hp: 420, atk: 90, def: 40, spd: 90, critRate: 0.05f, critDmg: 1.5f,
                hpGrowth: 40f, atkGrowth: 9f, defGrowth: 4f,
                slash, null, null);

            var grunt = MakeHero("Goblin Grunt", "goblin_grunt", Element.Earth, HeroClass.Warrior, rarity: 3,
                hp: 380, atk: 85, def: 38, spd: 95, critRate: 0.05f, critDmg: 1.5f,
                hpGrowth: 36f, atkGrowth: 8f, defGrowth: 4f,
                slash, null, null);

            var orc = MakeHero("Orc", "orc", Element.Dark, HeroClass.Warrior, rarity: 4,
                hp: 600, atk: 100, def: 70, spd: 75, critRate: 0.05f, critDmg: 1.5f,
                hpGrowth: 65f, atkGrowth: 10f, defGrowth: 7f,
                heavy, null, null);

            var wolf = MakeHero("Wolf", "wolf", Element.Dark, HeroClass.Thief, rarity: 3,
                hp: 350, atk: 95, def: 35, spd: 130, critRate: 0.10f, critDmg: 1.5f,
                hpGrowth: 34f, atkGrowth: 9f, defGrowth: 3f,
                slash, null, null);

            // ── Encounter ─────────────────────────────────────────────────
            MakeEncounter("Intro Skirmish", "intro_skirmish",
                new[] { hero, knight, mage, cleric },
                new[] { goblin, grunt, orc, wolf },
                allyLevel: 1, enemyLevel: 1);

            // ── Gacha pool (the four summonable heroes) ───────────────────
            MakeGachaPool(new[] { hero, knight, mage, cleric }, summonCost: 300);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RPG] Starter content created: 6 skills, 8 heroes, the " +
                      "'Intro Skirmish' 4v4 encounter, and a GachaPool in " +
                      "Assets/ScriptableObjects/. Now run RPG → Setup Combat Scene " +
                      "and RPG → Setup Home Scene.");
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

        static void MakeGachaPool(HeroData[] heroes, int summonCost)
        {
            var gp = LoadOrCreate<GachaPool>("Assets/ScriptableObjects/GachaPool.asset");
            gp.heroes     = heroes;
            gp.summonCost = summonCost;
            EditorUtility.SetDirty(gp);
        }

        static void MakeEncounter(
            string name, string id, HeroData[] allies, HeroData[] enemies,
            int allyLevel, int enemyLevel)
        {
            string path = $"{EncounterDir}/{Sanitize(name)}.asset";
            var ed = LoadOrCreate<EncounterData>(path);

            ed.encounterName = name;
            ed.allies        = allies;
            ed.enemies       = enemies;
            ed.allyLevel     = allyLevel;
            ed.enemyLevel    = enemyLevel;

            EditorUtility.SetDirty(ed);
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
            if (!AssetDatabase.IsValidFolder(EncounterDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Encounters");
        }

        static string Sanitize(string name) => name.Replace(" ", "");
    }
}
#endif
