namespace Core
{
    // Runtime-only handoff of which stage the player picked on Home, read by the
    // Combat scene after a scene load. Not persisted — resets each session.
    public static class CampaignState
    {
        public static int SelectedStage;
    }
}
