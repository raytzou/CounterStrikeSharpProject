using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MyProject.Enum;
using MyProject.PluginInterfaces;

namespace MyProject.PluginClasses;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;

    public void WarmupBehavior()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("bot_stop 1");
        AddSpecialBot();

        void AddSpecialBot()
        {
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {BotDifficulty.None_10.ToString()} \"[S++] Kanonushi\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {BotDifficulty.None_10.ToString()} \"[S+] Artorius\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {BotDifficulty.None_10.ToString()} \"[S] Pine\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {BotDifficulty.None_10.ToString()} \"[S] Zakiyama\"");
        }
    }

    public void WarmupEndBehavior(int botQuota)
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("bot_stop 0");
        KickAndFillBot(botQuota, BotDifficulty.hard, BotNameGroup.fumo);
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
        //KickAndFillBot(botQuota);
    }

    private static string GetBotTeam(string mapName) => mapName[..2] switch
    {
        "cs" => "T",
        "de" => "CT",
        _ => string.Empty,
    };

    private void KickAndFillBot(int quota, BotDifficulty difficulty, BotNameGroup nameGroup)
    {
        KickOnlyTrashes();

        var botTeam = GetBotTeam(Server.MapName).ToLower();
        if (botTeam == string.Empty) return;

        for (int i = 1; i <= quota; i++)
        {
            string botName = $"\"[{GetBotGrade(difficulty).ToString()}] {nameGroup.ToString()}#{i:D2}\"";
            Server.ExecuteCommand($"bot_add_{botTeam} {difficulty} {botName}");
        }

        BotGrade GetBotGrade(BotDifficulty botDifficulty)
        {
            return botDifficulty switch
            {
                BotDifficulty.easy => BotGrade.E,
                BotDifficulty.hard => BotGrade.D,
                BotDifficulty.expert => BotGrade.C,
                _ => BotGrade.A,
            };
        }

        void KickOnlyTrashes()
        {
            for (int i = 0; i < Server.MaxPlayers; i++)
            {
                var client = Utilities.GetPlayerFromIndex(i);

                if (client is not null &&
                    client.IsValid &&
                    client.IsBot &&
                    client.PlayerName.Contains(BotNameGroup.fumo.ToString()))
                {
                    Server.ExecuteCommand($"kick {client.PlayerName}");
                }
            }
        }
    }
}
