using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Modules.Interfaces;
using MyProject.Services.Interfaces;

namespace MyProject.Modules
{
    public class Music : IMusic
    {
        private readonly IPlayerService _playerService;
        private readonly ILogger<Music> _logger;
        private static readonly Random _random = new Random(); // avoid repeated instantiation
        private int? _currentPlayingIndex;
        private readonly Dictionary<int, uint> _playingRoundSounds = []; // player.Slot -> Sound Event ID

        public Music(IPlayerService playerService, ILogger<Music> logger)
        {
            _playerService = playerService;
            _logger = logger;
        }

        public string? CurrentRoundMusicName => _currentPlayingIndex is null ? null : Utility.SoundEvent.Round[_currentPlayingIndex.Value].DisplayName;

        public void PlayEndGameMusic()
        {
            PlayMusicToAllHumans(Utility.SoundEvent.EndGame);
        }

        public void PlayRoundWinMusic()
        {
            PlayMusicToAllHumans(Utility.SoundEvent.Win);
        }

        public void PlayRoundLoseMusic()
        {
            PlayMusicToAllHumans(Utility.SoundEvent.Loose);
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
                _playingRoundSounds[player.Slot] = EmitSound(player, soundEvents[selectedIndex]);
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

        private void PlayMusicToAllHumans(List<string> sounds)
        {
            var humans = Utility.GetHumanPlayers();

            foreach (var player in humans)
            {
                PlaySound(player, sounds);
            }
        }

        private void PlaySound(CCSPlayerController player, List<string> sounds)
        {
            var selectedSound = sounds[_random.Next(sounds.Count)];

            EmitSound(player, selectedSound);
        }

        private uint EmitSound(CCSPlayerController player, string soundName)
        {
            var recipient = new RecipientFilter { player };
            var playerVolume = _playerService.GetPlayerCache(player.SteamID)?.Volume ?? 50;
            var soundEventId = player.EmitSound(soundName, recipient, playerVolume / 100f);

            Server.NextWorldUpdate(() =>
            {
                if (!Utility.IsHumanPlayerValid(player))
                    return;

                Utility.SendSoundEventPackage(player, soundEventId, playerVolume / 100f);
            });

            return soundEventId;
        }


    }
}
