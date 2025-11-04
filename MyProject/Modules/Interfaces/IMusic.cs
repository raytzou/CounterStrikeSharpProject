using CounterStrikeSharp.API.Core;

namespace MyProject.Modules.Interfaces
{
    public interface IMusic
    {
        /// <summary>
        /// Plays warmup music for a player
        /// </summary>
        /// <param name="player">Player Controller</param>
        void PlayWarmupMusic(CCSPlayerController player);

        /// <summary>
        /// Plays synchronized round music to all human players
        /// </summary>
        void PlayRoundMusic();

        void PlayRoundEndMusic();

        void PlayEndGameMusic();
    }
}
