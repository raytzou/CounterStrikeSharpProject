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
    private const int BotQuota = 20;
    private const int MidBossRound = 4;
    private const int FinalBossRound = 8;

    public int CurrentLevel => _level + 1;
    public int RespawnTimes => _respawnTimes;
    public int MaxRespawnTimes => _maxRespawnTimes;

    public async Task MapStartBehavior()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("bot_stop 1");
        Server.ExecuteCommand("bot_kick");
        await AddSpecialBot(0);
        _level = 2;
    }

    public async Task WarmupEndBehavior()
    {
        if (!AppSettings.IsDebug)
        {
            Server.ExecuteCommand("sv_cheats 0");
            Server.ExecuteCommand("bot_stop 0");
        }

        await FillNormalBot(GetDifficultyLevel(0, 0));
        await FixBotAddedInHumanTeam(0);
    }

    public async Task RoundStartBehavior(int roundCount)
    {
        SetBotMoneyToZero();

        if (roundCount != MidBossRound && roundCount != FinalBossRound)
        {
            SetSpecialBotAttribute();
            SetSpecialBotModel();

            _respawnTimes = _maxRespawnTimes;

            if (roundCount > 1)
            {
                await SetSpecialBotWeapon(BotProfile.Special[0], CsItem.AWP); // "[ELITE]EagleEye"
                await SetSpecialBotWeapon(BotProfile.Special[1], CsItem.M4A1S); // "[ELITE]mimic"
                await SetSpecialBotWeapon(BotProfile.Special[2], CsItem.P90); // "[EXPERT]Rush"

                await Server.NextFrameAsync(() =>
                {
                    foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot))
                    {
                        bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = true;
                    }
                });
            }
        }
        else
        {
            _respawnTimes = 0;
        }

        async Task SetSpecialBotWeapon(string botName, CsItem item)
        {
            var bot = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(botName));
            var botActiveWeapon = bot!.PlayerPawn.Value!.WeaponServices?.ActiveWeapon.Value?.DesignerName;
            var itemValue = Utility.GetCsItemEnumValue(item);

            await Server.NextFrameAsync(async () =>
            {
                bot.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = false;
                if (botActiveWeapon != itemValue)
                {
                    if (!string.IsNullOrEmpty(botActiveWeapon))
                        bot.RemoveWeapons();
                    await Server.NextFrameAsync(() => bot.GiveNamedItem(itemValue));
                }
            });
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
                var eagleEye = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[0]));
                var mimic = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[1]));
                var rush = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[2]));
                var random = new Random();
                var randomSkin = Utility.WorkshopSkins.ElementAt(random.Next(Utility.WorkshopSkins.Count));

                Utility.SetClientModel(mimic!, randomSkin.Key);
                Utility.SetClientModel(eagleEye!, EagleEyeModel);
                Utility.SetClientModel(rush!, RushModel);
            });
        }

        void SetSpecialBotAttribute()
        {
            var eagleEye = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[0]));
            var mimic = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[1]));
            var rush = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[2]));

            eagleEye!.Score = 999;
            mimic!.Score = 888;
            rush!.Score = 777;
            if (roundCount > 1)
                rush!.PlayerPawn.Value!.Health = 250;
        }
    }

    public async Task RoundEndBehavior(int roundCount, int winStreak, int looseStreak)
    {
        if (roundCount > 0)
        {
            SetDefaultWeapon();
            await KickBotAsync();
            await AddSpecialBot(roundCount);
            if (roundCount != MidBossRound - 1 && roundCount != FinalBossRound - 1 && roundCount != FinalBossRound)
                await FillNormalBot(GetDifficultyLevel(winStreak, looseStreak));
            await FixBotAddedInHumanTeam(roundCount);
        }

        void SetDefaultWeapon()
        {
            var botTeam = GetBotTeam(Server.MapName);
            if (botTeam == CsTeam.None)
                return;
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
                var botTeam = GetBotTeam(Server.MapName);
                if (botTeam == CsTeam.None) return;
                bot.GiveNamedItem(botTeam == CsTeam.CounterTerrorist ? CsItem.M4A1 : CsItem.AK47);
                bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = true;
            });
        }
        _respawnTimes--;
    }

    private CsTeam GetBotTeam(string mapName)
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

                _logger.LogWarning("Bot team not found. {mapName}", Server.MapName);
                return CsTeam.None;
        }
    }

    private async Task FillNormalBot(int level)
    {
        await Server.NextFrameAsync(() =>
        {
            var botTeam = GetBotTeam(Server.MapName);
            if (botTeam == CsTeam.None)
                return;

            var difficulty = level switch
            {
                0 => BotProfile.Difficulty.easy,
                1 or 2 => BotProfile.Difficulty.normal,
                3 or 4 => BotProfile.Difficulty.hard,
                5 or 6 or 7 => BotProfile.Difficulty.expert,
                _ => BotProfile.Difficulty.easy,
            };

            for (int i = 1; i <= BotQuota - BotProfile.Special.Count; i++)
            {
                string botName = $"\"[{BotProfile.Grade[level]}]{BotProfile.NameGroup[level]}#{i:D2}\"";
                var team = (botTeam == CsTeam.CounterTerrorist) ? "ct" : "t";
                Server.ExecuteCommand($"bot_add_{team} {difficulty} {botName}");

                if (AppSettings.LogBotAdd)
                    _logger.LogInformation($"FillNormalBot() bot_add_{team} {difficulty} {botName}");
            }
        });
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

    private async Task AddSpecialBot(int roundCount)
    {
        await Server.NextFrameAsync(() =>
        {
            var botTeam = GetBotTeam(Server.MapName);
            if (botTeam == CsTeam.None)
                return;

            var team = botTeam == CsTeam.CounterTerrorist ? "ct" : "t";
            if (roundCount == MidBossRound - 1)
            {
                Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.easy)} {BotProfile.Boss[0]}");
                if (AppSettings.LogBotAdd)
                {
                    _logger.LogInformation("AddSpecialBot()");
                    _logger.LogInformation($"bot_add_{team} {nameof(BotProfile.Difficulty.easy)} {BotProfile.Boss[0]}");
                }
            }
            else if (roundCount == FinalBossRound - 1)
            {
                Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.easy)} {BotProfile.Boss[1]}");
                if (AppSettings.LogBotAdd)
                {
                    _logger.LogInformation("AddSpecialBot()");
                    _logger.LogInformation($"bot_add_{team} {nameof(BotProfile.Difficulty.easy)} {BotProfile.Boss[1]}");
                }
            }
            else
            {
                Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[0]}");
                Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[1]}");
                Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[2]}");
                if (AppSettings.LogBotAdd)
                {
                    _logger.LogInformation("AddSpecialBot()");
                    _logger.LogInformation($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[0]}");
                    _logger.LogInformation($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[1]}");
                    _logger.LogInformation($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[2]}");
                }
            }
        });
    }

    private static async Task KickBotAsync()
    {
        await Server.NextFrameAsync(() =>
        {
            Server.ExecuteCommand("bot_kick");
            Server.ExecuteCommand("bot_quota 0");
        });
    }

    private static async Task FixBotAddedInHumanTeam(int roundCount)
    {
        await Server.NextFrameAsync(() =>
        {
            var humanTeam = Main.Instance.HumanTeam;
            if (humanTeam == CsTeam.None)
                return;
            foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot && player.Team == humanTeam))
            {
                Server.ExecuteCommand($"kick \"{bot.PlayerName}\"");
            }

            if (roundCount == MidBossRound - 1 || roundCount == FinalBossRound - 1)
                Server.ExecuteCommand("bot_quota 1");
            else if (roundCount == FinalBossRound)
                Server.ExecuteCommand("bot_quota 3");
            else
                Server.ExecuteCommand($"bot_quota {BotQuota}");
        });
    }
}
