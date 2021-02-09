using System.Collections.Generic;

namespace AutoModerator.Warnings
{
    public static class WarningDefaultTexts
    {
        public const string Title = "Auto Moderator: L.A.G.S. Warning";
        public const string NotificationFormat = "Your current L.A.G.S. level: {level}%";

        public static string MustProfileSelf => ToString(new[]
        {
            "Hello engineer, Auto Moderator here! We have detected an excessive level of L.A.G.S. particles from your assets!",
            "L.A.G.S. can travel several light years and draw space pirates to your location!",
            "Scan yourself with this chat command right now:\n!lags scan",
        });

        public static string MustDelagSelf => ToString(new[]
        {
            "You've successfully scanned yourself!",
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