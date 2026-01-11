using CounterStrikeSharp.API.Core;

namespace MyProject.Classes
{
    public class MainConfig : BasePluginConfig
    {
        public float ChangeMapTimeBuffer = 2f;
        public int SpawnPointCount = 20;
        public int CostScoreToRevive = 50;
        public float WeaponCheckTime = 3f;
        public int BotQuota = 20;
        public int MidBossRound = 4;
        public int FinalBossRound = 8;
        public string EagleEyeModel = "[???]Nano Girl";
        public string RushModel = "[Resident Evil 2]Hunk";
        public double BossActiveAbilityChance = 20;
        public int MidBossHealth = 10000;
        public int FinalBossHealth = 15000;
        public int DisplayMenuInterval = 30;
        public int MaxRounds = 9;
    }
}
