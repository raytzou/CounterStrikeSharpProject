using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using MyProject.Classes;
using MyProject.Modules.Interfaces;
using MyProject.Services.Interfaces;

namespace MyProject.Modules
{
    public class Music : IMusic
    {
        private readonly IPlayerService _playerService;
        private static readonly Random _random = new Random(); // avoid repeated instantiation
        private int? _currentPlayingIndex;

        public Music(IPlayerService playerService)
        {
            _playerService = playerService;
        }

        public string? CurrentRoundMusicName => _currentPlayingIndex is null ? null : Utility.SoundEvent.Round[_currentPlayingIndex.Value].DisplayName;

        public void PlayEndGameMusic()
        {
            var humans = Utility.GetHumanPlayers();

            foreach (var player in humans)
            {
                PlaySound(player, Utility.SoundEvent.EndGame);
            }
        }

        public void PlayRoundEndMusic()
        {
            throw new NotImplementedException();
        }

        public void PlayRoundMusic()
        {
            var humans = Utility.GetHumanPlayers();
            var selectedIndex = _random.Next(Utility.SoundEvent.Round.Count);
            var soundEvents = Utility.SoundEvent.Round.Select(soundEvent => soundEvent.EventName).ToList();

            _currentPlayingIndex = selectedIndex;

            foreach (var player in humans)
            {
                PlaySound(player, soundEvents, selectedIndex);
            }
        }

        public void PlayWarmupMusic(CCSPlayerController player)
        {
            PlaySound(player, Utility.SoundEvent.Warmup);
        }

        private void PlaySound(CCSPlayerController player, List<string> sounds)
        {
            var selectedSound = sounds[_random.Next(sounds.Count)];
            EmitSoundToPlayer(player, selectedSound);
        }

        private void PlaySound(CCSPlayerController player, List<string> sounds, int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= sounds.Count)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));

            var selectedSound = sounds[selectedIndex];
            EmitSoundToPlayer(player, selectedSound);
        }

        private void EmitSoundToPlayer(CCSPlayerController player, string soundName)
        {
            var recipient = new RecipientFilter { player };
            var playerVolume = _playerService.GetPlayerCache(player.SteamID)?.Volume ?? 50;

            player.EmitSound(soundName, recipient, playerVolume / 100);
        }
    }
}
