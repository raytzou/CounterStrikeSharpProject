using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MyProject;
public class MyBot(ILogger<MyBot> logger) : BasePlugin
{
    public override string ModuleAuthor => "cynic";
    public override string ModuleName => "MyBot";
    public override string ModuleVersion => "0.87";
    public override string ModuleDescription => "My Bot Plugin";

    private readonly ILogger<MyBot> _logger = logger;
    private int _roundNum = 0;


    public override void Load(bool hotreload)
    {
        RegisterListener<Listeners.OnMapStart>(MapStartListener);
        RegisterEventHandler<EventRoundStart>(RoundStartHandler);
    }

    private void MapStartListener(string mapName)
    {
        _roundNum = 0;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        _roundNum++;

        if(_roundNum <= 1)
        {
            Server.ExecuteCommand("sv_cheats 1");
            Server.ExecuteCommand("bot_stop 1");
        }    
        else
        {
            Server.ExecuteCommand("sv_cheats 0");
            Server.ExecuteCommand("bot_stop 0");
        }

        return HookResult.Continue;
    }
}
