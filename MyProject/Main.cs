using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Domains;
using MyProject.Models;
using MyProject.Modules.Interfaces;
using MyProject.Services.Interfaces;

namespace MyProject;

public class Main(
    ILogger<Main> logger,
    IPlayerService playerService,
    ICommand commmand,
    IBot bot,
    IMusic music
    ) : BasePlugin, IPluginConfig<MainConfig>
{
    #region plugin info
    public override string ModuleAuthor => "cynicat";
    public override string ModuleName => "MyProject";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My main plugin";
    #endregion plugin info

    private readonly ILogger<Main> _logger = logger;
    private readonly IPlayerService _playerService = playerService;

    // fields
    private readonly Dictionary<string, int> _players = []; // playerName -> slot
    private readonly Dictionary<string, Position> _position = [];
    private readonly Dictionary<string, WeaponStatus> _weaponStatus = [];
    private CounterStrikeSharp.API.Modules.Timers.Timer? _weaponCheckTimer = null;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _roundTimer = null;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _bombTimer = null;
    private int _roundCount = 0;
    private bool _warmup = true;
    private int _winStreak = 0;
    private int _looseStreak = 0;
    private bool _isRoundEnd = false;
    private bool _randomSpawn = false;
    private int _currentRoundSecond = 0;
    private int _endGameRound = 0;

    // module services
    private readonly ICommand _command = commmand;
    private readonly IBot _bot = bot;
    private readonly IMusic _music = music;

    // singleton members
    public static Main Instance { get; private set; } = null!; // To Do: remove singleton one day
    public required MainConfig Config { get; set; }
    public int RoundCount => _roundCount;
    public int PlayerCount => Utilities.GetPlayers().Count(p => !p.IsBot);
    public bool IsRoundEnd => _isRoundEnd;
    public int RoundSecond => _currentRoundSecond;

    public int GetPlayerSlot(string playerName)
    {
        if (_players.TryGetValue(playerName, out int slot))
            return slot;

        var player = Utilities.GetPlayers().FirstOrDefault(p => p.PlayerName == playerName);
        if (player != null && player.IsValid)
        {
            _players[playerName] = player.Slot;
            if (AppSettings.IsDebug)
                _logger.LogInformation("Late player registration: {playerName} -> slot {slot}", playerName, player.Slot);
            return player.Slot;
        }

        throw new Exception($"Player not found: {playerName}");
    }

    public override void Load(bool hotreload)
    {
        Instance = this;
        if (AppSettings.IsDebug)
            _logger.LogWarning("Debug mode is on");
        _logger.LogInformation("Server host time: {DT}", DateTime.Now);
        InitialSaySoundsSync();
        Reigsters();
        _command.RegisterCommands();
    }

    public void OnConfigParsed(MainConfig config)
    {
        Config = config;
    }

    #region Listeners
    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        var pathSet = new HashSet<string>();
        var armSet = new HashSet<string>();
        foreach (var skin in Utility.WorkshopSkins)
        {
            pathSet.Add(skin.Value.ModelPath);
            if (skin.Value.ArmPath is not null)
                armSet.Add(skin.Value.ArmPath);
        }
        foreach (var path in pathSet)
            manifest.AddResource(path);
        foreach (var path in armSet)
            manifest.AddResource(path);

        manifest.AddResource(SaySoundHelper.SaySoundHelper.SoundEventFile);
    }

    private void OnTick()
    {
        if (_roundCount == Config.MidBossRound || _roundCount == Config.FinalBossRound)
            DisplayEveryoneOnRadar();
    }

    private void OnMapStart(string mapName)
    {
        _logger.LogInformation("Map Start: {mapName}", mapName);

        Initialize(mapName);
        _playerService.ClearPlayerCache();

        _ = Task.Run(async () =>
        {
            try
            {
                await _bot.MapStartBehavior(mapName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Map Start Behavior error: {error}", ex);
            }
        });
    }

    private void OnMapEnd()
    {
        _playerService.SaveAllCachesToDB();
    }

    private void OnEntityCreated(CEntityInstance entity)
    {
        FixWeaponAmmoAmount();

        void FixWeaponAmmoAmount()
        {
            Server.NextFrame(() =>
            {
                if (!Utility.IsEntityValid(entity))
                {
                    if (AppSettings.IsDebug)
                        _logger.LogWarning("Entity invalidated before weapon ammo fix");
                    return;
                }

                if (string.IsNullOrEmpty(entity.Entity!.DesignerName))
                {
                    if (AppSettings.IsDebug)
                        _logger.LogWarning("Entity DesignerName is null or empty before weapon ammo fix");
                    return;
                }

                if (!entity.Entity.DesignerName.StartsWith("weapon_"))
                    return;

                CCSWeaponBase? weaponBase = null;

                try
                {
                    weaponBase = new CCSWeaponBase(entity.Handle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Entity handle invalidated during weapon ammo fix: {error}", ex.Message);
                    return;
                }

                if (weaponBase is null || !weaponBase.IsValid || weaponBase.Entity is null || weaponBase.VData is null)
                {
                    _logger.LogWarning("Weapon base invalid after construction during weapon ammo fix");
                    return;
                }

                try
                {
                    var weaponDesignName = weaponBase.Entity.DesignerName;
                    var weaponIndex = weaponBase.AttributeManager.Item.ItemDefinitionIndex;

                    if (AppSettings.IsDebug)
                    {
                        _logger.LogInformation("weapon created: {entityName}", weaponDesignName);
                        _logger.LogInformation("weapon index: {index}", weaponIndex);
                    }

                    switch (weaponDesignName)
                    {
                        case "weapon_awp":
                            Utility.SetAmmoAmount(weaponBase, 10);
                            Utility.SetReservedAmmoAmount(weaponBase, 30);
                            break;
                        case "weapon_m4a1":
                            if (weaponIndex == 60) // m4a1 silencer
                            {
                                Utility.SetAmmoAmount(weaponBase, 30);
                                Utility.SetReservedAmmoAmount(weaponBase, 90);
                            }
                            break;
                        case "weapon_hkp2000":
                            if (weaponIndex == 61) // USP-S
                                Utility.SetReservedAmmoAmount(weaponBase, 100);
                            break;
                        case "weapon_p250":
                            Utility.SetReservedAmmoAmount(weaponBase, 52);
                            break;
                        case "weapon_cz75a":
                            Utility.SetReservedAmmoAmount(weaponBase, 60);
                            break;
                        case "weapon_deagle":
                            if (weaponIndex == 64) // R8 Revolver
                                Utility.SetReservedAmmoAmount(weaponBase, 40);
                            break;
                        default:
                            return;
                    }
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Entity is not valid") &&
                        !ex.Message.Contains("Invoked on a non-main thread"))
                    {
                        _logger.LogError(ex, "Unexpected error fixing weapon ammo");
                    }
                }
            });
        }
    }
    #endregion Listeners

    #region hook result
    private HookResult PlayerFullConnectHandler(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (!Utility.IsHumanValid(player))
            return HookResult.Continue;

        if (!player!.IsBot)
        {
            _logger.LogInformation("{client} has connected at {DT}, IP: {ipAddress}, SteamID: {steamID}", player.PlayerName, DateTime.Now, player.IpAddress, player.SteamID);

            if (!_position.ContainsKey(player.PlayerName))
                _position.Add(player.PlayerName, new Position());
            if (!_weaponStatus.ContainsKey(player.PlayerName))
                _weaponStatus.Add(player.PlayerName, new WeaponStatus());

            Server.PrintToChatAll($" {ChatColors.Purple}{player.PlayerName} {ChatColors.Green}has connected to the server.");
        }

        _players[player.PlayerName] = player.Slot;

        return HookResult.Continue;
    }

    private HookResult PlayerJoinTeamHandler(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!Utility.IsHumanValid(player))
            return HookResult.Continue;

        if (_playerService.GetPlayerCache(player.SteamID) is null)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    await _playerService.PrepareCache(player);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prepare player cache error: {playerName}", player.PlayerName);
            }
        }

        if (_roundCount == 0)
        {
            AddTimer(1f, () =>
            {
                if (!Utility.IsHumanValid(player))
                    return;
                _music.PlayWarmupMusic(player);
            });
        }
        return HookResult.Continue;
    }

    private HookResult PlayerSpawnHandler(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (!Utility.IsPlayerValid(player))
            return HookResult.Continue;

        if (_warmup)
        {
            WelcomeMessage();
            if (player.PlayerPawn.Value!.TakesDamage)
                SetPlayerProtection(player);
        }
        else
            RemovePlayerProtection(player);

        if (!player.IsBot)
        {
            _weaponStatus[player.PlayerName].IsTracking = true;
            SetClientModel(player);
        }

        return HookResult.Continue;

        static void SetPlayerProtection(CCSPlayerController? player)
        {
            if (player is not null && player.PlayerPawn.Value is not null)
                player.PlayerPawn.Value.TakesDamage = false;
        }

        void WelcomeMessage()
        {
            AddTimer(1f, () =>
            {
                if (!Utility.IsHumanValid(player))
                    return;
                player.PrintToChat($" {ChatColors.Lime}Welcome to {ChatColors.Purple}{ConVar.Find("hostname")!.StringValue}{ChatColors.Lime}!");
                player.PrintToChat($" {ChatColors.Lime}Type {ChatColors.Orange}'!help' {ChatColors.Lime}for more information!");
                player.PrintToChat($" {ChatColors.Lime}If you have any problem, feel free to contact the admin!");
                player.PrintToChat($" {ChatColors.Lime}Hope you enjoy here ^^");
            });
        }
    }

    private HookResult PlayerDeathHandler(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null ||
            !player.IsValid)
            return HookResult.Continue;

        if (_warmup || _isRoundEnd) return HookResult.Continue;

        if (player.IsBot && player.Team != GetHumanTeam())
        {
            if (!_randomSpawn)
            {
                Server.ExecuteCommand("mp_randomspawn 1");
                _randomSpawn = true;
            }
            if (_bot.SpecialAndBoss.Contains(player.PlayerName)) return HookResult.Continue;
            var mapName = Server.MapName;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _bot.RespawnBotAsync(player, mapName);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Entity is not valid"))
                {
                    _logger.LogWarning("Failed to respawn bot {PlayerName}: Entity is not valid, probably the bot has been kicked after round end.", player.PlayerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error respawning bot {PlayerName}", player.PlayerName);
                }
            });
        }
        else if (_position.TryGetValue(player.PlayerName, out Position? playerPosition))
        {
            var origin = new Vector(player.PlayerPawn.Value!.AbsOrigin?.X ?? 0f, player.PlayerPawn.Value!.AbsOrigin?.Y ?? 0f, player.PlayerPawn.Value!.AbsOrigin?.Z ?? 0f);
            var rotation = new QAngle(player.PlayerPawn.Value!.AbsRotation?.X ?? 0f, player.PlayerPawn.Value!.AbsRotation?.Y ?? 0f, player.PlayerPawn.Value!.AbsRotation?.Z ?? 0f);
            var velocity = new Vector(player.PlayerPawn.Value!.AbsVelocity?.X ?? 0f, player.PlayerPawn.Value!.AbsVelocity?.Y ?? 0f, player.PlayerPawn.Value!.AbsVelocity?.Z ?? 0f);

            playerPosition.Origin = origin;
            playerPosition.Rotation = rotation;
            playerPosition.Velocity = velocity;
            _weaponStatus[player.PlayerName].IsTracking = false;
        }

        return HookResult.Continue;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        KillTimer();
        _bot.ClearDamageTimer();
        _currentRoundSecond = 0;
        _isRoundEnd = false;
        BotRoundStartBehavior();

        if (_warmup) return HookResult.Continue;

        var isBossRound = _roundCount == Config.MidBossRound || _roundCount == Config.FinalBossRound;

        HandleRoundStartMessages();
        RemoveProtectionFromAllPlayers();
        ActivateAllWeaponStatuses();
        StartWeaponCheckTimer();

        if (isBossRound)
        {
            RemoveBomb();
            RemoveHostage();
        }

        if (_roundCount == _endGameRound)
        {
            // End Game
            Server.ExecuteCommand("mp_maxrounds 1");
            AddTimer(1f, () =>
            {
                _music.PlayEndGameMusic();
            });
        }
        else
            PlayRoundMusic();

        return HookResult.Continue;

        void HandleRoundStartMessages()
        {
            if (_roundCount != _endGameRound)
            {
                Utility.PrintToChatAllWithColor($"Round: {ChatColors.LightRed}{_roundCount}{ChatColors.Grey}/{ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>() - 1}");

                if (!isBossRound)
                {
                    Utility.PrintToChatAllWithColor($"Difficulty level: {ChatColors.Purple}{_bot.CurrentLevel}{ChatColors.Grey}/{BotProfile.MaxLevel}");
                    Utility.PrintToChatAllWithColor($"Bot respawn: {ChatColors.Green}{_bot.MaxRespawnTimes}");
                }
                else
                    Server.PrintToChatAll($" {ChatColors.Red}***** Boss shows up!! *****");
            }
        }

        void RemoveProtectionFromAllPlayers()
        {
            foreach (var player in Utilities.GetPlayers())
            {
                RemovePlayerProtection(player);
            }
        }

        void ActivateAllWeaponStatuses()
        {
            foreach (var pair in _weaponStatus)
            {
                pair.Value.IsTracking = true;
            }
        }

        void StartWeaponCheckTimer()
        {
            _weaponCheckTimer = AddTimer(Config.WeaponCheckTime, () =>
            {
                if (AppSettings.LogWeaponTracking)
                    Server.PrintToChatAll("weapon check");

                foreach (var pair in _weaponStatus)
                {
                    if (!pair.Value.IsTracking)
                        continue;

                    if (AppSettings.LogWeaponTracking)
                        Server.PrintToChatAll($"tracking: {pair.Key}");

                    UpdateWeaponStatus(pair);
                }
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

            void UpdateWeaponStatus(KeyValuePair<string, WeaponStatus> pair)
            {
                pair.Value.Weapons.Clear();
                var playerWeaponService = Utilities.GetPlayers().First(player => player.PlayerName == pair.Key).PlayerPawn.Value!.WeaponServices;
                if (playerWeaponService is null) return;

                foreach (var weapon in playerWeaponService.MyWeapons)
                {
                    if (weapon.Value is null) continue;
                    pair.Value.Weapons.Add(weapon.Value.DesignerName);
                }

                if (AppSettings.LogWeaponTracking)
                {
                    foreach (var cacheWeapon in pair.Value.Weapons)
                        Server.PrintToChatAll(cacheWeapon);
                }
            }
        }

        void RemoveBomb()
        {
            var findBomb = false;
            foreach (var player in Utilities.GetPlayers())
            {
                if (!Utility.IsHumanValid(player)) continue;
                if (player.PlayerPawn.Value!.WeaponServices is null) continue;

                foreach (var weapon in player.PlayerPawn.Value.WeaponServices.MyWeapons)
                {
                    if (weapon.Value is null) continue;
                    if (weapon.Value.DesignerName == Utility.GetCsItemEnumValue(CsItem.C4))
                    {
                        findBomb = true;
                        break;
                    }
                }

                if (findBomb)
                {
                    player.RemoveItemByDesignerName(Utility.GetCsItemEnumValue(CsItem.C4));
                    break;
                }
            }
        }

        void RemoveHostage()
        {
            foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("hostage_entity"))
            {
                if (!Utility.IsEntityValid(entity)) continue;
                entity.Remove();
            }
        }

        void StartRoundTimer()
        {
            _roundTimer = AddTimer(1f, () =>
            {
                if (!_isRoundEnd)
                    _currentRoundSecond++;
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        }

        void BotRoundStartBehavior()
        {
            var mapName = Server.MapName;

            Server.NextWorldUpdate(() =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _bot.RoundStartBehavior(mapName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Bot Round Start Behavior error at round {round}, map {map}", _roundCount, mapName);
                    }
                });
            });
        }

        void PlayRoundMusic()
        {
            var freezeTime = ConVar.Find("mp_freezetime")!.GetPrimitiveValue<int>();

            AddTimer(freezeTime, () =>
            {
                StartRoundTimer();
                _music.PlayRoundMusic();
                Server.NextFrame(() =>
                {
                    var roundMusicName = _music.CurrentRoundMusicName;
                    if (!string.IsNullOrEmpty(roundMusicName))
                    {
                        Utility.PrintToChatAllWithColor($"Now is playing: {ChatColors.Lime}{roundMusicName}");
                    }
                });
            });
        }
    }

    private HookResult RoundEndHandler(EventRoundEnd eventRoundEnd, GameEventInfo gameEventInfo)
    {
        _isRoundEnd = true;
        _music.StopRoundMusic();
        KillTimer();
        _bot.ClearDamageTimer();

        if (eventRoundEnd.Winner == (int)GetHumanTeam())
        {
            _winStreak++;
            _looseStreak = 0;
            _music.PlayRoundWinMusic();
        }
        else
        {
            _looseStreak++;
            _winStreak = 0;
            _music.PlayRoundLoseMusic();
        }

        if (!_warmup)
        {
            var mapName = Server.MapName;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _bot.RoundEndBehavior(_winStreak, _looseStreak, mapName);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Round End Behavior error, round: {round} {ex}", _roundCount, ex);
                }
            });


            Server.ExecuteCommand("mp_randomspawn 0");
            _randomSpawn = false;
            _roundCount++;
        }

        return HookResult.Continue;
    }

    private HookResult WarmupHandler(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        _roundCount = 0;
        _warmup = true;
        return HookResult.Continue;
    }

    private HookResult WarmupEndHandler(EventWarmupEnd @event, GameEventInfo info)
    {
        _roundCount = 1;
        _warmup = false;
        var mapName = Server.MapName;

        _ = Task.Run(async () =>
        {
            try
            {
                await _bot.WarmupEndBehavior(mapName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Warmup End Behavior error: {error}", ex);
            }
        });

        return HookResult.Continue;
    }

    private HookResult DisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player is null || !player.IsValid)
            return HookResult.Continue;

        if (!player.IsBot)
        {
            _logger.LogInformation("{client} has disconnected at {DT}", player.PlayerName, DateTime.Now);
            var playerCache = _playerService.GetPlayerCache(player.SteamID);
            if (playerCache != null)
                _playerService.SaveCacheToDB(playerCache);
        }

        if (_position.ContainsKey(player.PlayerName))
            _position.Remove(player.PlayerName);
        if (_weaponStatus.ContainsKey(player.PlayerName))
            _weaponStatus.Remove(player.PlayerName);
        if (_players.ContainsKey(player.PlayerName))
            _players.Remove(player.PlayerName);

        return HookResult.Continue;
    }

    private HookResult PlayerHurtHandler(EventPlayerHurt @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (victim is null || !victim.IsValid || attacker is null || !attacker.IsValid)
            return HookResult.Continue;

        // Prevent boss from being damaged by their own abilities
        if (_bot.IsBoss(victim) && _bot.IsBoss(attacker) && victim.Index == attacker.Index)
            return HookResult.Handled;

        if (_bot.IsBoss(victim))
            _bot.BossBehavior(victim);

        return HookResult.Continue;
    }

    private HookResult BombPlantedHandler(EventBombPlanted @event, GameEventInfo info)
    {
        var c4Timer = ConVar.Find("mp_c4timer")!.GetPrimitiveValue<int>();

        _bombTimer = AddTimer(1f, () =>
        {
            Utility.PrintToAllCenter($"C4 Counter: {c4Timer--}");
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        return HookResult.Continue;
    }

    private HookResult BombExplodedHandler(EventBombExploded @event, GameEventInfo info)
    {
        BombEventHandler("C4 has exploded!");

        return HookResult.Continue;
    }

    private HookResult BombDefusedHandler(EventBombDefused @event, GameEventInfo info)
    {
        BombEventHandler("Bomb has been defuesed");

        return HookResult.Continue;
    }

    private HookResult OnPlayerSayCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (!Utility.IsHumanValid(player))
            return HookResult.Continue;

        var message = commandInfo.GetArg(1);
        var pitchModifiers = new Dictionary<string, float>()
        {
            { "qq", 1.5f },
            { "q", 1.3f },
            { "f", 1.15f },
            { "s", 0.85f },
            { "d", 0.75f },
            { "r", Random.Shared.Next(75, 150) / 100f }
        };
        var (keyword, pitch, keywordPtch) = ParseSaySoundMessage(message);

        if (!SaySoundHelper.SaySoundHelper.SaySounds.TryGetValue(keyword, out var saySound))
            return HookResult.Continue;

        var messageParts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (messageParts.Length > 2)
            return HookResult.Continue;

        if (messageParts.Length == 2)
        {
            if (!pitchModifiers.ContainsKey(messageParts[1]))
                return HookResult.Continue;
        }

        _music.PlaySaySound(saySound.SoundEvent, pitch);
        BroadcastLocalizedSaySoundMessage();

        return HookResult.Handled;

        #region local methods
        (string Keyword, float Pitch, string KeywordPitch) ParseSaySoundMessage(string rawMessage)
        {
            var parts = rawMessage.Split(' ', 2);
            var keyword = parts[0];
            var pitchText = parts.Length > 1 ? parts[1] : string.Empty;

            var pitch = ParsePitchModifier(pitchText);
            var keywordPitch = pitch == 1f ? keyword : $"{keyword} {pitchText}";

            return (keyword, pitch, keywordPitch);
        }

        float ParsePitchModifier(string pitchText) => pitchModifiers.TryGetValue(pitchText, out var pitch) ? pitch : 1f;

        void BroadcastLocalizedSaySoundMessage()
        {
            var humans = Utility.GetHumans();

            foreach (var human in humans)
            {
                var localizedContent = GetLocalizedSaySoundContent(human);
                Utility.PrintToChatWithTeamColor(human, $"{player.PlayerName}: {ChatColors.Grey}[{keywordPtch}] {ChatColors.Green}{localizedContent}");
            }
        }

        string GetLocalizedSaySoundContent(
            CCSPlayerController targetPlayer)
        {
            var playerCache = _playerService.GetPlayerCache(targetPlayer.SteamID);
            var language = playerCache?.Language ?? LanguageOption.English;

            var content = language switch
            {
                LanguageOption.Japanese => saySound.JPContent,
                LanguageOption.TraditionalChinese => saySound.TWContent,
                LanguageOption.English => saySound.Content,
                _ => saySound.Content
            };

            return string.IsNullOrWhiteSpace(content) ? keyword : content;
        }
        #endregion local methods
    }

    private HookResult OnMeesagePrint(UserMessage native)
    {
        var filterSet = new HashSet<string>()
        {
            "#Player_Cash_Award_Bomb_Planted",
            "#Player_Cash_Award_Bomb_Defused",
            "#Player_Cash_Award_Killed_VIP",
            "#Player_Cash_Award_Killed_Enemy",
            "#Player_Cash_Award_Killed_Enemy_Generic",
            "#Player_Cash_Award_Rescued_Hostage",
            "#Player_Cash_Award_Interact_Hostage",
            "#Player_Cash_Award_Respawn",
            "#Player_Cash_Award_Get_Killed",
            "#Player_Cash_Award_Kill_Teammate",
            "#Player_Cash_Award_Damage_Hostage",
            "#Player_Cash_Award_Kill_Hostage",
            "#Player_Point_Award_Killed_Enemy",
            "#Player_Point_Award_Killed_Enemy_Plural",
            "#Player_Point_Award_Killed_Enemy_NoWeapon",
            "#Player_Point_Award_Killed_Enemy_NoWeapon_Plural",
            "#Player_Point_Award_Assist_Enemy",
            "#Player_Point_Award_Assist_Enemy_Plural",
            "#Player_Point_Award_Picked_Up_Dogtag",
            "#Player_Point_Award_Picked_Up_Dogtag_Plural",
            "#Player_Team_Award_Killed_Enemy",
            "#Player_Team_Award_Killed_Enemy_Plural",
            "#Player_Team_Award_Bonus_Weapon",
            "#Player_Team_Award_Bonus_Weapon_Plural",
            "#Player_Team_Award_Picked_Up_Dogtag",
            "#Player_Team_Award_Picked_Up_Dogtag_Plural",
            "#Player_Team_Award_Picked_Up_Dogtag_Friendly",
            "#Player_Cash_Award_ExplainSuicide_YouGotCash",
            "#Player_Cash_Award_ExplainSuicide_TeammateGotCash",
            "#Player_Cash_Award_ExplainSuicide_EnemyGotCash",
            "#Player_Cash_Award_ExplainSuicide_Spectators",
            "#Team_Cash_Award_T_Win_Bomb",
            "#Team_Cash_Award_Elim_Hostage",
            "#Team_Cash_Award_Elim_Bomb",
            "#Team_Cash_Award_Win_Time",
            "#Team_Cash_Award_Win_Defuse_Bomb",
            "#Team_Cash_Award_Win_Hostages_Rescue",
            "#Team_Cash_Award_Win_Hostage_Rescue",
            "#Team_Cash_Award_Loser_Bonus",
            "#Team_Cash_Award_Bonus_Shorthanded",
            "#Team_Cash_Award_Loser_Bonus_Neg",
            "#Team_Cash_Award_Loser_Zero",
            "#Team_Cash_Award_Rescued_Hostage",
            "#Team_Cash_Award_Hostage_Interaction",
            "#Team_Cash_Award_Hostage_Alive",
            "#Team_Cash_Award_Planted_Bomb_But_Defused",
            "#Team_Cash_Award_Survive_GuardianMode_Wave",
            "#Team_Cash_Award_CT_VIP_Escaped",
            "#Team_Cash_Award_T_VIP_Killed",
            "#Team_Cash_Award_no_income",
            "#Team_Cash_Award_no_income_suicide",
            "#Team_Cash_Award_Generic",
            "#Team_Cash_Award_Custom",
            "#Cstrike_TitlesTXT_Game_teammate_attack"
        };

        int count = native.GetRepeatedFieldCount("param");
        if (count > 0)
        {
            string messageId = native.ReadString("param", 0); // ["#Player_Cash_Award_Killed_Enemy", "300", "Player1"]

            if (filterSet.Contains(messageId))
                return HookResult.Stop;
        }

        return HookResult.Continue;
    }
    #endregion hook result

    #region commands
    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_kick", "Kick player")]
    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnKickCommand(client, command);
    }

    [ConsoleCommand("css_info", "Server Info")]
    public void OnInfoCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnInfoCommand(client, command, _bot.RespawnTimes);
    }

    [RequiresPermissions("@css/changemap")]
    [ConsoleCommand("css_map", "Change map")]
    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnChangeMapCommand(client, command, Config.ChangeMapTimeBuffer);
    }

    [RequiresPermissions("@css/changemap")]
    [ConsoleCommand("css_maps", "Change map")]
    public void OnMapsCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnMapsCommand(client, command);
    }

    [RequiresPermissions("@css/cvar")]
    [ConsoleCommand("css_cvar", "Modify Cvar")]
    public void OnCvarCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnCvarCommand(client, command);
    }

    [RequiresPermissions("@css/rcon")]
    [ConsoleCommand("css_rcon", "Use RCON")]
    public void OnRconCommand(CCSPlayerController? client, CommandInfo command)
    {
        _command.OnRconCommand(client, command);
    }

    [ConsoleCommand("css_players", "Player List")]
    public void OnPlayersCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnPlayersCommand(client, command);
    }

    [RequiresPermissions("@css/slay")]
    [ConsoleCommand("css_slay", "Slay Player")]
    public void OnSlayCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnSlayCommand(client, command);
    }

    [RequiresPermissions("@css/cheats")]
    [ConsoleCommand("css_god", "Godmode Toggler")]
    public void OnGodCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnGodCommand(client, command);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_debug", "debug info")]
    public void OnDebugCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnDebugCommand(client, command, _weaponStatus);
    }

    [ConsoleCommand("css_revive", "Revive Command")]
    [ConsoleCommand("css_res", "Revive Command")]
    public void OnReviveCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnReviveCommand(client, command, _position[client.PlayerName], _weaponStatus[client.PlayerName]);
    }

    [ConsoleCommand("css_models", "Models Command")]
    public void OnModelsCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnModelsCommand(client, command, this);
    }

    [RequiresPermissions("@css/slay")]
    [ConsoleCommand("css_slap", "Slap Command")]
    public void OnSlapCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnSlapCommand(client, command);
    }

    [ConsoleCommand("css_volume", "Music Volume Command")]
    public void OnVolumeCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnVolumeCommand(client, command);
    }

    [ConsoleCommand("css_ss_volume", "SaySound Volume Command")]
    public void OnSaySoundVolumeCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnSaySoundVolumeCommand(client, command);
    }

    [ConsoleCommand("css_buy", "Buy Command")]
    public void OnBuyCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnBuyCommand(client, command, this);
    }

    [ConsoleCommand("css_help", "Help Command")]
    public void OnHelpCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnHelpCommand(client, command);
    }

    [ConsoleCommand("en", "Switch SaySound to English")]
    [ConsoleCommand("tw", "Switch SaySound to Traditional Chinese")]
    [ConsoleCommand("jp", "Switch SaySound to Japanese")]
    public void OnLanguageCommand(CCSPlayerController client, CommandInfo command)
    {
        var language = command.GetCommandString switch
        {
            "en" => LanguageOption.English,
            "tw" => LanguageOption.TraditionalChinese,
            "jp" => LanguageOption.Japanese,
            _ => LanguageOption.English
        };

        _command.OnLanguageCommand(client, command, language);
    }
    #endregion commands

    private void Initialize(string mapName)
    {
        Server.ExecuteCommand("mp_randomspawn 0");

        _roundCount = 0;
        _winStreak = 0;
        _warmup = true;
        _players.Clear();
        _position.Clear();
        _weaponStatus.Clear();
        _randomSpawn = false;
        _currentRoundSecond = 0;
        _endGameRound = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

        SetHumanTeam();
        ResetDefaultWeapon();
        AddMoreSpawnPoint();

        void ResetDefaultWeapon()
        {
            Server.ExecuteCommand($"mp_ct_default_primary \"\"");
            Server.ExecuteCommand($"mp_ct_default_secondary \"{Utility.GetCsItemEnumValue(CsItem.USPS)}\"");
            Server.ExecuteCommand($"mp_t_default_primary \"\"");
            Server.ExecuteCommand($"mp_t_default_secondary \"{Utility.GetCsItemEnumValue(CsItem.Glock)}\"");
            _logger.LogInformation("Reset default weapon");
        }

        void SetHumanTeam()
        {
            var humanTeam = GetHumanTeam();

            if (humanTeam == CsTeam.CounterTerrorist)
                Server.ExecuteCommand("mp_humanteam ct");
            else
                Server.ExecuteCommand("mp_humanteam t");
            _logger.LogInformation("Set human team, {humanTeam}", humanTeam);
        }

        void AddMoreSpawnPoint()
        {
            Server.NextWorldUpdate(() =>
            {
                var TSpawnPoints = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").ToList();
                var CTSpawnPoints = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist").ToList();

                if (TSpawnPoints.Count == 0)
                    _logger.LogWarning("T Spawn point not found. map: {mapName}", mapName);

                if (CTSpawnPoints.Count == 0)
                    _logger.LogWarning("CT Spawn point not found. map: {mapName}", mapName);

                var random = new Random();

                // Check and duplicate T spawn points if needed
                if (TSpawnPoints.Count > 0 && TSpawnPoints.Count < Config.SpawnPointCount)
                {
                    int neededSpawns = Config.SpawnPointCount - TSpawnPoints.Count;
                    _logger.LogInformation("T team has {current} spawn points, duplicating {needed} more to reach {target}",
                        TSpawnPoints.Count, neededSpawns, Config.SpawnPointCount);

                    for (int i = 0; i < neededSpawns; i++)
                    {
                        var randomSpawnPoint = TSpawnPoints[random.Next(TSpawnPoints.Count)];
                        var newTSpawnPoint = Utilities.CreateEntityByName<CInfoPlayerTerrorist>("info_player_terrorist");
                        newTSpawnPoint!.Teleport(randomSpawnPoint.AbsOrigin, randomSpawnPoint.AbsRotation);
                        newTSpawnPoint.DispatchSpawn();
                    }
                }

                // Check and duplicate CT spawn points if needed
                if (CTSpawnPoints.Count > 0 && CTSpawnPoints.Count < Config.SpawnPointCount)
                {
                    int neededSpawns = Config.SpawnPointCount - CTSpawnPoints.Count;
                    _logger.LogInformation("CT team has {current} spawn points, duplicating {needed} more to reach {target}",
                        CTSpawnPoints.Count, neededSpawns, Config.SpawnPointCount);

                    for (int i = 0; i < neededSpawns; i++)
                    {
                        var randomSpawnPoint = CTSpawnPoints[random.Next(CTSpawnPoints.Count)];
                        var newCTSpawnPoint = Utilities.CreateEntityByName<CInfoPlayerCounterterrorist>("info_player_counterterrorist");
                        newCTSpawnPoint!.Teleport(randomSpawnPoint.AbsOrigin, randomSpawnPoint.AbsRotation);
                        newCTSpawnPoint.DispatchSpawn();
                    }
                }
            });
            Server.NextWorldUpdate(() =>
            {
                var TSpawnPointCount = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").Count();
                var CTSpawnPointCount = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist").Count();

                _logger.LogInformation("TSpawnPointCount: {count}", TSpawnPointCount);
                _logger.LogInformation("CTSpawnPoints: {count}", CTSpawnPointCount);
            });
        }
    }

    private static void RemovePlayerProtection(CCSPlayerController? player)
    {
        if (player is not null && player.PlayerPawn.Value is not null)
            player.PlayerPawn.Value.TakesDamage = true;
    }

    private void SetClientModel(CCSPlayerController client)
    {
        Server.NextWorldUpdate(() =>
        {
            if (!Utility.IsHumanValid(client))
                return;

            var playerCache = _playerService.GetPlayerCache(client.SteamID);

            if (playerCache is null)
            {
                _logger.LogWarning("Player cache not found when setting model for {steamID}", client.SteamID);
                return;
            }

            var skinName = playerCache.PlayerSkins.FirstOrDefault(cache => cache.IsActive)?.SkinName;
            if (string.IsNullOrEmpty(skinName))
                Utility.SetClientModel(client, playerCache.DefaultSkinModelPath);
            else
                Utility.SetClientModel(client, skinName);
        });
    }

    private static void DisplayEveryoneOnRadar()
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null) continue;

            pawn.EntitySpottedState.Spotted = true;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_entitySpottedState", Schema.GetSchemaOffset("EntitySpottedState_t", "m_bSpotted"));

            Span<uint> spottedByMask = pawn.EntitySpottedState.SpottedByMask;

            for (int i = 0; i < spottedByMask.Length; i++)
            {
                spottedByMask[i] = 0;
            }

            foreach (var otherPlayer in players)
            {
                if (otherPlayer.Index != player.Index)
                {
                    int playerIndex = (int)otherPlayer.Index;
                    int maskIndex = playerIndex / 32;
                    int bitIndex = playerIndex % 32;

                    if (maskIndex < spottedByMask.Length)
                    {
                        spottedByMask[maskIndex] |= (uint)(1 << bitIndex);
                    }
                }
            }

            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_entitySpottedState", Schema.GetSchemaOffset("EntitySpottedState_t", "m_bSpottedByMask"));
        }
    }

    private void BombEventHandler(string message)
    {
        Utility.PrintToAllCenter(message);
        _bombTimer?.Kill();
    }

    private void KillTimer()
    {
        _weaponCheckTimer?.Kill();
        _roundTimer?.Kill();
        _bombTimer?.Kill();
    }

    private void Reigsters()
    {
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
        RegisterEventHandler<EventPlayerConnectFull>(PlayerFullConnectHandler);
        RegisterEventHandler<EventPlayerDisconnect>(DisconnectHandler);
        RegisterEventHandler<EventPlayerTeam>(PlayerJoinTeamHandler);
        RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler);
        RegisterEventHandler<EventPlayerDeath>(PlayerDeathHandler);
        RegisterEventHandler<EventRoundAnnounceWarmup>(WarmupHandler);
        RegisterEventHandler<EventWarmupEnd>(WarmupEndHandler);
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
        RegisterEventHandler<EventRoundEnd>(RoundEndHandler);
        RegisterEventHandler<EventPlayerHurt>(PlayerHurtHandler);
        RegisterEventHandler<EventBombPlanted>(BombPlantedHandler);
        RegisterEventHandler<EventBombDefused>(BombDefusedHandler);
        RegisterEventHandler<EventBombExploded>(BombExplodedHandler);

        AddCommandListener("say", OnPlayerSayCommand);
        AddCommandListener("say_team", OnPlayerSayCommand);

        HookUserMessage(124, OnMeesagePrint, HookMode.Pre);
    }

    private void InitialSaySoundsSync()
    {
        try
        {
            SaySoundHelper.SaySoundHelper.InitializeAsync(ModuleDirectory).GetAwaiter().GetResult();
            _logger.LogInformation("SaySoundHelper initialized with {count} sounds", SaySoundHelper.SaySoundHelper.SaySounds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SaySoundHelper");
        }
    }

    public string GetTargetNameByKeyword(string keyword)
    {
        string normalizedKeyword = keyword.Trim().ToLowerInvariant();
        string targetName = string.Empty;
        int ctr = 0;

        foreach (var playerName in _players.Keys)
        {
            if (playerName.Contains(normalizedKeyword, StringComparison.InvariantCultureIgnoreCase))
            {
                ctr++;
                targetName = playerName;
                if (ctr > 1)
                    break;
            }
        }

        return ctr == 1 ? targetName : string.Empty;
    }

    public CsTeam GetHumanTeam()
    {
        var mapName = Server.MapName;
        switch (mapName[..3])
        {
            case "cs_":
                return CsTeam.CounterTerrorist;
            case "de_":
                return CsTeam.Terrorist;
            default:
                if (AppSettings.IsDebug)
                    _logger.LogWarning("Cannot identify the category of map: {mapName}", mapName);
                return CsTeam.Terrorist;
        }
    }
}