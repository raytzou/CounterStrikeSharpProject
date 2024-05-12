using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;

namespace MyProject;

public class MyBasePlugin : BasePlugin
{
    public override string ModuleAuthor => "cynic";
    public override string ModuleName => "MyBasePlugin";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My base plugin";

    private readonly ILogger<MyBasePlugin> _logger;
    private Dictionary<int, string> _players;
    private int _playerCount = 0;
    private string _mapName = ""; 
    private static bool _restart = false;
    private int _roundNum = 0;

    public MyBasePlugin(ILogger<MyBasePlugin> logger)
    {
        _logger = logger;
        _players = new();
    }

    public override void Load(bool hotReload)
    {
        var hostnameCvar = ConVar.Find("hostname");

        _logger.LogInformation("Server host time: {DT}", DateTime.Now);

        if(hostnameCvar is null)
            _logger.LogError("Cannot find the hostname CVAR");
        else if(string.IsNullOrEmpty(hostnameCvar.StringValue))
            _logger.LogError("Cannot find the hostname");
        else
            _logger.LogInformation("Server name: {serverName}", hostnameCvar.StringValue);

        RegisterListener<Listeners.OnClientConnected>(ConnectListener);
        RegisterListener<Listeners.OnClientDisconnect>(DisconnectListener);
        RegisterListener<Listeners.OnMapStart>(MapStartListener);
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
    }

    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_kick", "Kick player")]
    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        if(command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_kick <target>");
            return;
        }

        string targetName = GetPlayerName(command.GetArg(1));

        if(string.IsNullOrEmpty(targetName))
        {
            command.ReplyToCommand("[css] Target not found.");
            return;
        }

