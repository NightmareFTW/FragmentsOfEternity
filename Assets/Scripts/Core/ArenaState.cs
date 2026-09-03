namespace Core
{
    // Runtime-only handoff from Home to the Combat scene when entering an
    // Arena battle instead of a campaign stage. Not persisted — like
    // CampaignState, only meaningful for the current session. Both of
    // Combat's entry points (HomeController.LoadStage for a normal stage,
    // HomeController.OnArena for this) set Active explicitly, so there's no
    // stale-state risk between the two modes.
    public static class ArenaState
    {
        public static bool   Active;
        public static string RivalName = "Rival";
        public static int    RivalReward;
        public static (string heroId, int level)[] RivalRoster;
    }
}
