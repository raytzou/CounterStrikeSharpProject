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
        private static readonly List<(string SoundEvent, string DisplayName)> _round = new()
        {
            ("round.01", "龍が如く0 OST - Fiercest Warrior ver 0"),
            ("round.02", "龍が如く6 命の詩 OST - Fiercest Warrior ver.6"),
            ("round.03", "Metal Slug 2 - Assault Theme"),
            ("round.04", "Cyberpunk 2077 OST - The Rebel Path"),
            ("round.05", "龍が如く0 OST - Pledge of Demon 怨魔の契り"),
            ("round.06", "龍が如く0 OST - 閻魔の誓い"),
            ("round.07", "龍が如く0 OST - Receive You ~Tech Trance Arrange~"), // skip 8 cuz the file is broken and removed
            ("round.09", "Devil May Cry 3 - Divine Hate"),
            ("round.10", "Devil May Cry 4 - Shall Never Surrender"),
            ("round.11", "Devil May Cry 5 - Bury the Light"),
            ("round.12", "Devil May Cry 5 - Devil Trigger"),
        };
        private static readonly string[] _endGame = new string[]
        {
            "end.01"
        };

        private int? _currentPlayingIndex;

        public Music(ILogger<Music> logger)
        {
            _logger = logger;
        }

        public void PlayEndGameMusic()
        {
            var humans = Utility.GetHumanPlayers();

            foreach (var player in humans)
            {
                PlaySound(player, _endGame);
            }
        }

        public void PlayRoundEndMusic()
        {
            throw new NotImplementedException();
        }

        public void PlayRoundMusic()
        {
            var humans = Utility.GetHumanPlayers();
            var selectedIndex = _random.Next(_round.Count);
            var soundEvents = _round.Select(r => r.SoundEvent).ToArray();

            _currentPlayingIndex = selectedIndex;

            foreach (var player in humans)
            {
                PlaySound(player, soundEvents, selectedIndex);
            }
        }

        public void PlayWarmupMusic(CCSPlayerController player)
        {
            PlaySound(player, _warmup);
        }

        public string? GetCurrentRoundMusicName()
        {
            if (_currentPlayingIndex is null) return null;
            return _round[_currentPlayingIndex.Value].DisplayName;
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
