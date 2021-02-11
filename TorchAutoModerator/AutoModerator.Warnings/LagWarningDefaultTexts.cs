using System.Collections.Generic;

namespace AutoModerator.Warnings
{
    public static class LagWarningDefaultTexts
    {
        public const string Title = "Auto Moderator: L.A.G. Warning";
        public const string CurrentLevel = "Your current L.A.G. level";

        public static string MustProfileSelf => ToString(new[]
        {
            "Hello engineer, be advised! We have detected a critical level of L.A.G. particles around your assets!",
            "L.A.G. is highly corrosive substance that could do all sorts of harm if you keep them around!",
            "Scan yourself with this chat command right now:\n!lag scan",
        });

        public static string MustDelagSelf => ToString(new[]
        {
            "You've successfully scanned your L.A.G. level!",
            "To lower your L.A.G. level, you may disable or reduce the number of suspicious blocks.",
            "To scan your current L.A.G. level, type in \"!lag scan\" anytime you want!",
        });

        public static string MustWaitUnpinned => ToString(new[]
        {
            "OH NO! I'm afraid but L.A.G. is taking over your assets... We can't help you at this point!",
            "You can still try to lower your L.A.G. level so you can recover faster!",
        });

        public static string Ended => ToString(new[]
        {
            "Congratulations! Your L.A.G. level is now sufficiently low. You'll be safe!",
            "I'll see myself out -- safe travel, fellow engineer!"
        });

        static string ToString(IEnumerable<string> lines) => string.Join(" ", lines);
    }
}