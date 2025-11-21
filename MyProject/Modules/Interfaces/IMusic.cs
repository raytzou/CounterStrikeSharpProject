using CounterStrikeSharp.API.Core;

namespace MyProject.Modules.Interfaces
{
    public interface IMusic
    {
        /// <summary>
        /// The current round music name, or null if no round music has been played yet
        /// </summary>
        string? CurrentRoundMusicName { get; }

        /// <summary>
        /// Plays warmup music for a player
        /// </summary>
        /// <param name="player">Player Controller</param>
        void PlayWarmupMusic(CCSPlayerController player);

        /// <summary>
        /// Plays synchronized round music to all human players
        /// </summary>
        void PlayRoundMusic();

        /// <summary>
        /// Stops playing round music
        /// </summary>
        void StopRoundMusic();

        /// <summary>
        /// Plays win music when human team win
        /// </summary>
        void PlayRoundWinMusic();

        /// <summary>
        /// Plays lose music when human team loose
        /// </summary>
        void PlayRoundLoseMusic();

        /// <summary>
        /// Plays end game music for all players
        /// </summary>
        void PlayEndGameMusic();
    }
}
