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

        public static int MaxLevel => 8;

        public static string[] Grade => ["*", "**", "***", "X", "XX", "XXX", "OTAKU", "TIKU", "ZAKO"];

        public static string[] NameGroup => ["Rookie", "Casual", "Regular", "Veteran", "Skilled", "Pro", "GemaHaijin", "Dorei", "Helper"];

        public static Dictionary<int, string> Special => new()
        {
            { 0, "[ELITE]EagleEye" },
            { 1, "[ELITE]mimic" },
            { 2, "[EXPERT]Rush" },
        };

        public static Dictionary<int, string> Boss => new()
        {
            { 0, "[BOSS]FirstBoss" },
            { 1, "[BOSS]FinalBoss" }
        };
    }
}
