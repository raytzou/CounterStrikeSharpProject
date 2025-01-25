﻿using CounterStrikeSharp.API;
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
    public override string ModuleName => "MyMain";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My main plugin";
    #endregion plugin info

    private readonly ILogger<Main> _logger = logger;

    // fields
    private readonly Dictionary<ulong, string> _players = [];
    private int _playerCount = 0;
    private static bool _restart = false;
    private int _roundCount = 0;
    private static bool _isBotFilled = false;
    private string _mapName = string.Empty;

    // plugins
    private readonly ICommand _command = commmand;
    private readonly IBot _bot = bot;

    // constants
    private readonly int BotQuota = 10; // should I write a cfg file? .Net way or plugin way? or probably a .txt with IO API lol?

    public override void Load(bool hotreload)
    {
        var hostnameCvar = ConVar.Find("hostname");
        
        _logger.LogInformation("Server host time: {DT}", DateTime.Now);

        if (hostnameCvar is null)
            _logger.LogError("Cannot find the hostname CVAR");
        else if (string.IsNullOrEmpty(hostnameCvar.StringValue))
            _logger.LogError("Cannot find the hostname");
        else
            _logger.LogInformation("Server name: {serverName}", hostnameCvar.StringValue);

        RegisterListener<Listeners.OnMapStart>(MapStartListener);
        RegisterEventHandler<EventPlayerConnectFull>(ConnectHandler);
        RegisterEventHandler<EventPlayerDisconnect>(DisconnectHandler);
        RegisterEventHandler<EventRoundAnnounceWarmup>(WarmupHandler);
        RegisterEventHandler<EventWarmupEnd>(WarmupEndHandler);
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
        RegisterEventHandler<EventRoundEnd>(RoundEndHandler);
    }

    #region hook result
    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        Server.PrintToChatAll($"Round: {_roundCount}");
        _bot.RoundStartBehavior(_roundCount, ref _isBotFilled, BotQuota, _mapName);
        return HookResult.Continue;
    }

    private HookResult RoundEndHandler(EventRoundEnd eventRoundEnd, GameEventInfo gameEventInfo)
    {
        _roundCount++;
        _bot.RoundEndBehavior(ref _isBotFilled);
        return HookResult.Continue;
    }

    private HookResult WarmupHandler(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        _roundCount = 0;
        _bot.WarmupBehavior(_mapName);
        return HookResult.Continue;
    }

    private HookResult WarmupEndHandler(EventWarmupEnd @event, GameEventInfo info)
    {
        _roundCount = 1;
        _bot.WarmupEndBehavior();
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
        InitializeFileds(mapName);

        _logger.LogInformation("server has restarted: {restart}", _restart);

        if (!_restart)
        {
            RestartServer();
            return;
        }

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
            _logger.LogInformation("restarting server");
            Server.ExecuteCommand($"changelevel {mapName}");
            _restart = true;
        }
    }

    #region commands
    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_kick", "Kick player")]
    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        string targetName = GetTargetName(command.GetArg(1));

        _command.OnKickCommand(client, command, targetName);
    }

    [ConsoleCommand("css_info", "Current counts of player")]
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
    [ConsoleCommand("css_cvar", "Modify cvar")]
    public void OnCvarCommand(CCSPlayerController client, CommandInfo command)
    {
        _command.OnCvarCommand(client, command);
    }
    #endregion commands

    private void InitializeFileds(string mapName)
    {
        _roundCount = 0;
        _playerCount = 0;
        _players.Clear();
        _isBotFilled = false;
        _mapName = mapName;
    }

    private string GetTargetName(string name)
    {
        foreach (var pair in _players)
        {
            Console.WriteLine(pair.Key + " " + pair.Value);
            if (pair.Value == name)
                return pair.Value;
        }

        return string.Empty;
    }
}