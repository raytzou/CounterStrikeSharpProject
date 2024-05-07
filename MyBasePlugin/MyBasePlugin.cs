using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;

namespace MyBasePlugin;

public class MyBasePlugin : BasePlugin
{
    public override string ModuleAuthor => "cynic";
    public override string ModuleName => "MyBasePlugin";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My base plugin";

    private readonly ILogger<MyBasePlugin> _logger;
    private Dictionary<int, string> _players;

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

        RegisterListener<Listeners.OnClientConnect>(ConnectHandler);
        RegisterListener<Listeners.OnClientDisconnect>(DisconnectHandler);
    }

    [RequiresPermissions("@css/kick")]
    [ConsoleCommand("css_kick", "Kick player")]
    public void OnCommand(CCSPlayerController client, CommandInfo command)
    {
        if(command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_kick <target>");
            return;
        }

        string targetName = GetTargetName(command.GetArg(1));

        if(string.IsNullOrEmpty(targetName))
        {
            command.ReplyToCommand("[css] target not found.");
            return;
        }

        Server.ExecuteCommand($"kick {targetName}");
        command.ReplyToCommand($"[css]You kick {targetName}");
        _logger.LogInformation("[css] {targetName} has been kicked at {DT}", targetName, DateTime.Now);
        Server.PrintToChatAll($"Admin kicked {targetName}");
    }

    private void ConnectHandler(int slot, string name, string ipAddress)
    {
        if(!_players.ContainsKey(slot))
            _players.Add(slot, name);
        else
            _players[slot] = name;
    }

    private void DisconnectHandler(int slot)
    {
        _players.Remove(slot);
    }

    private string GetTargetName(string arg)
    {
        foreach(var pair in _players){
            if(pair.Value == arg)
                return pair.Value;
        }

        return "";
    }
}
