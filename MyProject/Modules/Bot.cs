using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Factories;
using MyProject.Modules.Interfaces;
using System.Drawing;
using System.Text.RegularExpressions;

namespace MyProject.Modules;

public class Bot(ILogger<Bot> logger) : IBot
{
    private readonly ILogger<Bot> _logger = logger;
    private int _level = 2;
    private int _respawnTimes = 0;
    private int _maxRespawnTimes = 20;
    private readonly List<CounterStrikeSharp.API.Modules.Timers.Timer> _damageTimers = new();

    private static readonly Regex NormalBotNameRegex = new(@"^\[(?<Grade>[^\]]+)\](?<Group>[^#]+)#(?<Num>\d{1,2})$");
    private static readonly HashSet<string> _specialBots = BotProfile.Special.Values.ToHashSet();
    private static readonly HashSet<string> _bosses = BotProfile.Boss.Values.ToHashSet();
    private static readonly HashSet<string> _specialAndBoss = _specialBots.Concat(_bosses).ToHashSet();

    public int CurrentLevel => _level + 1;
    public int RespawnTimes => _respawnTimes;
    public int MaxRespawnTimes => _maxRespawnTimes;
    public IReadOnlySet<string> SpecialAndBoss => _specialAndBoss;
    public IReadOnlySet<string> SpecialBots => _specialBots;
    public IReadOnlySet<string> Bosses => _bosses;

    public bool IsBoss(CCSPlayerController player) => _bosses.Contains(player.PlayerName);

    public async Task MapStartBehavior(string mapName)
    {
        await StopBotMoving();

        var botTeam = GetBotTeam(mapName);
        if (botTeam != CsTeam.None)
            await AddSpecialOrBoss(botTeam);

        _level = 2;

        if (AppSettings.IsDebug)
        {
            await Server.NextWorldUpdateAsync(() => Server.ExecuteCommand("sv_cheats 1"));
        }

        static async Task StopBotMoving()
        {
            await Server.NextWorldUpdateAsync(() =>
            {
                Server.ExecuteCommand("css_cvar bot_stop 1");
            });
        }
    }

    public async Task WarmupEndBehavior(string mapName)
    {
        if (!AppSettings.IsDebug)
        {
            await Server.NextFrameAsync(() => Server.ExecuteCommand("sv_cheats 0"));
            await Server.NextFrameAsync(() => Server.ExecuteCommand("bot_stop 0"));
        }

        var botTeam = GetBotTeam(mapName);
        if (botTeam != CsTeam.None)
            await FillNormalBotAsync(GetDifficultyLevel(0, 0), botTeam);
    }

