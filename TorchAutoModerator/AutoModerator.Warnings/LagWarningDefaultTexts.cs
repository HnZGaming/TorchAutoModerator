using System.Collections.Generic;

namespace AutoModerator.Warnings
{
    public static class LagWarningDefaultTexts
    {
        public const string Title = "Auto Moderator: L.A.G.S. Warning";

        public static string MustProfileSelf => ToString(new[]
        {
            "Hello engineer, Auto Moderator here! We have detected an excessive density of L.A.G.S. particles around your assets!",
            "L.A.G.S. is a highly electric substance that could damage your construction and lure in space pirates from afar!",
            "Scan yourself with this chat command right now:\n!lags scan",
        });

        public static string MustDelagSelf => ToString(new[]
        {
            "You've successfully scanned your constructions!",
            "Now, lower your L.A.G.S. level as much as possible!",
            "You may disable or reduce the number of suspicious blocks to start with.",
        });

        public static string MustWaitUnpinned => ToString(new[]
        {
            "Oh no! I'm afraid but your location is known by everyone by now...",
            "We can't help you at this point!",
            "Defend yourself until your L.A.G.S. particles are eliminated!",
        });

        public static string Ended => ToString(new[]
        {
            "Congratulations! Your L.A.G.S. level is now sufficiently low.",
            "Space pirates can't find you anymore!",
            "I'll see myself out -- safe travel, fellow engineer!"
        });

        static string ToString(IEnumerable<string> lines) => string.Join(" ", lines);
    }
}