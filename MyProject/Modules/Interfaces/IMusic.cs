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

        /// <summary>
        /// Plays end game music for all players
        /// </summary>
        void PlayEndGameMusic();

        /// <summary>
        /// Gets the display name of the current round music track
        /// </summary>
        /// <returns>The current round music name, or null if no round music has been played yet</returns>
        string? GetCurrentRoundMusicName();
    }
}
