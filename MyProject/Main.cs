using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
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
    private int _playerCount = 0;
    private static bool _restart = false;
    private int _roundCount = 0;

    // plugins
    private readonly ICommand _command = commmand;
    private readonly IBot _bot = bot;

    // constants
    private readonly int BotQuota = 8; // should I write a cfg file? .Net way or plugin way? or probably a .txt with IO API lol?

    public override void Load(bool hotreload)
    {
        var hostname = ConVar.Find("hostname");

        _logger.LogInformation("Server host time: {DT}", DateTime.Now);

        if (string.IsNullOrEmpty(hostname?.StringValue))
            _logger.LogError("hostname is not be set");
        else
            _logger.LogInformation("Server name: {serverName}", hostname.StringValue);

        RegisterListener<Listeners.OnMapStart>(MapStartListener);
        RegisterEventHandler<EventPlayerConnectFull>(ConnectHandler);
        RegisterEventHandler<EventPlayerDisconnect>(DisconnectHandler);
        RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler);
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

        if (_roundCount == 0 && player.PlayerPawn.Value.TakesDamage)
            SetPlayerProtection(player); // somehow spawn event triggered when player is connecting, not in the real timing

        return HookResult.Continue;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        Server.PrintToChatAll($"Round: {_roundCount}");
        //_bot.RoundStartBehavior(_roundCount, ref _isBotFilled, BotQuota);
        return HookResult.Continue;
    }

    private HookResult RoundEndHandler(EventRoundEnd eventRoundEnd, GameEventInfo gameEventInfo)
    {
        _roundCount++;
        _bot.RoundEndBehavior(BotQuota);
        return HookResult.Continue;
    }

    private HookResult WarmupHandler(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        _roundCount = 0;
        _bot.WarmupBehavior();
        return HookResult.Continue;
    }

    private HookResult WarmupEndHandler(EventWarmupEnd @event, GameEventInfo info)
    {
        _roundCount = 1;
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
            _players.Add(player.SteamID, player.PlayerName);
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

        return HookResult.Continue;
    }
    #endregion hook result

    private void MapStartListener(string mapName)
    {
        InitializeFileds();
        _logger.LogInformation("server has restarted: {restart}", _restart);

        if (!_restart)
        {
            RestartServer();
            return;
        }

        ResetDefaultWeapon();

        switch (mapName[..2])
        {
            case "cs":
                Server.ExecuteCommand("mp_humanteam CT");
                break;
            case "de":
                Server.ExecuteCommand("mp_humanteam T");
                break;
            default:
                Server.ExecuteCommand("mp_humanteam T");
                _logger.LogInformation("Cannot identify the category of map: {mapName}", mapName);
                break;
        }

        void RestartServer()
        {
            _restart = true;
            _logger.LogInformation("restarting server");
            Server.ExecuteCommand($"changelevel {Server.MapName}");
        }

        void ResetDefaultWeapon()
        {
            Server.ExecuteCommand($"mp_ct_default_primary \"\"");
            Server.ExecuteCommand($"mp_ct_default_secondary \"weapon_usp_silencer\"");
            Server.ExecuteCommand($"mp_t_default_primary \"\"");
            Server.ExecuteCommand($"mp_t_default_secondary \"weapon_glock\"");
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
        _command.OnChangeMapCommand(client, command);
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
        _command.OnPlayersCommand(client, command, _players);
    }

    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_slay", "Slay Player")]
    public void OnSlayCommand(CCSPlayerController client, CommandInfo command)
    {
        var targetName = GetTargetNameByKeyword(command.GetArg(1));

        _command.OnSlayCommand(client, command, targetName);
    }
    #endregion commands

    private void InitializeFileds()
    {
        _roundCount = 0;
        _playerCount = 0;
        _players.Clear();
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

        if(ctr != 1)
            return string.Empty;
        else
            return _players.FirstOrDefault(player => player.Value.Contains(keyword)).Value;
    }

    private void SetPlayerProtection(CCSPlayerController? player)
    {
        if (player is not null && player.PlayerPawn.Value is not null)
            player.PlayerPawn.Value.TakesDamage = false;
    }
}