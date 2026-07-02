using UnityEngine;

namespace Data
{
    // The set of heroes a player can summon, plus the cost per pull.
    [CreateAssetMenu(fileName = "GachaPool", menuName = "RPG/Gacha Pool")]
    public class GachaPool : ScriptableObject
    {
        public HeroData[] heroes;
        [Min(0)] public int summonCost = 300;
    }
}