    public async Task RoundStartBehavior(string mapName)
    {
        ClearDamageTimer();
        await SetBotMoneyToZero();

        if (Main.Instance.RoundCount != Main.Instance.Config.MidBossRound && Main.Instance.RoundCount != Main.Instance.Config.FinalBossRound)
        {
            await SetSpecialBotAttribute();
            await SetSpecialBotModel();

            _respawnTimes = _maxRespawnTimes;

            if (Main.Instance.RoundCount > 1)
            {
                await SetSpecialBotWeapon(BotProfile.Special[0], CsItem.AWP); // "[ELITE]EagleEye"
                await SetSpecialBotWeapon(BotProfile.Special[1], CsItem.M4A1S); // "[ELITE]mimic"
                await SetSpecialBotWeapon(BotProfile.Special[2], CsItem.P90); // "[EXPERT]Rush" 

                foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot))
                {
                    if (!Utility.IsBotValid(bot)) continue;

                    await PreventBotPickupWeapon(bot);
                }
            }
        }
        else
        {
            _respawnTimes = 0;

            if (Main.Instance.RoundCount == Main.Instance.Config.MidBossRound)
            {
                //var midBoss = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(BotProfile.Boss[0]));
                //midBoss!.PlayerPawn.Value!.Health = Main.Instance.Config.MidBossHealth;
            }
            else if (Main.Instance.RoundCount == Main.Instance.Config.FinalBossRound)
            {
                var finalBoss = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(BotProfile.Boss[1]));
                finalBoss!.PlayerPawn.Value!.Health = Main.Instance.Config.FinalBossHealth;
            }
        }

        async Task SetSpecialBotWeapon(string botName, CsItem item)
        {
            var bot = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(botName));
            var botActiveWeapon = bot!.PlayerPawn.Value!.WeaponServices?.ActiveWeapon.Value?.DesignerName;
            var itemValue = Utility.GetCsItemEnumValue(item);

            await AllowBotPickupWeapon(bot);
            await RemoveBotWeapon(bot);
            await GiveBotWeapon(bot, mapName, item);
            await PreventBotPickupWeapon(bot);

            if (AppSettings.IsDebug)
            {
                await Server.NextFrameAsync(() =>
                {
                    if (Utility.IsBotValid(bot))
                    {
                        var currentWeapon = bot.PlayerPawn.Value!.WeaponServices?.ActiveWeapon.Value?.DesignerName;
                        if (!Utility.IsWeaponMatch(item, currentWeapon ?? ""))
                        {
                            _logger.LogWarning("Special bot {BotName} weapon mismatch. Expected: {Expected}, Actual: {Actual}",
                                bot.PlayerName, Utility.GetCsItemEnumValue(item), currentWeapon ?? "None");
                        }
                    }
                });
            }
        }

        async Task SetBotMoneyToZero()
        {
            await Server.NextFrameAsync(() =>
            {
                foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot))
                {
                    if (!Utility.IsBotValid(bot)) continue;
                    bot.InGameMoneyServices!.StartAccount = 0;
                    bot.InGameMoneyServices.Account = 0;
                }
            });
        }

        async Task SetSpecialBotModel()
        {
            await Server.NextFrameAsync(() =>
            {
                try
                {
                    var eagleEye = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[0]));
                    var mimic = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[1]));
                    var rush = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[2]));
                    var random = new Random();
                    var randomSkin = Utility.WorkshopSkins.ElementAt(random.Next(Utility.WorkshopSkins.Count));

                    Utility.SetClientModel(mimic!, randomSkin.Key);
                    Utility.SetClientModel(eagleEye!, Main.Instance.Config.EagleEyeModel);
                    Utility.SetClientModel(rush!, Main.Instance.Config.RushModel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Special bots not ready yet, skipping model setup: {error}", ex.Message);
                }
            });
        }

        async Task SetSpecialBotAttribute()
        {
            await Server.NextFrameAsync(() =>
            {
                try
                {
                    var eagleEye = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[0]));
                    var mimic = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[1]));
                    var rush = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[2]));

                    eagleEye!.Score = 999;
                    mimic!.Score = 888;
                    rush!.Score = 777;
                    if (Main.Instance.RoundCount > 1)
                        rush!.PlayerPawn.Value!.Health = 250;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Special bots not ready yet, skipping attribute setup: {error}", ex.Message);
                }
            });
        }
    }

    public async Task RoundEndBehavior(int winStreak, int looseStreak, string mapName)
    {
        ClearDamageTimer();
        var botTeam = GetBotTeam(mapName);

        if (Main.Instance.RoundCount > 0)
        {
            await SetDefaultWeapon();
            if (botTeam != CsTeam.None)
                await AddSpecialOrBoss(botTeam);
            await KickNormalBotAsync();
            if (Main.Instance.RoundCount != Main.Instance.Config.MidBossRound && Main.Instance.RoundCount < Main.Instance.Config.FinalBossRound)
            {
                if (botTeam != CsTeam.None)
                    await FillNormalBotAsync(GetDifficultyLevel(winStreak, looseStreak), botTeam);
            }
        }

        async Task SetDefaultWeapon()
        {
            var botTeam = GetBotTeam(mapName);
            if (botTeam == CsTeam.None)
                return;
            var team = (botTeam == CsTeam.CounterTerrorist) ? "ct" : "t";

            await Server.NextFrameAsync(() => Server.ExecuteCommand($"mp_{team}_default_primary {GetDefaultPrimaryWeapon()}"));
            await Server.NextFrameAsync(() => Server.ExecuteCommand($"mp_{team}_default_secondary \"\""));

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

    public async Task RespawnBotAsync(CCSPlayerController bot, string mapName)
    {
        if (_respawnTimes <= 0)
            return;
        if (!Utility.IsBotValid(bot))
            return;

        try
        {
            await Server.NextFrameAsync(() =>
            {
                // check again if the round has ended
                if (Main.Instance.IsRoundEnd)
                    return;

                // double-check to ensure the entity is still valid during execution
                if (!Utility.IsBotValid(bot))
                    return;

                bot.Respawn();
            });

            if (Main.Instance.RoundCount > 1)
            {
                await AllowBotPickupWeapon(bot);
                await RemoveBotWeapon(bot);
                await GiveBotWeapon(bot, mapName);
                await PreventBotPickupWeapon(bot);

                if (AppSettings.IsDebug)
                {
                    await Server.NextFrameAsync(() =>
                    {
                        if (Utility.IsBotValid(bot))
                        {
                            var currentWeapon = bot.PlayerPawn.Value!.WeaponServices?.ActiveWeapon.Value?.DesignerName;
                            var botTeam = GetBotTeam(mapName);
                            var expectedWeapon = Utility.GetCsItemEnumValue(botTeam == CsTeam.CounterTerrorist ? CsItem.M4A1 : CsItem.AK47);
                            if (currentWeapon != expectedWeapon)
                            {
                                _logger.LogWarning("Respawn bot {BotName} weapon mismatch. Expected: {Expected}, Actual: {Actual}",
                                    bot.PlayerName, expectedWeapon, currentWeapon ?? "None");
                            }
                        }
                    });
                }
            }

            _respawnTimes--;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Entity is not valid"))
        {
            // skip when entity is invalid, not counted as failure
            return;
        }
    }

    public void BossBehavior(CCSPlayerController boss)
    {
        var activeAbilityChance = GetChance();

        if (activeAbilityChance <= Main.Instance.Config.BossActiveAbilityChance)
        {
            var random = new Random();
            var abilityChoice = random.Next(1, 7); // 1-4 for four different abilities

            switch (abilityChoice)
            {
                case 1:
                    FireTorture();
                    break;
                case 2:
                    Freeze();
                    break;
                case 3:
                    Flashbang();
                    break;
                case 4:
                    Explosion();
                    break;
                case 5:
                    ToxicSmoke();
                    break;
                case 6:
                    Cursed();
                    break;
            }
        }

        double GetChance()
        {
            var random = new Random();
            return random.NextDouble() * 100; // Returns a value between 0-100
        }

        void FireTorture()
        {
            CreateTimedProjectileAttack(
                "The Boss ignites all players!",
                System.Drawing.Color.Red,
                CreateMolotovAtPosition
            );

            void CreateMolotovAtPosition(Vector position)
            {
                CreateProjectileAtPosition<CMolotovProjectile>(
                    position,
                    boss,
                    7.0f
                );
            }
        }

        void Freeze()
        {
            Utility.PrintToAllCenter("The Boss locks the battlefield in ice!");
            var humanPlayers = Utility.GetAliveHumanPlayers();

            if (humanPlayers.Count == 0) return;

            var frozenPlayers = new List<CCSPlayerController>();

            Server.NextFrame(() =>
            {
                foreach (var player in humanPlayers)
                {
                    if (!Utility.IsPlayerValidAndAlive(player)) continue;

                    player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_NONE;
                    ApplyScreenOverlay(player.PlayerPawn.Value, 3f);
                    frozenPlayers.Add(player);
                }
            });

            Utility.AddTimer(2f, () =>
            {
                Server.NextFrame(() =>
                {
                    foreach (var player in frozenPlayers)
                    {
                        if (!Utility.IsPlayerValidAndAlive(player)) continue;

                        player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_WALK;
                    }
                });
            });
        }

        void Flashbang()
        {
            CreateTimedProjectileAttack(
                "The Boss blinds the battlefield!",
                System.Drawing.Color.Transparent,
                CreateFlashbangAtPosition,
                0f
            );

            void CreateFlashbangAtPosition(Vector position)
            {
                CreateProjectileAtPosition<CFlashbangProjectile>(
                    position,
                    boss,
                    7.0f
                );
            }
        }

        void Explosion()
        {
            CreateTimedProjectileAttack(
                "The Boss prepares explosive devastation!",
                System.Drawing.Color.Orange,
                CreateGrenadeAtPosition
            );

            void CreateGrenadeAtPosition(Vector position)
            {
                CreateProjectileAtPosition<CHEGrenadeProjectile>(
                    position,
                    boss,
                    cleanupTime: 3.0f
                );
            }
        }

        void ToxicSmoke()
        {
            Utility.PrintToAllCenter("The Boss releases toxic clouds!");
            var humanPlayers = Utility.GetAliveHumanPlayers();
            if (humanPlayers.Count == 0)
                return;

            var random = new Random();
            var targetCount = Math.Max(1, humanPlayers.Count / 3);
            var selectedPlayers = humanPlayers
                .OrderBy(x => random.Next())
                .Take(targetCount)
                .ToList();

            var markedPositions = new List<Vector>();

            Server.NextFrame(() =>
            {
                foreach (var player in selectedPlayers)
                {
                    if (!player.IsValid || player.PlayerPawn.Value == null)
                        continue;

                    var playerPosition = player.PlayerPawn.Value!.AbsOrigin!;
                    var markedPosition = new Vector(
                        playerPosition.X,
                        playerPosition.Y,
                        playerPosition.Z
                    );
                    markedPositions.Add(markedPosition);

                    Utility.DrawBeaconOnPlayer(player, System.Drawing.Color.Green, 100.0f, 15.0f, 2.0f);
                }
            });

            Utility.AddTimer(1.0f, () =>
            {
                foreach (var position in markedPositions)
                {
                    CreateToxicSmokeAtPosition(position);
                }
            });

            void CreateToxicSmokeAtPosition(Vector position)
            {
                CreateProjectileAtPosition<CSmokeGrenadeProjectile>(
                    position,
                    boss,
                    15.0f
                );

                CreateToxicDamageZone(position);
            }

            void CreateToxicDamageZone(Vector smokePosition)
            {
                const float damageRadius = 150.0f;
                const int damagePerSecond = 5;
                const float smokeDuration = 15.0f;

                var startTime = Server.CurrentTime;

                var toxicTimer = Utility.AddTimer(1f, () =>
                {
                    var currentTime = Server.CurrentTime;
                    if (currentTime - startTime >= smokeDuration)
                        return;

                    var humanPlayers = Utility.GetAliveHumanPlayers();

                    foreach (var player in humanPlayers)
                    {
                        if (!Utility.IsPlayerValidAndAlive(player))
                            continue;

                        var playerPosition = player.PlayerPawn.Value!.AbsOrigin!;
                        var distance = CalculateDistance(playerPosition, smokePosition);

                        if (distance <= damageRadius)
                        {
                            var currentHealth = player.PlayerPawn.Value!.Health;
                            var newHealth = Math.Max(1, currentHealth - damagePerSecond);

                            player.PlayerPawn.Value!.Health = newHealth;
                            Utilities.SetStateChanged(player.PlayerPawn.Value!, "CBaseEntity", "m_iHealth");
                            player.PrintToCenter($"Toxic Smoke: -{damagePerSecond} HP");

                            ApplyScreenOverlay(player.PlayerPawn.Value, 1f);
                        }
                    }
                }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

                _damageTimers.Add(toxicTimer);

                Utility.AddTimer(smokeDuration, () =>
                {
                    _damageTimers.Remove(toxicTimer);
                    toxicTimer?.Kill();
                });
            }

            float CalculateDistance(Vector pos1, Vector pos2)
            {
                var dx = pos1.X - pos2.X;
                var dy = pos1.Y - pos2.Y;
                var dz = pos1.Z - pos2.Z;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        void Cursed()
        {
            var bossHealth = boss.PlayerPawn.Value!.Health;
            var maxHealth = IsBoss(boss) && boss.PlayerName.Contains(BotProfile.Boss[0]) ? Main.Instance.Config.MidBossHealth : Main.Instance.Config.FinalBossHealth;
            var oneThirdHealth = maxHealth / 3;

            if (bossHealth > oneThirdHealth)
                return;

            Utility.PrintToAllCenter("The Boss casts a deadly curse upon all!");
            var humanPlayers = Utility.GetAliveHumanPlayers();

            if (humanPlayers.Count == 0)
                return;

            const int curseDamage = 2;
            const float curseDuration = 10.0f;
            var startTime = Server.CurrentTime;

            // Add purple beacon effect to all players to indicate curse
            Server.NextFrame(() =>
            {
                foreach (var player in humanPlayers)
                {
                    if (!Utility.IsPlayerValidAndAlive(player)) continue;

                    // Use purple beacon to mark cursed players
                    Utility.DrawBeaconOnPlayer(player, System.Drawing.Color.Purple, 100.0f, curseDuration, 1.0f);
                }
            });

            // Create curse damage timer
            var cursedTimer = Utility.AddTimer(1f, () =>
            {
                var currentTime = Server.CurrentTime;
                if (currentTime - startTime >= curseDuration)
                    return;

                var alivePlayers = Utility.GetAliveHumanPlayers();

                foreach (var player in alivePlayers)
                {
                    if (!Utility.IsPlayerValidAndAlive(player))
                        continue;

                    try
                    {
                        Utility.SlapPlayer(player, curseDamage, true);

                        player.PrintToCenter($"Cursed: -{curseDamage} HP");

                        var color = Color.FromArgb(102, 193, 45, 45);
                        Utility.ColorScreen(player, color, 0.3f, 0.2f);
                    }
                    catch (ArgumentException)
                    {
                        // Player is dead or invalid, ignore error
                        continue;
                    }
                }
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

            _damageTimers.Add(cursedTimer);

            Utility.AddTimer(curseDuration, () =>
            {
                _damageTimers.Remove(cursedTimer);
                cursedTimer?.Kill();
                Utility.PrintToAllCenter("The curse has been lifted...");
            });
        }

        void CreateTimedProjectileAttack(string message, System.Drawing.Color beaconColor, Action<Vector> createProjectileAction, float delayTime = 3.0f)
        {
            Utility.PrintToAllCenter(message);
            var humanPlayers = Utility.GetAliveHumanPlayers();
            if (humanPlayers.Count == 0)
                return;

            var markedPositions = new List<Vector>();

            Server.NextFrame(() =>
            {
                foreach (var player in humanPlayers)
                {
                    if (!player.IsValid || player.PlayerPawn.Value == null) // check player is valid at next frame
                        continue;

                    var playerPosition = player.PlayerPawn.Value!.AbsOrigin!;
                    var markedPosition = new Vector(
                        playerPosition.X,
                        playerPosition.Y,
                        playerPosition.Z
                    );
                    markedPositions.Add(markedPosition);

                    var beaconDuration = (beaconColor == System.Drawing.Color.Green) ? 15.0f : 6.0f;
                    Utility.DrawBeaconOnPlayer(player, beaconColor, 100.0f, beaconDuration, 2.0f);
                }
            });

            Utility.AddTimer(delayTime, () =>
            {
                foreach (var position in markedPositions)
                {
                    createProjectileAction(position);
                }
            });
        }

        void CreateProjectileAtPosition<T>(Vector position, CCSPlayerController attacker, float cleanupTime = 3.0f) where T : CBaseCSGrenadeProjectile
        {
            // Early check: ensure attacker is valid before creating projectile
            if (attacker == null || !attacker.IsValid || attacker.PlayerPawn.Value == null || !attacker.PlayerPawn.Value.IsValid)
                return;

            var defaultVelocity = new Vector(0, 0, 0);
            var defaultAngle = new QAngle(0, 0, 0);
            var projectile = GrenadeProjectileFactory.Create<T>(position, defaultAngle, defaultVelocity);
            if (projectile == null)
                return;

            var centerPosition = new Vector(
                position.X,
                position.Y,
                position.Z + 40.0f
            );

            projectile.Teleport(centerPosition);
            projectile.DispatchSpawn();
            projectile.Elasticity = 0f;

            // Double-check attacker validity before setting ownership properties
            // This handles cases where the attacker becomes invalid between the initial check and this point
            if (attacker.IsValid)
            {
                projectile.TeamNum = attacker.TeamNum;
                projectile.Thrower.Raw = attacker.PlayerPawn.Raw;
                projectile.OriginalThrower.Raw = attacker.PlayerPawn.Raw;
                projectile.OwnerEntity.Raw = attacker.PlayerPawn.Raw;
            }
            // If attacker becomes invalid, projectile will have no owner
            // Players can still be damaged, but kills will count as environmental/suicide

            if (projectile is CSmokeGrenadeProjectile smokeProjectile)
            {
                smokeProjectile.SmokeColor.X = 0;
                smokeProjectile.SmokeColor.Y = 255;
                smokeProjectile.SmokeColor.Z = 0;
            }

            Utility.AddTimer(0.05f, () =>
            {
                if (projectile != null && projectile.IsValid)
                {
                    Utility.AddTimer(cleanupTime, () =>
                    {
                        if (projectile != null && projectile.IsValid)
                        {
                            projectile.Remove();
                        }
                    });
                }
            });
        }
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

    private async Task FillNormalBotAsync(int level, CsTeam botTeam)
    {
        var difficulty = level switch
        {
            0 => BotProfile.Difficulty.easy,
            1 or 2 => BotProfile.Difficulty.normal,
            3 or 4 => BotProfile.Difficulty.hard,
            5 or 6 or 7 => BotProfile.Difficulty.expert,
            _ => BotProfile.Difficulty.easy,
        };

        await Server.NextFrameAsync(() =>
        {
            for (int i = 1; i <= Main.Instance.Config.BotQuota - BotProfile.Special.Count; i++)
            {
                string botName = $"\"[{BotProfile.Grade[level]}]{BotProfile.NameGroup.Keys.ToList()[level]}#{i:D2}\"";
                var team = (botTeam == CsTeam.CounterTerrorist) ? "ct" : "t";
                Server.ExecuteCommand($"bot_add_{team} {difficulty} {botName}");

                if (AppSettings.LogBotAdd)
                    _logger.LogInformation("FillNormalBot() bot_add_{team} {difficulty} {botName}", team, difficulty, botName);
            }

            Server.ExecuteCommand($"bot_quota {Main.Instance.Config.BotQuota}");
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

    private async Task AddSpecialOrBoss(CsTeam botTeam)
    {
        var team = botTeam == CsTeam.CounterTerrorist ? "ct" : "t";
        if (Main.Instance.RoundCount == Main.Instance.Config.MidBossRound)
        {
            var midBossSpawn = Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Boss[0]) == 1;
            if (!midBossSpawn)
            {
                Utility.AddTimer(0.5f, async () =>
                {
                    await KickBotAsync();
                    await Server.NextWorldUpdateAsync(() =>
                    {
                        Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Boss[0]}");
                        Server.ExecuteCommand("bot_quota 1");
                        if (AppSettings.LogBotAdd)
                        {
                            _logger.LogInformation("AddSpecialOrBoss()");
                            _logger.LogInformation("bot_add_{team} {difficulty} {boss}", team, nameof(BotProfile.Difficulty.expert), BotProfile.Boss[0]);
                        }
                    });
                });
            }
        }
        else if (Main.Instance.RoundCount == Main.Instance.Config.FinalBossRound)
        {
            var finalBossSpawn = Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Boss[1]) == 1;
            if (!finalBossSpawn)
            {
                Utility.AddTimer(0.5f, async () =>
                {
                    await KickBotAsync();
                    await Server.NextWorldUpdateAsync(() =>
                    {
                        Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Boss[1]}");
                        Server.ExecuteCommand("bot_quota 1");
                        if (AppSettings.LogBotAdd)
                        {
                            _logger.LogInformation("AddSpecialOrBoss()");
                            _logger.LogInformation("bot_add_{team} {difficulty} {boss}", team, nameof(BotProfile.Difficulty.expert), BotProfile.Boss[1]);
                        }
                    });
                });
            }
        }
        else
        {
            var specialBotSpawn = Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Special[0]) == 1 &&
                Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Special[1]) == 1 &&
                Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Special[2]) == 1;

            if (!specialBotSpawn)
            {
                await KickBotAsync();
                await Server.NextWorldUpdateAsync(() =>
                {
                    Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[0]}");
                    Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[1]}");
                    Server.ExecuteCommand($"bot_add_{team} {nameof(BotProfile.Difficulty.expert)} {BotProfile.Special[2]}");
                    Server.ExecuteCommand("bot_quota 3");
                    if (AppSettings.LogBotAdd)
                    {
                        _logger.LogInformation("AddSpecialOrBoss()");
                        _logger.LogInformation("bot_add_{team} {difficulty} {special}", team, nameof(BotProfile.Difficulty.expert), BotProfile.Special[0]);
                        _logger.LogInformation("bot_add_{team} {difficulty} {special}", team, nameof(BotProfile.Difficulty.expert), BotProfile.Special[1]);
                        _logger.LogInformation("bot_add_{team} {difficulty} {special}", team, nameof(BotProfile.Difficulty.expert), BotProfile.Special[2]);
                    }
                });
            }
        }
    }

    private static async Task KickBotAsync()
    {
        await Server.NextWorldUpdateAsync(() =>
        {
            Server.ExecuteCommand("bot_kick");
        });
    }

    private static async Task KickNormalBotAsync()
    {
        await Server.NextFrameAsync(() =>
        {
            foreach (var bot in Utilities.GetPlayers().Where(p => p.IsBot))
            {
                var match = NormalBotNameRegex.Match(bot.PlayerName);

                if (match.Success)
                {
                    Server.ExecuteCommand($"kick {bot.PlayerName}");
                }
            }

            Server.ExecuteCommand("bot_quota 3");
        });
    }

    private void ClearDamageTimer()
    {
        foreach (var timer in _damageTimers)
        {
            timer?.Kill();
        }
        _damageTimers.Clear();
    }

    private static void ApplyScreenOverlay(CCSPlayerPawn pawn, float timeInterval)
    {
        ApplyOverlay();

        void ApplyOverlay(int attempt = 0)
        {
            if (pawn == null || !pawn.IsValid) return;
            if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

            var currentTime = Server.CurrentTime;
            var future = currentTime + MathF.Max(0.1f, timeInterval);

            pawn.HealthShotBoostExpirationTime = 0.0f;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");

            Utility.AddTimer(0.01f, () =>
            {
                if (pawn == null || !pawn.IsValid) return;
                pawn.HealthShotBoostExpirationTime = future;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
            });

            if (attempt < 3)
            {
                Utility.AddTimer(0.15f, () =>
                {
                    if (pawn == null || !pawn.IsValid) return;

                    var t = pawn.HealthShotBoostExpirationTime;
                    var stillPast = t <= Server.CurrentTime + 0.05f;

                    if (stillPast)
                    {
                        ApplyOverlay(attempt + 1);
                    }
                });
            }
        }
    }

    private async Task AllowBotPickupWeapon(CCSPlayerController bot)
    {
        // prevent pickup = false
        await Server.NextFrameAsync(() =>
        {
            // double-check pawn
            if (!Utility.IsBotValid(bot))
                return;

            bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = false;
        });
    }

    private async Task RemoveBotWeapon(CCSPlayerController bot)
    {
        // remove bot's weapon
        await Server.NextFrameAsync(() =>
        {
            if (!Utility.IsBotValid(bot))
                return;

            bot.RemoveWeapons();
        });
    }

    private async Task GiveBotWeapon(CCSPlayerController bot, string mapName, CsItem? weapon = null)
    {
        // give bot weapon
        await Server.NextFrameAsync(() =>
        {
            if (!Utility.IsBotValid(bot))
                return;

            var botTeam = GetBotTeam(mapName);
            if (botTeam == CsTeam.None) return;
            if (weapon is null)
                bot.GiveNamedItem(botTeam == CsTeam.CounterTerrorist ? CsItem.M4A1 : CsItem.AK47);
            else
                bot.GiveNamedItem(weapon.Value);
        });
    }

    private async Task PreventBotPickupWeapon(CCSPlayerController bot)
    {
        // prevent pickup = true
        await Server.NextFrameAsync(() =>
        {
            if (!Utility.IsBotValid(bot))
                return;

            bot.PlayerPawn.Value!.WeaponServices!.PreventWeaponPickup = true;
        });
    }
}
