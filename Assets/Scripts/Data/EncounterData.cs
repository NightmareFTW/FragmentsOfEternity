using UnityEngine;

namespace Data
{
    // Defines a single battle: the player's team and the enemy team.
    // Later, campaign stages will reference these.
    [CreateAssetMenu(fileName = "NewEncounter", menuName = "RPG/Encounter Data")]
    public class EncounterData : ScriptableObject
    {
        [Header("Identity")]
        public string encounterName;

        [Header("Teams")]
        public HeroData[] allies;
        public HeroData[] enemies;

        [Header("Level")]
        [Min(1)] public int allyLevel  = 1;
        [Min(1)] public int enemyLevel = 1;
    }
}
