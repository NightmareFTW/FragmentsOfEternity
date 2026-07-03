using System;
using System.Collections.Generic;

namespace Core
{
    // Serializable player save data: soft currency plus the list of owned hero
    // ids (duplicates allowed — future dupe/shard systems can build on this).
    [Serializable]
    public class PlayerProfile
    {
        public int          gems         = 3000;
        public List<string> ownedHeroIds = new List<string>();

        // The (up to 4) hero ids the player has chosen to bring into battle.
        public List<string> teamHeroIds  = new List<string>();

        // Pulls since the last 5★ — drives the pity guarantee.
        public int pityCounter;

        // Number of campaign stages cleared; stage i is unlocked when this >= i.
        public int campaignProgress;
    }
}
