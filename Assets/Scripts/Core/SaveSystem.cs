using UnityEngine;

namespace Core
{
    // Simple PlayerPrefs + JsonUtility save. Enough for a single local profile;
    // swap for a file/cloud backend later without touching callers.
    public static class SaveSystem
    {
        private const string Key = "player_profile_v1";
        private static PlayerProfile _cached;

        public static PlayerProfile Profile => _cached ??= Load();

        public static PlayerProfile Load()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<PlayerProfile>(PlayerPrefs.GetString(Key));
                    if (loaded != null)
                    {
                        loaded.ownedHeroIds ??= new System.Collections.Generic.List<string>();
                        return loaded;
                    }
                }
                catch { /* corrupt save — fall through to a fresh profile */ }
            }
            return new PlayerProfile();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Profile));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            _cached = new PlayerProfile();
            Save();
        }
    }
}
