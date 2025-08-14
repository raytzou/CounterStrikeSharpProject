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
    }
}
