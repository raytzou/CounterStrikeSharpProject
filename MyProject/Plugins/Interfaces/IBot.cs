using CounterStrikeSharp.API.Core;

namespace MyProject.Plugins.Interfaces
{
    public interface IBot
    {
        int CurrentLevel { get; }

        int MaxRespawnTimes { get; }

        int RespawnTimes { get; }

        void MapStartBehavior();

        Task WarmupEndBehavior();

        Task RoundStartBehavior();

        Task RoundEndBehavior(int winStreak, int looseStreak);

        Task RespawnBotAsync(CCSPlayerController bot);
    }
}
