using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MyProject.PluginInterfaces;

namespace MyProject.PluginClasses;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;

    private const int NumberOfSpawnBotAtBeginning = 5;

    public void WarmupBehavior()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("bot_stop 1");
        KickAndFillBot(NumberOfSpawnBotAtBeginning);
    }

    public void WarmupEndBehavior(int botQuota)
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("bot_stop 0");
        KickAndFillBot(botQuota);
    }

    public void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota)
    {
        //var botTeam = GetBotTeam(currentMap);

        //if (string.IsNullOrEmpty(botTeam))
        //    return;

        //if (!isBotFilled)
        //{
        //    AddBot(botQuota, botTeam);
        //    isBotFilled = true;
        //}
    }

    public void RoundEndBehavior(int botQuota)
    {
        KickAndFillBot(botQuota);
    }

    private static string GetBotTeam(string mapName) => mapName[..2] switch
    {
        "cs" => "T",
        "de" => "CT",
        _ => string.Empty,
    };

    private static void KickAndFillBot(int quota)
    {
        var botTeam = GetBotTeam(Server.MapName);
        if(botTeam == string.Empty) return;

        Server.ExecuteCommand("bot_kick");

        for (int i = 0; i < quota; i++)
        {
            Server.ExecuteCommand($"bot_add {botTeam}");
        }
    }
}
