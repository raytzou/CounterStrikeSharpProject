using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyProject.PluginClasses;

public class MyBot(ILogger<MyBot> logger)
{
    private readonly ILogger<MyBot> _logger = logger;

    private void MapStartListener(string mapName)
    {
        //_fillBot = false;
    }

    private HookResult RoundStartHandler(EventRoundStart eventRoundStart, GameEventInfo gameEventInfo)
    {
        //if (RoundNum <= 1)
        //{
        //    Server.ExecuteCommand("sv_cheats 1");
        //    Server.ExecuteCommand("bot_stop 1");
        //}
        //else
        //{
        //    Server.ExecuteCommand("sv_cheats 0");
        //    Server.ExecuteCommand("bot_stop 0");

        //    if (!_fillBot)
        //    {
        //        var botTeam = BotJoinTeam(CurrentMap);

        //        if (string.IsNullOrEmpty(botTeam))
        //        {
        //            _logger.LogInformation("Cannot identify the category of map: {mapName}", CurrentMap);

        //            return HookResult.Continue;
        //        }

        //        for (int i = 0; i < 10; i++)
        //        {
        //            Server.ExecuteCommand($"bot_add {botTeam}");
        //        }

        //        _fillBot = true;
        //    }
        //}

        return HookResult.Continue;
    }

    private string BotJoinTeam(string mapName)
    {
        return mapName[..2] switch
        {
            "cs" => "T",
            "de" => "CT",
            _ => string.Empty,
        };
    }
}
