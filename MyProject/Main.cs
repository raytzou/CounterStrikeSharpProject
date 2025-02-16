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
    private readonly Dictionary<ulong, string> _players = [];
    private readonly Dictionary<string, Position> _position = [];
    private int _playerCount = 0;
    private int _roundCount = 0;
    private bool _warmup = true;
    private int _winStreak = 0;
    private int _looseStreak = 0;
    private static bool _restart = false;

    // plugins
    private readonly ICommand _command = commmand;
    private readonly IBot _bot = bot;

    // constants
    private const int BotQuota = 20; // should I write a cfg file? .Net way or plugin way? or probably a .txt with IO API lol?
    private const float ChangeMapTimeBuffer = 2f;
    private const int SpawnPointCount = 10;

    public override void Load(bool hotreload)
    {
        if (AppSettings.IsDebug)
            _logger.LogWarning("Debug mode is on");
        _logger.LogInformation("Server host time: {DT}", DateTime.Now);
        RegisterListener<Listeners.OnMapStart>(MapStartListener);
        RegisterEventHandler<EventPlayerConnectFull>(ConnectHandler);
        RegisterEventHandler<EventPlayerDisconnect>(DisconnectHandler);
        RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler);
        RegisterEventHandler<EventPlayerDeath>(PlayerDeathHandler);
        RegisterEventHandler<EventRoundAnnounceWarmup>(WarmupHandler);
        RegisterEventHandler<EventWarmupEnd>(WarmupEndHandler, HookMode.Pre);
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
        RegisterEventHandler<EventRoundEnd>(RoundEndHandler, HookMode.Pre);
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

        return HookResult.Continue;
    }

    private HookResult PlayerDeathHandler(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot || !_position.ContainsKey(player.PlayerName)) return HookResult.Continue;

        var origin = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z);
        var rotation = new QAngle(player.PlayerPawn.Value.AbsRotation.X, player.PlayerPawn.Value.AbsRotation.Y, player.PlayerPawn.Value.AbsRotation.Z);
        var velocity = new Vector(player.PlayerPawn.Value.AbsVelocity.X, player.PlayerPawn.Value.AbsVelocity.Y, player.PlayerPawn.Value.AbsVelocity.Z);

        _position[player.PlayerName].Origin = origin;
        _position[player.PlayerName].Rotation = rotation;
        _position[player.PlayerName].Velocity = velocity;

        return HookResult.Continue;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        if (!_warmup && _roundCount != ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>())
        {
            Server.PrintToChatAll($"Round: {_roundCount}");
            Server.PrintToChatAll($"Difficulty level: {_bot.CurrentLevel}/{BotProfile.MaxLevel}");
        }

        if (!AppSettings.IsDebug)
            _bot.RoundStartBehavior(_roundCount);

        if (!_warmup)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                RemovePlayerProtection(player);
            }
        }

        if (_roundCount == ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>())
        {
            // End Game
            Server.ExecuteCommand("mp_maxrounds 1");
        }

        return HookResult.Continue;
    }

    private HookResult RoundEndHandler(EventRoundEnd eventRoundEnd, GameEventInfo gameEventInfo)
    {
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

        if (!AppSettings.IsDebug)
            _bot.RoundEndBehavior(BotQuota, _roundCount, _winStreak, _looseStreak);

        if (!_warmup)
            _roundCount++;

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

    private HookResult ConnectHandler(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        _playerCount++;
        _logger.LogInformation("{client} has connected at {DT}, IP: {ipAddress}, SteamID: {steamID}", player.PlayerName, DateTime.Now, player.IpAddress, player.SteamID);

        if (!_players.ContainsKey(player.SteamID))
        {
            _players.Add(player.SteamID, player.PlayerName);
            _position.Add(player.PlayerName, new Position());
        }
        else
            _players[player.SteamID] = player.PlayerName;

        return HookResult.Continue;
    }

    private HookResult DisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        _playerCount--;
        _logger.LogInformation("{client} has disconnected at {DT}", player.PlayerName, DateTime.Now);
        _players.Remove(player.SteamID);
        _position.Remove(player.PlayerName);

        return HookResult.Continue;
    }
    #endregion hook result

    private void MapStartListener(string mapName)
    {
        _logger.LogInformation("server has restarted: {restart}", _restart);

        if (!_restart)
        {
            _restart = true;
            return;
        }

        var hostname = ConVar.Find("hostname");

        if (string.IsNullOrEmpty(hostname?.StringValue))
            _logger.LogWarning("hostname is not be set");
        else
            _logger.LogInformation("Server name: {serverName}", hostname.StringValue);

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
        _command.OnInfoCommand(client, command, _playerCount, _roundCount);
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
    [ConsoleCommand("css_weapon", "weapon info")]
    public void OnWeaponCommand(CCSPlayerController client, CommandInfo command)
    {
        if (AppSettings.IsDebug)
        {
            command.ReplyToCommand("Weapon info is available only in debug mode");
            return;
        }

        _command.OnWeaponCommand(client, command);
    }

    [ConsoleCommand("css_revive", "revive command")]
    public void OnReviveCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnReviveCommand(client, command, _position[client.PlayerName]);
    }
    #endregion commands

    private void InitializeFileds()
    {
        _roundCount = 0;
        _playerCount = 0;
        _winStreak = 0;
        _warmup = true;
        _players.Clear();
        _position.Clear();
    }

    private string GetTargetNameByKeyword(string keyword)
    {
        int ctr = 0;

        foreach (var pair in _players)
        {
            if (pair.Value.Contains(keyword))
            {
                ctr++;
                if (ctr > 1)
                    break;
            }
        }

        if (ctr != 1)
            return string.Empty;
        else
            return _players.FirstOrDefault(player => player.Value.Contains(keyword)).Value;
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