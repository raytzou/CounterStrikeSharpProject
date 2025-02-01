namespace MyProject.Classes
{
    public class BotProfile
    {
        public enum Difficulty
        {
            easy,
            normal,
            hard,
            expert,
        }

        public static string[] Grade => ["*", "**", "***", "X", "XX", "XXX", "OTAKU", "TIKU", "ZAKO"];

        public static string[] NameGroup => ["Rookie", "Casual", "Regular", "Veteran", "Skilled", "Pro", "GemaHaijin", "Dorei", "Helper"];
    }
}
