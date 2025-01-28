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
        Server.ExecuteCommand("bot_kick");
        AddSpecialBot();

        void AddSpecialBot()
        {
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {Difficulty.None_10.ToString()} \"[S++] Kanonushi\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {Difficulty.None_10.ToString()} \"[S+] Artorius\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {Difficulty.None_10.ToString()} \"[S] Pine\"");
            Server.ExecuteCommand($"bot_add_{GetBotTeam(Server.MapName).ToLower()} {Difficulty.None_10.ToString()} \"[S] Zakiyama\"");
        }
    }

    public void WarmupEndBehavior(int botQuota)
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("bot_stop 0");
        KickAndFillBot(botQuota, Difficulty.hard, NameGroup.fumo);
    }

    public void RoundStartBehavior()
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
                client.PlayerPawn.Value.WeaponServices.PreventWeaponPickup = true;
            }
        }
    }

    public void RoundEndBehavior(int botQuota, int roundCount)
    {
        if (roundCount > 0)
            SetDefaultWeapon();
        KickAndFillBot(botQuota, Difficulty.hard, NameGroup.fumo);

        void SetDefaultWeapon()
        {
            var botTeam = GetBotTeam(Server.MapName).ToLower();

            Server.ExecuteCommand($"mp_{botTeam}_default_primary {GetDefaultPrimaryWeapon()}");
            Server.ExecuteCommand($"mp_{botTeam}_default_secondary {GetDefaultSecondaryWeapon()}");

            string GetDefaultPrimaryWeapon()
            {
                if (botTeam == "ct")
                    return "weapon_m4a1_silencer";
                else if (botTeam == "t")
                    return "weapon_ak47";

                return string.Empty;
            }

            string GetDefaultSecondaryWeapon()
            {
                if (botTeam == "ct")
                    return "weapon_usp_silencer";
                else if (botTeam == "t")
                    return "weapon_glock";

                return string.Empty;
            }
        }
    }

    private string GetBotTeam(string mapName) => mapName[..2] switch
    {
        "cs" => "T",
        "de" => "CT",
        _ => string.Empty,
    };

    private void KickAndFillBot(int quota, Difficulty difficulty, NameGroup nameGroup)
    {
        KickOnlyTrashes();

        var botTeam = GetBotTeam(Server.MapName).ToLower();
        if (botTeam == string.Empty) return;

        for (int i = 1; i <= quota; i++)
        {
            string botName = $"\"[{GetBotGrade(difficulty).ToString()}] {nameGroup.ToString()}#{i:D2}\"";
            Server.ExecuteCommand($"bot_add_{botTeam} {difficulty} {botName}");
        }

        Grade GetBotGrade(Difficulty botDifficulty)
        {
            return botDifficulty switch
            {
                Difficulty.hard => Grade.A,
                Difficulty.normal => Grade.C,
                Difficulty.easy => Grade.E,
                _ => Grade.A,
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
                    client.PlayerName.Contains(NameGroup.fumo.ToString()))
                {
                    Server.ExecuteCommand($"kick {client.PlayerName}");
                }
            }
        }
    }
}
