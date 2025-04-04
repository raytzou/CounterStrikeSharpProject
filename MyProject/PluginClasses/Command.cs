using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CS2MenuManager.API.Menu;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Models;
using MyProject.PluginInterfaces;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

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

    public void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundCount, int botRespawnRemaining)
    {
        command.ReplyToCommand("----------");
        command.ReplyToCommand($"Server local time: {DateTime.Now}");
        command.ReplyToCommand($"Current map: {Server.MapName}");
        command.ReplyToCommand($"Player: {playerCount}/{Server.MaxPlayers}");
        command.ReplyToCommand($"Round: {roundCount}/{ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>()}");
        command.ReplyToCommand($"Bot respawn remaining: {botRespawnRemaining}");
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

        Utility.AddTimer(changeMapTimeBuffer, () =>
        {
            if (Utility.MapsFromWorkshop.Contains(mapName))
                Server.ExecuteCommand($"ds_workshop_changelevel {mapName}");
            else if (Utility.MapsInPhysicalDirectory.Contains(mapName))
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
                command.ReplyToCommand("[css] God mode on");
            else
                command.ReplyToCommand("[css] God mode off");

            client.PlayerPawn.Value.TakesDamage = !takeDamage;
        }
    }

    public void OnReviveCommand(CCSPlayerController client, CommandInfo command, int costScore, Position position, Dictionary<string, WeaponStatus> weaponStatus)
    {
        if (client is null) return;

        if (client.PlayerPawn.Value!.Health > 0)
        {
            command.ReplyToCommand("[css] You are alive."); // after connecting server, the Health is alawys 100, no matter player is dead or alive
            return;
        }

        if (client.Score - costScore < 0 && !AppSettings.IsDebug)
        {
            command.ReplyToCommand($"[css] You don't have enough score ({costScore}) to revive.");
            return;
        }

        if (AppSettings.IsDebug)
            command.ReplyToCommand($"X: {position.Origin.X} Y: {position.Origin.Y} Z: {position.Origin.Z}");
        else
            client.Score -= 50;

        float time = 1f;
        CounterStrikeSharp.API.Modules.Timers.Timer? timer = null;

        timer = Utility.AddTimer(time, () =>
        {
            ReviveCallBack(ref time, client, position, timer, weaponStatus);
        }, TimerFlags.REPEAT);
    }

    public void OnDebugCommand(CCSPlayerController client, CommandInfo command, Dictionary<string, WeaponStatus> weaponStatus)
    {
        if (!AppSettings.IsDebug)
        {
            command.ReplyToCommand("debug command is available only in debug mode");
            return;
        }

        if (client is null || client.PlayerPawn.Value is null)
            return;

        var weaponServices = client.PlayerPawn.Value.WeaponServices;
        if (weaponServices is null)
        {
            command.ReplyToCommand($"weapon services is null");
            return;
        }

        var weaponServiceValue = weaponServices.ActiveWeapon.Value;
        if (weaponServiceValue is null)
            command.ReplyToCommand($"weapon service value is null");
        else
            command.ReplyToCommand($"Weapon: {weaponServiceValue.DesignerName}");

        foreach (var weapon in weaponServices.MyWeapons)
        {
            if (weapon.Value is not null)
                command.ReplyToCommand($"my weapons: {weapon.Value.DesignerName}");
        }

        if (weaponStatus is null)
        {
            command.ReplyToCommand("weaponStatus is null");
            return;
        }

        foreach (var weapon in weaponStatus[client.PlayerName].Weapons)
        {
            command.ReplyToCommand($"weapon cache: {weapon}");
        }
    }

    public void OnModelsCommand(CCSPlayerController client, CommandInfo command)
    {
        var menu = new ScreenMenu("Select Models", Main.Instance);

        foreach (var skin in Utility.WorkshopSkins)
        {
            menu.AddItem(skin.Key, (player, option) =>
            {
                client.PlayerPawn.Value!.SetModel(skin.Value.ModelPath);
                if (skin.Value.MeshGroupIndex.HasValue)
                {
                    client.PlayerPawn.Value.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.MeshGroupMask =
                        Utility.ComputeMeshGroupMask(new int[] { skin.Value.MeshGroupIndex.Value }, new Dictionary<int, int>());
                }
            });
        }

        menu.Display(client);
    }

    private static void ReviveCallBack(ref float time, CCSPlayerController client, Position position, Timer? timer, Dictionary<string, WeaponStatus> weaponStatus)
    {
        time++;

        if ((5 - time) >= 1)
        {
            client.PrintToCenter($"Revive in {5 - time}");
        }
        else
        {
            client.PrintToCenter($"You have been revived.");
            client.Respawn();
            Server.NextFrameAsync(() =>
            {
                client.Pawn.Value!.Teleport(position.Origin, position.Rotation, position.Velocity);
                client.RemoveWeapons();

                foreach (var weapon in weaponStatus[client.PlayerName].Weapons)
                {
                    if (AppSettings.IsDebug)
                        client.PrintToChat($"try to give: {weapon}");
                    client.GiveNamedItem(weapon);
                }
            });
            timer?.Kill();
        }
    }
}