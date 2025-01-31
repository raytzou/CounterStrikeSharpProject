using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MyProject.Enum;
using MyProject.PluginInterfaces;

namespace MyProject.PluginClasses;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;
    private readonly HashSet<string> Special = new() { "[ELITE]EagleEye", "[ELITE]mimic", "[EXPERT]Rush" };

    public void MapStartBehavior()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("bot_stop 1");
        Server.ExecuteCommand("bot_kick");
        AddSpecialBot();

        void AddSpecialBot()
        {
            var botTeam = GetBotTeam(Server.MapName);

            if (botTeam == string.Empty)
            {
                _logger.LogWarning("Bot team not found. {mapName}", Server.MapName);
                return;
            }

            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName)} {BotProfile.Difficulty.expert.ToString()} \"[ELITE]EagleEye\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName)} {BotProfile.Difficulty.expert.ToString()} \"[ELITE]mimic\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName)} {BotProfile.Difficulty.expert.ToString()} \"[EXPERT]Rush\"");
            Server.NextWorldUpdateAsync(SetBotScore);
        }
    }

    public void WarmupEndBehavior(int botQuota)
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("bot_stop 0");
        KickAndFillBot(botQuota, 0);
    }

    public void RoundStartBehavior(int roundCount)
    {
        for (int i = 0; i < Server.MaxPlayers; i++)
        {
            var client = Utilities.GetPlayerFromIndex(i);

            if (client is not null &&
                client.IsValid &&
                client.IsBot)
            {
                client.InGameMoneyServices.StartAccount = 0;
                client.InGameMoneyServices.Account = 0;
            }
        }

        SetBotScore();

        if (roundCount > 1)
        {
            SetSpecialBotWeapon("[ELITE]EagleEye", "weapon_awp");
            SetSpecialBotWeapon("[ELITE]mimic", "weapon_m4a1_silencer");
            SetSpecialBotWeapon("[EXPERT]Rush", "weapon_p90");
        }

        void SetSpecialBotWeapon(string botName, string weaponName)
        {
            var bot = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(botName));

            bot!.DropActiveWeapon();
            bot.GiveNamedItem(weaponName);
            Server.NextWorldUpdateAsync(() =>
            {
                foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot))
                {
                    bot.PlayerPawn.Value.WeaponServices.PreventWeaponPickup = true;
                }
            });
        }
    }

    public void RoundEndBehavior(int botQuota, int roundCount)
    {
        if (roundCount > 0)
        {
            SetDefaultWeapon();
            KickAndFillBot(botQuota, 0);
        }

        void SetDefaultWeapon()
        {
            var botTeam = GetBotTeam(Server.MapName);

            Server.ExecuteCommand($"mp_{botTeam}_default_primary {GetDefaultPrimaryWeapon()}");
            Server.ExecuteCommand($"mp_{botTeam}_default_secondary \"\"");

            string GetDefaultPrimaryWeapon()
            {
                if (botTeam == "ct")
                    return "weapon_m4a1";
                else if (botTeam == "t")
                    return "weapon_ak47";

                return string.Empty;
            }
        }
    }

    private string GetBotTeam(string mapName)
    {
        switch (mapName[..3])
        {
            case "cs_":
                return "t";
            case "de_":
                return "ct";
            default:
                if (mapName == "cs2_whiterun" ||
                    mapName == "sandstone_new" ||
                    mapName == "legend4" ||
                    mapName == "pango")
                    return "ct";

                return string.Empty;
        }
    }

    private void KickAndFillBot(int quota, int level)
    {
        KickOnlyTrashes();

        var botTeam = GetBotTeam(Server.MapName);
        if (botTeam == string.Empty) return;

        var difficulty = level switch
        {
            0 => BotProfile.Difficulty.easy,
            1 or 2 => BotProfile.Difficulty.normal,
            3 or 4 => BotProfile.Difficulty.hard,
            5 or 6 => BotProfile.Difficulty.expert,
            _ => BotProfile.Difficulty.easy,
        };

        for (int i = 1; i <= quota - Special.Count; i++)
        {
            string botName = $"\"[{BotProfile.Grade[level]}]{BotProfile.NameGroup[level]}#{i:D2}\"";
            Server.ExecuteCommand($"bot_add_{botTeam} {difficulty} {botName}");
        }

        Server.ExecuteCommand($"bot_quota {quota}");

        void KickOnlyTrashes()
        {
            for (int i = 0; i < Server.MaxPlayers; i++)
            {
                var client = Utilities.GetPlayerFromIndex(i);

                if (client is not null &&
                    client.IsValid &&
                    client.IsBot &&
                    !Special.Contains(client.PlayerName))
                {
                    Server.ExecuteCommand($"kick {client.PlayerName}");
                }
            }

            Server.ExecuteCommand($"bot_quota {Special.Count}");
        }
    }

    private void SetBotScore()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot)
            {
                if (player.PlayerName.Contains("EagleEye"))
                    player.Score = 999;
                else if (player.PlayerName.Contains("mimic"))
                    player.Score = 888;
                else if (player.PlayerName.Contains("Rush"))
                    player.Score = 777;
            }
        }
    }
}
