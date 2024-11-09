﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace MyProject;

public class MyBasePlugin : BasePlugin
{
    #region plugin info
    public override string ModuleAuthor => "cynic";
    public override string ModuleName => "MyBasePlugin";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My base plugin";
    #endregion plugin info

    public int RoundNum => _roundNum;
    public int PlayerCount => _playerCount;
    public static MyBasePlugin Instance => _instance;

    private readonly ILogger<MyBasePlugin> _logger;
    private Dictionary<ulong, string> _players;
    private int _playerCount = 0;
    private string _currentMap = string.Empty;
    private static bool _restart = false;
    private int _roundNum = 0;
    private static MyBasePlugin _instance;
    private static readonly object _lock = new object();

    public MyBasePlugin(ILogger<MyBasePlugin> logger)
    {
        _logger = logger;
        _players = new();
    }

    public override void Load(bool hotReload)
    {
        if (_instance is null)
        {
            lock (_lock)
            {
                _instance = this;
            }
        }

        _logger.LogInformation("instance init: " + (_instance != null));
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
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
        RegisterEventHandler<EventRoundEnd>(RoundEndHandler);
    }

    public string GetTargetName(string name)
    {
        foreach (var pair in _players)
        {
            Console.WriteLine(pair.Key + " " + pair.Value);
            if (pair.Value == name)
                return pair.Value;
        }

        return string.Empty;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        Server.PrintToChatAll($"Round: {_roundNum}");
        return HookResult.Continue;
    }

    private HookResult RoundEndHandler(EventRoundEnd eventRoundEnd, GameEventInfo gameEventInfo)
    {
        _roundNum++;
        return HookResult.Continue;
    }

    private HookResult ConnectHandler(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player.IsValid && !player.IsBot)
        {
            _playerCount++;
            _logger.LogInformation("{client} has connected at {DT}, IP: {ipAddress}, SteamID: {steamID}", player.PlayerName, DateTime.Now, player.IpAddress, player.SteamID);
        }

        if (!_players.ContainsKey(player.SteamID))
            _players.Add(player.SteamID, player.PlayerName);
        else
            _players[player.SteamID] = player.PlayerName;

        return HookResult.Continue;
    }

    private HookResult DisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player.IsValid && !player.IsBot)
        {
            _playerCount--;
            _logger.LogInformation("{client} has disconnected at {DT}", player.PlayerName, DateTime.Now);
        }

        _players.Remove(player.SteamID);

        return HookResult.Continue;
    }

    private void MapStartListener(string mapName)
    {
        _roundNum = 0;
        _playerCount = 0;
        _players.Clear();

        if (!_restart)
        {
            AddTimer(1.0f, RestartTimer, TimerFlags.STOP_ON_MAPCHANGE);
        }

        _currentMap = mapName;
        _logger.LogInformation("server has restarted: {restart}", _restart);

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
    }

    private void RestartTimer()
    {
        Server.ExecuteCommand($"changelevel {_currentMap}");
        _restart = true;
    }
}