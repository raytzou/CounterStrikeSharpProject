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
        public uint? GetPlayingRoundSoundID(int playerSlot) => _playingRoundSounds.TryGetValue(playerSlot, out uint id) ? id : null;

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
                var volume = _playerService.GetPlayerCache(player.SteamID)?.Volume ?? 50;
                SendSoundEventPackage(player, volume, _playingRoundSounds[player.Slot]);
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

        public void PlaySaySound(string soundEventName, float pitch)
        {
            var humans = Utility.GetHumanPlayers();

            foreach (var human in humans)
            {
                var soundEventId = EmitSound(human, soundEventName);
                var volume = _playerService.GetPlayerCache(human.SteamID)?.SaySoundVolume ?? 50;

                SendSoundEventPackage(human, volume, soundEventId, pitch);
            }
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
            var soundEventId = EmitSound(player, selectedSound);
            var volume = _playerService.GetPlayerCache(player.SteamID)?.Volume ?? 50;

            SendSoundEventPackage(player, volume, soundEventId);
        }

        private uint EmitSound(CCSPlayerController player, string soundName)
        {
            var recipient = new RecipientFilter { player };
            var soundEventId = player.EmitSound(soundName, recipient);

            return soundEventId;
        }

        private static void SendSoundEventPackage(CCSPlayerController player, byte volume, uint soundEventId, float pitch = 1f)
        {
            Server.NextWorldUpdate(() =>
            {
                if (!Utility.IsHumanPlayerValid(player))
                    return;

                Utility.SendSoundEventPackage(player, soundEventId, volume / 100f, pitch);
            });
        }
    }
}
