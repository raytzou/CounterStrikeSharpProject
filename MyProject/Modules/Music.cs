using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
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
        private static readonly string[] _round = new string[]
        {
            "round.01",
            "round.02",
            "round.03",
            "round.04",
            "round.05",
            "round.06",
            "round.07",
            "round.09",
            "round.10",
            "round.11",
            "round.12"
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
            var humans = Utility.GetHumanPlayers();
            var selectedIndex = _random.Next(_round.Length);

            foreach (var player in humans)
            {
                PlaySound(player, _round, selectedIndex);
            }
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
