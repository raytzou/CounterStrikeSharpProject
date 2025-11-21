using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.UserMessages;
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
        private readonly Dictionary<int, uint> _playingRoundSounds = []; // player.Slot -> Sound Event Index

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

        public void PlayRoundWinMusic()
        {
            var humans = Utility.GetHumanPlayers();

            foreach (var player in humans)
            {
                PlaySound(player, Utility.SoundEvent.Win);
            }
        }

        public void PlayRoundLoseMusic()
        {
            var humans = Utility.GetHumanPlayers();

            foreach (var player in humans)
            {
                PlaySound(player, Utility.SoundEvent.Loose);
            }
        }

        public void PlayRoundMusic()
        {
            var humans = Utility.GetHumanPlayers();
            var selectedIndex = _random.Next(Utility.SoundEvent.Round.Count);
            var soundEvents = Utility.SoundEvent.Round.Select(soundEvent => soundEvent.EventName).ToList();

            _playingRoundSounds.Clear();
            _currentPlayingIndex = selectedIndex;

            foreach (var player in humans)
            {
                EmitRoundSoundToPlayer(player, soundEvents[selectedIndex]);
            }
        }

        public void StopRoundMusic()
        {
            foreach (var playerRoundSoundEvent in _playingRoundSounds)
            {
                var message = UserMessage.FromId(209);
                message.Recipients.Add(playerRoundSoundEvent.Key);
                message.SetUInt("soundevent_guid", playerRoundSoundEvent.Value);
                message.Send();
            }

            _playingRoundSounds.Clear();
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

        private void EmitSoundToPlayer(CCSPlayerController player, string soundName)
        {
            var recipient = new RecipientFilter { player };
            var playerVolume = _playerService.GetPlayerCache(player.SteamID)?.Volume ?? 50;

            player.EmitSound(soundName, recipient, playerVolume / 100);
        }

        private void EmitRoundSoundToPlayer(CCSPlayerController player, string soundName)
        {
            var recipient = new RecipientFilter { player };
            var playerVolume = _playerService.GetPlayerCache(player.SteamID)?.Volume ?? 50;

            var soundIndex = player.EmitSound(soundName, recipient, playerVolume / 100f);
            _playingRoundSounds[player.Slot] = soundIndex;
        }
    }
}
