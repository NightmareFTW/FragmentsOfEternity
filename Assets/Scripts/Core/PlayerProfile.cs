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
    }
}
