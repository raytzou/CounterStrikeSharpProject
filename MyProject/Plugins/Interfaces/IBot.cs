using CounterStrikeSharp.API.Core;

namespace MyProject.Plugins.Interfaces
{
    public interface IBot
    {
        int CurrentLevel { get; }

        int MaxRespawnTimes { get; }

        int RespawnTimes { get; }

        void MapStartBehavior();

        void WarmupEndBehavior(int botQuota);

        void RoundStartBehavior(int roundCount);

        void RoundEndBehavior(int botQuota, int roundCount, int winStreak, int looseStreak);

        Task RespawnBotAsync(CCSPlayerController bot, int currentRound);
    }
}
