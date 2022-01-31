using System.Collections.Generic;

namespace AutoModerator.Quests
{
    public static class QuestDefaultTexts
    {
        public const string Title = "Auto Moderator: WARNING";
        public const string CurrentLevel = "Your current lag";

        public static string MustProfileSelf => ToString(new[]
        {
            "Hello engineer, this is Auto Moderator bot.",
            "One or some of your grids are about to exceed the max allowed server resource consumption.",
            "You may be causing the server lag.",
            "If your lag level reaches 100%, one of your laggiest grids will be punished.",
            "Profile yourself with this chat command:\n!lag profile",
        });

        public static string MustDelagSelf => ToString(new[]
        {
            "To reduce the server lag, please disable or reduce the number of suspicious blocks.",
            "This warning message will go away as soon as you reach below the threshold."
        });

        public static string MustWaitUnpinned => ToString(new[]
        {
            "Your lag level reached 100%. One of your grids will be punished.",
            "You can stop the punishment by lowering your lag level below 100%."
        });

        public static string Ended => ToString(new[]
        {
            "Your lag level reached below the alerting point.",
            "This warning message will show up again if your lag level grew back up.",
        });

        static string ToString(IEnumerable<string> lines) => string.Join(" ", lines);
    }
}