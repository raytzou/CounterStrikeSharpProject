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

        public static Dictionary<string, Difficulty> NameGroup => new()
        {
            { "Rookie", Difficulty.easy },
            { "Casual", Difficulty.normal },
            { "Regular", Difficulty.normal },
            { "Veteran", Difficulty.hard },
            { "Skilled", Difficulty.hard },
            { "Pro", Difficulty.hard },
            { "GemaHaijin", Difficulty.expert },
            { "Dorei", Difficulty.expert },
            { "Helper", Difficulty.normal }
        };

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
