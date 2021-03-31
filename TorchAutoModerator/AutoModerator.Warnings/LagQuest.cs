namespace AutoModerator.Warnings
{
    public enum LagQuest
    {
        None,
        MustProfileSelf,
        MustDelagSelf,
        MustWaitUnpinned,
        Ended, // show players that the quest is done
        Cleared, // remove the hud
    }
}