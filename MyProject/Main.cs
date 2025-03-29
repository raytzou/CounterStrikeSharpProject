using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Models;
using MyProject.PluginInterfaces;

namespace MyProject;

public class Main(
    ILogger<Main> logger,
    ICommand commmand,
    IBot bot
    ) : BasePlugin
{
    #region plugin info
    public override string ModuleAuthor => "cynicat";
    public override string ModuleName => "MyProject";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My main plugin";
    #endregion plugin info

    private readonly ILogger<Main> _logger = logger;

    // fields
    private readonly Dictionary<string, int> _players = []; // playerName -> slot
    private readonly Dictionary<string, Position> _position = [];
    private readonly Dictionary<string, WeaponStatus> _weaponStatus = [];
    private CounterStrikeSharp.API.Modules.Timers.Timer? _weaponCheckTimer = null;
    private int _roundCount = 0;
    private bool _warmup = true;
    private int _winStreak = 0;
    private int _looseStreak = 0;
    private bool _respawnBot = false;

    // plugins
    private readonly ICommand _command = commmand;
    private readonly IBot _bot = bot;

    // constants
    private const int BotQuota = 20;
    private const float ChangeMapTimeBuffer = 2f;
    private const int SpawnPointCount = 10;
    private const int CostScoreToRevive = 50;
    private const float WeaponCheckTime = 3f;
    private const bool LogWeaponTracking = false;

    // properties
    public static Main Instance { get; private set; } = null!;
    public Dictionary<string, int> Players => _players;

    public override void Load(bool hotreload)
    {
        Instance = this;
        if (AppSettings.IsDebug)
            _logger.LogWarning("Debug mode is on");
        _logger.LogInformation("Server host time: {DT}", DateTime.Now);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RegisterEventHandler<EventPlayerDisconnect>(DisconnectHandler);
        RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler);
        RegisterEventHandler<EventPlayerDeath>(PlayerDeathHandler);
        RegisterEventHandler<EventRoundAnnounceWarmup>(WarmupHandler);
        RegisterEventHandler<EventWarmupEnd>(WarmupEndHandler);
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
        RegisterEventHandler<EventRoundEnd>(RoundEndHandler);
    }

    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        foreach (var skin in Utility.WorkshopSkins)
        {
            manifest.AddResource(skin.Value.ModelPath);
            if (skin.Value.ArmPath is not null)
                manifest.AddResource(skin.Value.ArmPath);
        }
    }

    private void OnMapStart(string mapName)
    {
        var hostname = ConVar.Find("hostname");

        if (string.IsNullOrEmpty(hostname?.StringValue))
            _logger.LogWarning("hostname is not be set");
        else
            _logger.LogInformation("Server name: {serverName}", hostname.StringValue);

        Server.ExecuteCommand("mp_randomspawn 0");
        InitializeFileds();
        ResetDefaultWeapon();
        SetHumanTeam();

        AddTimer(2f, () =>
        {
            Server.NextWorldUpdateAsync(AddMoreSpawnPoint);
            _bot.MapStartBehavior();
        });

        void ResetDefaultWeapon()
        {
            Server.ExecuteCommand($"mp_ct_default_primary \"\"");
            Server.ExecuteCommand($"mp_ct_default_secondary \"{Utility.GetCsItemEnumValue(CsItem.USPS)}\"");
            Server.ExecuteCommand($"mp_t_default_primary \"\"");
            Server.ExecuteCommand($"mp_t_default_secondary \"{Utility.GetCsItemEnumValue(CsItem.Glock)}\"");
        }

        void SetHumanTeam()
        {
            var humanTeam = GetHumanTeam();

            if (humanTeam == CsTeam.CounterTerrorist)
                Server.ExecuteCommand("mp_humanteam ct");
            else
                Server.ExecuteCommand("mp_humanteam t");
        }

        void AddMoreSpawnPoint()
        {
            var TSpawnPoint = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").FirstOrDefault();
            var CTSpawnPoint = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist").FirstOrDefault();

            if (TSpawnPoint is null)
                _logger.LogWarning("T Spawn point not found. map: {mapName}", mapName);

            if (CTSpawnPoint is null)
                _logger.LogWarning("CT Spawn point not found. map: {mapName}", mapName);


            for (int i = 0; i < SpawnPointCount; i++)
            {
                if (TSpawnPoint is not null)
                {
                    var newTSpawnPoint = Utilities.CreateEntityByName<CInfoPlayerTerrorist>("info_player_terrorist");
                    newTSpawnPoint!.Teleport(TSpawnPoint.AbsOrigin);
                    newTSpawnPoint.DispatchSpawn();
                }

                if (CTSpawnPoint is not null)
                {
                    var newCTSpawnPoint = Utilities.CreateEntityByName<CInfoPlayerCounterterrorist>("info_player_counterterrorist");
                    newCTSpawnPoint!.Teleport(CTSpawnPoint.AbsOrigin);
                    newCTSpawnPoint.DispatchSpawn();
                }
            }
        }
    }

    private void OnClientPutInServer(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player is null || !player.IsValid)
            return;

        if (!player.IsBot)
        {
            _logger.LogInformation("{client} has connected at {DT}, IP: {ipAddress}, SteamID: {steamID}", player.PlayerName, DateTime.Now, player.IpAddress, player.SteamID);
            if (!_position.ContainsKey(player.PlayerName))
                _position.Add(player.PlayerName, new Position());
            if (!_weaponStatus.ContainsKey(player.PlayerName))
                _weaponStatus.Add(player.PlayerName, new WeaponStatus());
        }

        if (!_players.ContainsKey(player.PlayerName))
            _players.Add(player.PlayerName, player.Slot);
        else
            _players[player.PlayerName] = player.Slot;
    }

    #region hook result
    private HookResult PlayerSpawnHandler(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player is null || !player.IsValid || player.PlayerPawn.Value is null)
            return HookResult.Continue;

        if (_warmup && player.PlayerPawn.Value.TakesDamage)
            SetPlayerProtection(player);
        else
            RemovePlayerProtection(player);

        if (!player.IsBot)
            _weaponStatus[player.PlayerName].IsActive = true;

        return HookResult.Continue;
    }

    private HookResult PlayerDeathHandler(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null ||
            !player.IsValid)
            return HookResult.Continue;

        if (!_warmup)
        {
            if (_respawnBot && player.IsBot)
            {
                Server.NextFrameAsync(() =>
                {
                    _bot.RespawnBot(player, _roundCount);
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
                _weaponStatus[player.PlayerName].IsActive = false;
            }
        }

        return HookResult.Continue;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        if (!_warmup)
        {
            _respawnBot = true;
            Server.ExecuteCommand("mp_randomspawn 1");
            HandleRoundStartMessages();
            RemoveProtectionFromAllPlayers();
            ActivateAllWeaponStatuses();
            StartWeaponCheckTimer();
        }

        _bot.RoundStartBehavior(_roundCount);

        if (_roundCount == ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>())
        {
            // End Game
            Server.ExecuteCommand("mp_maxrounds 1");
        }

        return HookResult.Continue;

        void HandleRoundStartMessages()
        {
            if (_roundCount != ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>())
            {
                Server.PrintToChatAll($"Round: {_roundCount}");
                Server.PrintToChatAll($"Difficulty level: {_bot.CurrentLevel}/{BotProfile.MaxLevel}");
                Server.PrintToChatAll($"Bot respawn: {_bot.MaxRespawnTimes}");
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
                pair.Value.IsActive = true;
            }
        }

        void StartWeaponCheckTimer()
        {
            _weaponCheckTimer = AddTimer(WeaponCheckTime, () =>
            {
                if (AppSettings.IsDebug && LogWeaponTracking)
                    Server.PrintToChatAll("weapon check");

                foreach (var pair in _weaponStatus)
                {
                    if (!pair.Value.IsActive)
                        continue;

                    if (AppSettings.IsDebug && LogWeaponTracking)
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

                if (AppSettings.IsDebug && LogWeaponTracking)
                {
                    foreach (var cacheWeapon in pair.Value.Weapons)
                        Server.PrintToChatAll(cacheWeapon);
                }
            }
        }
    }

    private HookResult RoundEndHandler(EventRoundEnd eventRoundEnd, GameEventInfo gameEventInfo)
    {
        _respawnBot = false;

        if (eventRoundEnd.Winner == (int)GetHumanTeam())
        {
            _winStreak++;
            _looseStreak = 0;
        }
        else
        {
            _looseStreak++;
            _winStreak = 0;
        }

        if (!_warmup)
        {
            if (!AppSettings.IsDebug)
                _bot.RoundEndBehavior(BotQuota, _roundCount, _winStreak, _looseStreak);

            Server.ExecuteCommand("mp_randomspawn 0");
            _weaponCheckTimer?.Kill();
            _roundCount++;
        }
        _weaponCheckTimer?.Kill();
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
        if (!AppSettings.IsDebug)
            _bot.WarmupEndBehavior(BotQuota);
        return HookResult.Continue;
    }

    private HookResult DisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player is null || !player.IsValid)
            return HookResult.Continue;

        if (!player.IsBot)
            _logger.LogInformation("{client} has disconnected at {DT}", player.PlayerName, DateTime.Now);

        if (_position.ContainsKey(player.PlayerName))
            _position.Remove(player.PlayerName);
        if (_weaponStatus.ContainsKey(player.PlayerName))
            _weaponStatus.Remove(player.PlayerName);
        if (_players.ContainsKey(player.PlayerName))
            _players.Remove(player.PlayerName);

        return HookResult.Continue;
    }
    #endregion hook result

    #region commands
    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_kick", "Kick player")]
    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        string targetName = GetTargetNameByKeyword(command.GetArg(1));

        _command.OnKickCommand(client, command, targetName);
    }

    [ConsoleCommand("css_info", "Server Info")]
    public void OnInfoCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnInfoCommand(client, command, _players.Count, _roundCount, _bot.RespawnTimes);
    }

    [RequiresPermissions("@css/changemap")]
    [ConsoleCommand("css_map", "Change map")]
    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnChangeMapCommand(client, command, ChangeMapTimeBuffer);
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

    [ConsoleCommand("css_players", "Player List")]
    public void OnPlayersCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnPlayersCommand(client, command);
    }

    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_slay", "Slay Player")]
    public void OnSlayCommand(CCSPlayerController client, CommandInfo command)
    {
        var targetName = GetTargetNameByKeyword(command.GetArg(1));

        _command.OnSlayCommand(client, command, targetName);
    }

    [RequiresPermissions("@css/cheats")]
    [ConsoleCommand("css_god", "enable godmode")]
    public void OnGodCommand(CCSPlayerController client, CommandInfo command)
    {
        if (!ConVar.Find("sv_cheats")!.GetPrimitiveValue<bool>())
        {
            command.ReplyToCommand("God mode is available only when sv_cheats is true");
            return;
        }

        _command.OnGodCommand(client, command);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_debug", "debug info")]
    public void OnDebugCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnDebugCommand(client, command, _weaponStatus);
    }

    [ConsoleCommand("css_revive", "revive command")]
    public void OnReviveCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnReviveCommand(client, command, CostScoreToRevive, _position[client.PlayerName], _weaponStatus);
    }
    #endregion commands

    private void InitializeFileds()
    {
        _roundCount = 0;
        _winStreak = 0;
        _warmup = true;
        _players.Clear();
        _position.Clear();
        _weaponStatus.Clear();
    }

    private string GetTargetNameByKeyword(string keyword)
    {
        int ctr = 0;

        foreach (var playerName in _players.Keys)
        {
            if (playerName.Contains(keyword))
            {
                ctr++;
                if (ctr > 1)
                    break;
            }
        }

        if (ctr != 1)
            return string.Empty;

        return _players.Keys.FirstOrDefault(playerName => playerName.Contains(keyword)) ?? string.Empty;
    }

    private static void SetPlayerProtection(CCSPlayerController? player)
    {
        if (player is not null && player.PlayerPawn.Value is not null)
            player.PlayerPawn.Value.TakesDamage = false;
    }

    private static void RemovePlayerProtection(CCSPlayerController? player)
    {
        if (player is not null && player.PlayerPawn.Value is not null)
            player.PlayerPawn.Value.TakesDamage = true;
    }

    private CsTeam GetHumanTeam()
    {
        var mapName = Server.MapName;
        switch (mapName[..3])
        {
            case "cs_":
                return CsTeam.CounterTerrorist;
            case "de_":
                return CsTeam.Terrorist;
            default:
                if (mapName == "cs2_whiterun" ||
                    mapName == "sandstone_new" ||
                    mapName == "legend4" ||
                    mapName == "pango")
                    return CsTeam.Terrorist;
                _logger.LogWarning("Cannot identify the category of map: {mapName}", mapName);
                return CsTeam.Terrorist;
        }
    }
}