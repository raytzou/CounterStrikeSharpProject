using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using MyProject.PluginsInterfaces;

namespace MyProject.PluginClasses;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;

    public void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota, string currentMap)
    {
        if (roundCount <= 1)
        {
            Server.ExecuteCommand("sv_cheats 1");
            Server.ExecuteCommand("bot_stop 1");
            Server.ExecuteCommand("sv_cheats 0");
        }
        else
        {
            Server.ExecuteCommand("sv_cheats 0");
            Server.ExecuteCommand("bot_stop 0");

            if (!isBotFilled)
            {
                var botTeam = GetBotTeam(currentMap);

                if (string.IsNullOrEmpty(botTeam))
                {
                    _logger.LogInformation("Cannot identify the category of map: {currentMap}", currentMap);

                    return;
                }

                for (int i = 0; i < botQuota; i++)
                {
                    Server.ExecuteCommand($"bot_add {botTeam}");
                }

                isBotFilled = true;
            }
        }
    }

    private string GetBotTeam(string mapName)
    {
        return mapName[..2] switch
        {
            "cs" => "T",
            "de" => "CT",
            _ => string.Empty,
        };
    }
}
