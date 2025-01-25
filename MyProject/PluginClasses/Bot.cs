using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MyProject.PluginInterfaces;

namespace MyProject.PluginClasses;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;

    private const int NumberOfSpawnBotAtBeginning = 5;

    public void WarmupBehavior(string currentMap)
    {
        var botTeam = GetBotTeam(currentMap);

        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("bot_stop 1");

        if(botTeam != string.Empty)
            AddBot(NumberOfSpawnBotAtBeginning, botTeam);
    }

    public void WarmupEndBehavior()
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("bot_stop 0");
        Server.ExecuteCommand("bot_kick");
    }

    public void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota, string currentMap)
    {
        var botTeam = GetBotTeam(currentMap);

        if (string.IsNullOrEmpty(botTeam))
            return;

        if (!isBotFilled)
        {
            AddBot(botQuota, botTeam);
            isBotFilled = true;
        }
    }

    public void RoundEndBehavior(ref bool isBotFilled)
    {
        isBotFilled = false;
        Server.ExecuteCommand("bot_kick");
    }

    private static string GetBotTeam(string mapName) => mapName[..2] switch
    {
        "cs" => "T",
        "de" => "CT",
        _ => string.Empty,
    };

    private static void AddBot(int number, string botTeam)
    {
        for (int i = 0; i < number; i++)
        {
            Server.ExecuteCommand($"bot_add {botTeam}");
        }
    }
}
