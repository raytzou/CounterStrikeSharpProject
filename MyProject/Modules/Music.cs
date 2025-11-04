using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Modules.Interfaces;

namespace MyProject.Modules
{
    public class Music : IMusic
    {
        private readonly ILogger _logger;
        private static readonly Random _random = new Random(); // avoid repeated instantiation
        private static readonly string[] _warmup = new string[]
        {
            "warmup.01",
            "warmup.02",
            "warmup.03",
            "warmup.04",
            "warmup.05"
        };

        public Music(ILogger<Music> logger)
        {
            _logger = logger;
        }

        public void PlayEndGameMusic()
        {
            throw new NotImplementedException();
        }

        public void PlayRoundEndMusic()
        {
            throw new NotImplementedException();
        }

        public void PlayRoundMusic()
        {
            throw new NotImplementedException();
        }

        public void PlayWarmupMusic(CCSPlayerController player)
        {
            PlaySound(player, _warmup);
        }

        private void PlaySound(CCSPlayerController player, string[] sounds)
        {
            var selectedSound = sounds[_random.Next(sounds.Length)];
            EmitSoundToPlayer(player, selectedSound);
        }

        private void PlaySound(CCSPlayerController player, string[] sounds, int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= sounds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));

            var selectedSound = sounds[selectedIndex];
            EmitSoundToPlayer(player, selectedSound);
        }

        private void EmitSoundToPlayer(CCSPlayerController player, string soundName)
        {
            var recipient = new RecipientFilter { player };
            player.EmitSound(soundName, recipient);
        }
    }
}
