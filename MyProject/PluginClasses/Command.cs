using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.PluginInterfaces;

namespace MyProject.PluginClasses;

public class Command(ILogger<Command> logger) : ICommand
{
    private readonly ILogger<Command> _logger = logger;

    public void OnKickCommand(CCSPlayerController client, CommandInfo command, string targetName)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_kick <target>");
            return;
        }

        if (string.IsNullOrEmpty(targetName))
        {
            command.ReplyToCommand("[css] Target not found.");
            return;
        }

        Server.ExecuteCommand($"kick {targetName}");
        command.ReplyToCommand($"[css] You kick {targetName}");
        _logger.LogInformation("[css] {admin} kicked {targetName} at {DT}", client.PlayerName, targetName, DateTime.Now);
        Server.PrintToChatAll($"Admin kicked {targetName}");
    }

    public void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundCount)
    {
        command.ReplyToCommand("----------");
        command.ReplyToCommand($"Server local time: {DateTime.Now}");
        command.ReplyToCommand($"Current map: {Server.MapName}");
        command.ReplyToCommand($"Player: {playerCount}/{Server.MaxPlayers}");
        command.ReplyToCommand($"Round: {roundCount}/{ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>()}");
        command.ReplyToCommand("----------");
    }

    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command, float changeMapTimeBuffer)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_map <map name>");
            return;
        }

        var mapName = command.GetArg(1);

        if (!Utility.AllMaps.Contains(mapName))
        {
            command.ReplyToCommand($"[css] Map not found: {mapName}");
            return;
        }

        _logger.LogInformation("{admin} changed map to {mapName} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, mapName, DateTime.Now);
        Server.PrintToChatAll($"Admin changed map to {mapName}");

        Utility.MyAddTimer(changeMapTimeBuffer, () =>
        {
            if (Utility.GetMapsFromWorkshop().Contains(mapName))
                Server.ExecuteCommand($"ds_workshop_changelevel {mapName}");
            else if (Utility.GetMapsInPhysicalDirectory().Contains(mapName))
                Server.ExecuteCommand($"changelevel {mapName}");
        });
    }

    public void OnMapsCommand(CCSPlayerController client, CommandInfo command)
    {
        foreach (var map in Utility.AllMaps)
        {
            command.ReplyToCommand(map);
        }
    }

    public void OnCvarCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
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

        string value = string.Empty;

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
                    //bot_stop 1
                    //Reference types must be accessed using `GetReferenceValue`
                    _logger.LogError("cvar: {name}, type: {type}, arg: {arg}", cvar.Name, cvar.Type, command.GetArg(2));
                    return;
                }
                break;
        }

        if (!string.IsNullOrEmpty(value))
        {
            Server.PrintToChatAll($"{cvar.Name} changed to {value}");
            _logger.LogInformation("{admin} changed {cvar} to {value} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, cvar.Name, value, DateTime.Now);
        }
    }

    public void OnPlayersCommand(CCSPlayerController client, CommandInfo command)
    {
        var players = Utilities.GetPlayers();
        if (players.Count == 0)
        {
            command.ReplyToCommand("no any player");
            return;
        }

        foreach (var player in players)
        {
            command.ReplyToCommand(player.PlayerName);
        }
    }

    public void OnSlayCommand(CCSPlayerController client, CommandInfo command, string targetName)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_slay <target>");
            return;
        }

        if (string.IsNullOrEmpty(targetName))
        {
            command.ReplyToCommand("[css] Target not found.");
            return;
        }

        var player = GetPlayerControllerByName();

        player!.CommitSuicide(true, true);
        command.ReplyToCommand($"[css] You slay {targetName}");
        _logger.LogInformation("[css] {admin} slew {targetName} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, targetName, DateTime.Now);
        Server.PrintToChatAll($"Admin slew {targetName}");

        CCSPlayerController? GetPlayerControllerByName()
        {
            foreach (var client in Utilities.GetPlayers())
            {
                if (client.PlayerName == targetName) return client;
            }

            return null;
        }
    }

    public void OnGodCommand(CCSPlayerController client, CommandInfo command)
    {
        if (client is null) return;
        if (ConVar.Find("sv_cheats")!.GetPrimitiveValue<bool>())
        {
            var takeDamage = client.PlayerPawn.Value!.TakesDamage;

            if (takeDamage)
                command.ReplyToCommand("[css] God mode off");
            else
                command.ReplyToCommand("[css] God mode on");

            client.PlayerPawn.Value.TakesDamage = !takeDamage;
        }
    }
}