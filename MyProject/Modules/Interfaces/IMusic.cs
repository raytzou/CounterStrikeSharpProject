using CounterStrikeSharp.API.Core;

namespace MyProject.Modules.Interfaces
{
    public interface IMusic
    {
        /// <summary>
        /// Plays warmup music for a player who joins the server during the warmup period
        /// </summary>
        /// <param name="player">Player Controller</param>
        void PlayWarmupMusic(CCSPlayerController player);

        void PlayRoundMusic();

        void PlayRoundEndMusic();

        void PlayEndGameMusic();
    }
}
