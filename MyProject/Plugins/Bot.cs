using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Plugins.Interfaces;

namespace MyProject.Plugins;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;
    private int _level = 2;
    private int _respawnTimes = 0;
    private int _maxRespawnTimes = 20;

    private const string EagleEyeModel = "[???]Nano Girl";
    private const string RushModel = "[Resident Evil 2]Hunk";

    public int CurrentLevel => _level + 1;
    public int RespawnTimes => _respawnTimes;
    public int MaxRespawnTimes => _maxRespawnTimes;

    public void MapStartBehavior()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("bot_stop 1");
        Server.ExecuteCommand("bot_kick");
        AddSpecialBot();
        _level = 2;

        void AddSpecialBot()
        {
            if (GetBotTeam(Server.MapName) == CsTeam.None)
            {
                _logger.LogWarning("Bot team not found. {mapName}", Server.MapName);
                return;
            }

            var team = GetBotTeam(Server.MapName) == CsTeam.CounterTerrorist ? "ct" : "t";
            Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[0]}");
            Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[1]}");
            Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[2]}");
        }
    }

    public void WarmupEndBehavior(int botQuota)
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("bot_stop 0");
        KickAndFillBot(botQuota, GetDifficultyLevel(0, 0));
    }

    public void RoundStartBehavior(int roundCount)
    {
        SetBotMoneyToZero();
        SetSpecialBotScore();
        SetSpecialBotModel();

        _respawnTimes = _maxRespawnTimes;

        if (roundCount > 1)
        {
            SetSpecialBotWeapon(BotProfile.Special[0], CsItem.AWP); // "[ELITE]EagleEye"
            SetSpecialBotWeapon(BotProfile.Special[1], CsItem.M4A1S); // "[ELITE]mimic"
            SetSpecialBotWeapon(BotProfile.Special[2], CsItem.P90); // "[EXPERT]Rush"

            Server.NextFrameAsync(() =>
            {
                foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot))
                {
                    bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = true;
                }
            });
        }

        void SetSpecialBotWeapon(string botName, CsItem item)
        {
            var bot = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(botName));
            var botActiveWeapon = bot!.PlayerPawn.Value!.WeaponServices?.ActiveWeapon.Value?.DesignerName;
            var itemValue = Utility.GetCsItemEnumValue(item);

            if (string.IsNullOrEmpty(botActiveWeapon) || botActiveWeapon != itemValue)
            {
                bot.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = false;
                bot.GiveNamedItem(itemValue);
            }
        }

        void SetBotMoneyToZero()
        {
            foreach (var client in Utilities.GetPlayers().Where(player => player.IsBot))
            {
                client.InGameMoneyServices!.StartAccount = 0;
                client.InGameMoneyServices.Account = 0;
            }
        }

        void SetSpecialBotModel()
        {
            Server.NextFrameAsync(() =>
            {
                var eagleEye = Utilities.GetPlayerFromSlot(Main.Instance.Players[BotProfile.Special[0]]);
                var mimic = Utilities.GetPlayerFromSlot(Main.Instance.Players[BotProfile.Special[1]]);
                var rush = Utilities.GetPlayerFromSlot(Main.Instance.Players[BotProfile.Special[2]]);
                var random = new Random();
                var randomSkin = Utility.WorkshopSkins.ElementAt(random.Next(Utility.WorkshopSkins.Count));

                Utility.SetClientModel(mimic!, randomSkin.Key);
                Utility.SetClientModel(eagleEye!, EagleEyeModel);
                Utility.SetClientModel(rush!, RushModel);
            });
        }

        void SetSpecialBotScore()
        {
            var eagleEye = Utilities.GetPlayerFromSlot(Main.Instance.Players[BotProfile.Special[0]]);
            var mimic = Utilities.GetPlayerFromSlot(Main.Instance.Players[BotProfile.Special[1]]);
            var rush = Utilities.GetPlayerFromSlot(Main.Instance.Players[BotProfile.Special[2]]);

            eagleEye!.Score = 999;
            mimic!.Score = 888;
            rush!.Score = 777;
        }
    }

    public void RoundEndBehavior(int botQuota, int roundCount, int winStreak, int looseStreak)
    {
        if (roundCount > 0)
        {
            SetDefaultWeapon();
            KickAndFillBot(botQuota, GetDifficultyLevel(winStreak, looseStreak));
        }

        void SetDefaultWeapon()
        {
            var botTeam = GetBotTeam(Server.MapName);
            var team = (botTeam == CsTeam.CounterTerrorist) ? "ct" : "t";

            Server.ExecuteCommand($"mp_{team}_default_primary {GetDefaultPrimaryWeapon()}");
            Server.ExecuteCommand($"mp_{team}_default_secondary \"\"");

            string GetDefaultPrimaryWeapon()
            {
                if (botTeam == CsTeam.CounterTerrorist)
                    return Utility.GetCsItemEnumValue(CsItem.M4A4);
                else if (botTeam == CsTeam.Terrorist)
                    return Utility.GetCsItemEnumValue(CsItem.AK47);

                return string.Empty;
            }
        }
    }

    public async Task RespawnBotAsync(CCSPlayerController bot, int currentRound)
    {
        if (_respawnTimes <= 0 ||
            bot.PlayerName == BotProfile.Special[0] ||
            bot.PlayerName == BotProfile.Special[1] ||
            bot.PlayerName == BotProfile.Special[2])
            return;

        await Server.NextFrameAsync(bot.Respawn);

        if (currentRound > 1)
        {
            await Server.NextFrameAsync(() =>
            {
                bot.RemoveWeapons();
                bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = false;
            });

            await Server.NextFrameAsync(() =>
            {
                bot.GiveNamedItem(GetBotTeam(Server.MapName) == CsTeam.CounterTerrorist ? CsItem.M4A1 : CsItem.AK47);
                bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = true;
            });
        }
        _respawnTimes--;
    }

    private static CsTeam GetBotTeam(string mapName)
    {
        switch (mapName[..3])
        {
            case "cs_":
                return CsTeam.Terrorist;
            case "de_":
                return CsTeam.CounterTerrorist;
            default:
                if (mapName == "cs2_whiterun" ||
                    mapName == "sandstone_new" ||
                    mapName == "legend4" ||
                    mapName == "pango")
                    return CsTeam.CounterTerrorist;

                return CsTeam.None;
        }
    }

    private void KickAndFillBot(int quota, int level)
    {
        KickOnlyTrashes();

        var botTeam = GetBotTeam(Server.MapName);
        if (botTeam == CsTeam.None)
        {
            _logger.LogError("Bot team not found. {mapName}", Server.MapName);
            return;
        }

        var difficulty = level switch
        {
            0 => BotProfile.Difficulty.easy,
            1 or 2 => BotProfile.Difficulty.normal,
            3 or 4 => BotProfile.Difficulty.hard,
            5 or 6 or 7 => BotProfile.Difficulty.expert,
            _ => BotProfile.Difficulty.easy,
        };

        for (int i = 1; i <= quota - BotProfile.Special.Count; i++)
        {
            string botName = $"\"[{BotProfile.Grade[level]}]{BotProfile.NameGroup[level]}#{i:D2}\"";
            var team = (botTeam == CsTeam.CounterTerrorist) ? "ct" : "t";
            Server.ExecuteCommand($"bot_add_{team} {difficulty} {botName}");
        }

        Server.ExecuteCommand($"bot_quota {quota}");

        void KickOnlyTrashes()
        {
            var specialBotSet = BotProfile.Special.Values.Select(bot => bot).ToHashSet();

            foreach (var client in Utilities.GetPlayers().Where(player => player.IsBot && !specialBotSet.Contains(player.PlayerName)))
                Server.ExecuteCommand($"kick {client.PlayerName}");

            Server.ExecuteCommand($"bot_quota {BotProfile.Special.Count}");
        }
    }

    private int GetDifficultyLevel(int winStreak, int looseStreak)
    {
        if (winStreak > 1 && _level < 7)
            _level++;
        else if (looseStreak > 2 && _level > 1)
            _level--;

        SetMaxRespawnTimes(_level);

        return _level;

        void SetMaxRespawnTimes(int level)
        {
            _maxRespawnTimes = (level < 3) ? 20 : (level == 4) ? 50 : 80;
        }
    }
}
