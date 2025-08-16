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

        Task RoundStartBehavior();

        void RoundEndBehavior(int winStreak, int looseStreak);

        Task RespawnBotAsync(CCSPlayerController bot);
    }
}
