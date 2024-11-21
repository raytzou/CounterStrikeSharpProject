using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MyProject.Plugins;

public class MyCommand : BasePlugin
{
    #region plugin info
    public override string ModuleAuthor => "cynic";
    public override string ModuleName => "Base Command";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "base command plugin";
    #endregion plugin info

    private MyBase _myBase;

    public MyCommand(ILogger<MyCommand> logger, MyBase myBasePlugin)
    {
        _logger = logger;
        _myBase = myBasePlugin;
    }

    private readonly ILogger<MyCommand> _logger;

    public override void Load(bool hotreload)
    {

    }

    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_kick", "Kick player")]
    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_kick <target>");
            return;
        }

        string targetName = _myBase.GetTargetName(command.GetArg(1));

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

    [ConsoleCommand("css_info", "Current counts of player")]
    public void OnInfoCommand(CCSPlayerController client, CommandInfo command)
    {
        command.ReplyToCommand("----------");
        command.ReplyToCommand($"Server local time: {DateTime.Now}");
        command.ReplyToCommand($"Current map: {Server.MapName}");
        try
        {
            command.ReplyToCommand($"Player: {_myBase.PlayerCount}/{Server.MaxPlayers}");
            command.ReplyToCommand($"Round: {_myBase.RoundNum}/8");
        }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine(ex.InnerException?.Message ?? "Cannot get the inner exception msg");
            Console.WriteLine(ex.InnerException?.StackTrace ?? "Cannot get the inner StackTrace");
        }
        command.ReplyToCommand("----------");
    }

    [RequiresPermissions("@css/changemap")]
    [ConsoleCommand("css_map", "Change map")]
    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_map <map name>");
            return;
        }

        string mapName = GetMapNameInPhysicalDirectory(command.GetArg(1));

        if (string.IsNullOrEmpty(mapName))
        {
            command.ReplyToCommand($"[css] Map not found: {command.GetArg(1)}");
            return;
        }

        Server.PrintToChatAll($"Admin changed map to {mapName}");

        // LOL, take that, delegate syntax
        AddTimer(2.0f, () => ChangeMapTimer(mapName), TimerFlags.STOP_ON_MAPCHANGE);
    }

    [RequiresPermissions("@css/cvar")]
    [ConsoleCommand("css_cvar", "Modify cvar")]
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
                    _logger.LogError("cvar: {name}, type: {type}, arg: {arg}", cvar.Name, cvar.Type, command.GetArg(2));
                    return;
                }
                break;
        }

        if (!string.IsNullOrEmpty(value))
        {
            Server.PrintToChatAll($"{cvar.Name} changed to {value}");
            _logger.LogInformation("{admin} changed {cvar} to {value} at {DT}", client.PlayerName, cvar.Name, value, DateTime.Now);
        }
    }

    private void ChangeMapTimer(string mapName)
    {
        Server.ExecuteCommand($"changelevel {mapName}");
    }

    private string GetMapNameInPhysicalDirectory(string name)
    {
        string gameRootPath = Server.GameDirectory;

        gameRootPath += "\\csgo\\maps";

        List<string> maps = new();

        foreach (var mapPath in Directory.GetFiles(gameRootPath))
        {
            string[] arr = mapPath.Split("\\");
            string mapName = arr[arr.Length - 1].Substring(0, arr[arr.Length - 1].Length - 4);

            if (mapName.Contains("vanity") ||
               mapName.Contains("workshop_preview") ||
               mapName == "graphics_settings" ||
               mapName == "lobby_mapveto") continue;

            maps.Add(mapName);
        }

        maps.Sort();

        foreach (var map in maps) // should and can be optimized
        {
            if (map.Contains(name))
                return map;
        }

        return string.Empty;
    }

    public class ServiceCollectionExtensions : IPluginServiceCollection<MyBase>
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MyBase>();
        }
    }
}