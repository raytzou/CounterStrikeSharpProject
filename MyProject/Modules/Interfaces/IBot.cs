using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace MyProject.Modules.Interfaces
{
    public interface IBot
    {
        int CurrentLevel { get; }

        int MaxRespawnTimes { get; }

        int RespawnTimes { get; }

        IReadOnlySet<string> SpecialAndBoss { get; }

        IReadOnlySet<string> SpecialBots { get; }

        IReadOnlySet<string> Bosses { get; }

        Task MapStartBehavior(string mapName);

        Task WarmupEndBehavior(string mapName);

        Task RoundStartBehavior();

        Task RoundEndBehavior(int winStreak, int looseStreak, string mapName);

        Task RespawnBotAsync(CCSPlayerController bot, string mapName);

        bool IsBoss(CCSPlayerController player);

        bool IsSpecial(CCSPlayerController player);

        void BossBehavior(CCSPlayerController boss);

        void ClearDamageTimer();

        Task GiveBotWeapon(CCSPlayerController bot, CsItem weapon);
    }
}
