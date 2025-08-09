using CounterStrikeSharp.API.Core;

namespace MyProject.Plugins.Interfaces
{
    public interface IBot
    {
        int CurrentLevel { get; }

        int MaxRespawnTimes { get; }

        int RespawnTimes { get; }

        void MapStartBehavior();

        void WarmupEndBehavior();

        void RoundStartBehavior(int roundCount);

        Task RoundEndBehavior(int roundCount, int winStreak, int looseStreak);

        Task RespawnBotAsync(CCSPlayerController bot, int currentRound);
    }
}