        Server.ExecuteCommand($"kick {targetName}");
        command.ReplyToCommand($"[css] You kick {targetName}");
        _logger.LogInformation("[css] {admin} kicked {targetName} at {DT}", client.PlayerName, targetName, DateTime.Now);
        Server.PrintToChatAll($"Admin kicked {targetName}");
    }

    [ConsoleCommand("css_info", "Current counts of player")]
    public void OnInfoCommand(CCSPlayerController client, CommandInfo command)
    {
        command.ReplyToCommand("----------");
        command.ReplyToCommand($"Server local time: {DateTime.Now}");
        command.ReplyToCommand($"Current map: {Server.MapName}");
        command.ReplyToCommand($"Player: {_playerCount}/{Server.MaxPlayers}");
        command.ReplyToCommand($"Round: {_roundNum - 1}/8");
        command.ReplyToCommand("----------");
    }

    [RequiresPermissions("@css/changemap")]
    [ConsoleCommand("css_map", "Change map")]
    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command)
    {
        if(command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_map <map name>");
            return;
        }

        string mapName = GetMapName(command.GetArg(1));

        if(string.IsNullOrEmpty(mapName))
        {
            command.ReplyToCommand($"[css] Map not found: {command.GetArg(1)}");
            return;
        }

        Server.PrintToChatAll($"Admin changed map to {mapName}");
        _mapName = mapName;
        AddTimer(2.0f, ChangeMapTimer, TimerFlags.STOP_ON_MAPCHANGE);
    }

    [RequiresPermissions("@css/cvar")]
    [ConsoleCommand("css_cvar", "Modify cvar")]
    public void OnCvarCommand(CCSPlayerController client, CommandInfo command)
    {
        if(command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_cvar <ConVar> <Value>");
            return;
        }

        var cvar = ConVar.Find(command.GetArg(1));

        if (cvar is null)
        {
            command.ReplyToCommand($"[css] Cannot find the ConVar: {command.GetArg(1)}");
            return;
        }
        else if (command.ArgCount == 2)
        {
            switch (cvar.Type)
            {
                case ConVarType.Int16:
                case ConVarType.Int32:
                case ConVarType.Int64:
                case ConVarType.UInt16:
                case ConVarType.UInt32:
                case ConVarType.UInt64:
                    command.ReplyToCommand($"{cvar.Name}: {cvar.GetPrimitiveValue<int>()}");
                    break;
                case ConVarType.Float32:
                case ConVarType.Float64:
                    command.ReplyToCommand($"{cvar.Name}: {cvar.GetPrimitiveValue<float>()}");
                    break;
                case ConVarType.Bool:
                    command.ReplyToCommand($"{cvar.Name}: {cvar.GetPrimitiveValue<bool>()}");
                    break;
                default:
                    command.ReplyToCommand($"[css] ConVar: {cvar.Name}, type: {cvar.Type}");
                    break;
            }

            return;
        }

        string value;

        switch (cvar.Type)
        {
            case ConVarType.Int16:
            case ConVarType.Int32:
            case ConVarType.Int64:
            case ConVarType.UInt16: 
            case ConVarType.UInt32:
            case ConVarType.UInt64:
                if (int.TryParse(command.GetArg(2), out int parseInt))
                {
                    cvar.SetValue(parseInt);
                    value = parseInt.ToString();
                }
                else
                {
                    command.ReplyToCommand("[css] Value type error!");
                    return;
                }
                break;
            case ConVarType.Float32:
            case ConVarType.Float64:
                if (float.TryParse(command.GetArg(2), out float parseFloat))
                {
                    cvar.SetValue(parseFloat);
                    value = parseFloat.ToString();
                }
                else
                {
                    command.ReplyToCommand("[css] Value type error!");
                    return;
                }
                break;
            case ConVarType.Bool:
                if (command.GetArg(2) == "0")
                {
                    cvar.SetValue(false);
                    value = "false";
                }
                else if (command.GetArg(2) == "1")
                {
                    cvar.SetValue(true);
                    value = "true";
                }
                else
                {
                    string arg = command.GetArg(2).ToLower();

                    if (arg == "false")
                    {
                        cvar.SetValue(false);
                        value = "false";
                    }
                    else if (arg == "true")
                    {
                        cvar.SetValue(true);
                        value = "true";
                    }
                    else
                    {
                        command.ReplyToCommand("[css] Value type error!");
                        return;
                    }
                }
                break;
            default:
                try
                {
                    cvar.SetValue($"{command.GetArg(2)}");
                }
                catch (Exception ex)
                {
                    _logger.LogError("{ex}", ex.Message);
                    _logger.LogError("cvar: {name}, type: {type}, arg: {arg}", cvar.Name, cvar.Type, command.GetArg(2));
                }

                return;
        }

        Server.PrintToChatAll($"{cvar.Name} changed to {value}");
        _logger.LogInformation("{admin} changed {cvar} to {value} at {DT}", client.PlayerName, cvar.Name, value, DateTime.Now);
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        _roundNum++;

        return HookResult.Continue;
    }

    private void ConnectListener(int slot)
    {
        var playerController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(slot + 1));

        if(playerController.IsValid && !playerController.IsBot)
        {
            _playerCount++;
            _logger.LogInformation("{client} has connected at {DT}, IP: {ipAddress}", playerController.PlayerName, DateTime.Now, playerController.IpAddress);
        }

        if(!_players.ContainsKey(slot))
            _players.Add(slot, playerController.PlayerName);
        else
            _players[slot] = playerController.PlayerName;
    }

    private void DisconnectListener(int slot)
    {
        var playerController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(slot + 1));

        if(playerController.IsValid && !playerController.IsBot)
        {
            _playerCount--;
            _logger.LogInformation("{client} has disconnected at {DT}", playerController.PlayerName, DateTime.Now);
        }

        _players.Remove(slot);
    }

    private void MapStartListener(string mapName)
    {
        _roundNum = 0;
        _playerCount = 0;
        _players.Clear();

        if (!_restart)
        {
            _mapName = mapName;
            AddTimer(1.0f, RestartTimer, TimerFlags.STOP_ON_MAPCHANGE);
        }

        _logger.LogInformation("has restart: {restart}", _restart);
    }

    private string GetPlayerName(string arg)
    {
        foreach(var pair in _players)
        {
            Console.WriteLine(pair.Key + " " + pair.Value);
            if(pair.Value == arg)
                return pair.Value;
        }

        return "";
    }

    private string GetMapName(string arg)
    {
        string gameRootPath = Server.GameDirectory;

        gameRootPath += "\\csgo\\maps";

        List<string> maps = new();

        foreach(var mapPath in Directory.GetFiles(gameRootPath))
        {
            string[] arr = mapPath.Split("\\");
            string mapName = arr[arr.Length - 1].Substring(0, arr[arr.Length - 1].Length - 4);

            if(mapName.Contains("vanity") || 
               mapName.Contains("workshop_preview") ||
               mapName == "graphics_settings" ||
               mapName == "lobby_mapveto") continue;

            maps.Add(mapName);
        }

        maps.Sort();

        foreach(var map in maps)
        {
            if(map.Contains(arg))
                return map;
        }

        return "";
    }

    private void ChangeMapTimer()
    {
        Server.ExecuteCommand($"changelevel {_mapName}");
    }

    private void RestartTimer()
    {
        Server.ExecuteCommand($"changelevel {_mapName}");
        _restart = true;
    }
}