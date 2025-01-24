using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using MyProject.PluginInterfaces;

namespace MyProject.PluginClasses;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;

    public void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota, string currentMap)
    {
        var botTeam = GetBotTeam(currentMap);

        if (roundCount <= 1)
        {
            Server.ExecuteCommand("sv_cheats 1");
            Server.ExecuteCommand("bot_stop 1");
            Server.ExecuteCommand("sv_cheats 0");
            AddBot(5, botTeam);
        }
        else
        {
            Server.ExecuteCommand("sv_cheats 0");
            Server.ExecuteCommand("bot_stop 0"); // bot not move in my dedicated server idk why

            if (!isBotFilled)
            {
                if (string.IsNullOrEmpty(botTeam))
                {
                    _logger.LogInformation("Cannot identify the category of map: {currentMap}", currentMap);

                    return;
                }

                AddBot(botQuota, botTeam);
                isBotFilled = true;
            }
        }
    }

    public void RoundEndBehavior(ref bool isBotFilled)
    {
        isBotFilled = false;
        Server.ExecuteCommand("bot_kick");
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

    private void AddBot(int number, string botTeam)
    {
        for (int i = 0; i < number; i++)
        {
            Server.ExecuteCommand($"bot_add {botTeam}");
        }
    }
}
