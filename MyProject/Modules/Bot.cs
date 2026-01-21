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
    private bool _isCurseActive = false;
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
            await Server.NextFrameAsync(() => Server.ExecuteCommand("css_cvar bot_stop 0"));
            await Server.NextFrameAsync(() => Server.ExecuteCommand("sv_cheats 0"));
        }

        var botTeam = GetBotTeam(mapName);
        if (botTeam != CsTeam.None)
        {
            SetDifficultyLevel(0, 0);
            await FillNormalBotAsync(_level, botTeam);
            SetMaxRespawnTimes();
        }
    }

    public async Task RoundStartBehavior(string mapName)
    {
        await SetBotMoneyToZero();
        var isBossRound = Main.Instance.RoundCount == Main.Instance.Config.MidBossRound ||
            Main.Instance.RoundCount == Main.Instance.Config.FinalBossRound;

        if (isBossRound)
            await HandleBossRound();
        else
            await HandleNormalRound();

        async Task SetBotWeapon(string botName, CsItem item)
        {
            var bot = Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(botName));
            if (!Utility.IsBotValidAndAlive(bot))
            {
                _logger.LogError("Bot {BotName} not found or invalid when setting weapon", botName);
                return;
            }

            try
            {
                await AllowBotPickupWeapon(bot);
                await RemoveBotWeapon(bot);
                await GiveBotWeapon(bot, mapName, item);
                await PreventBotPickupWeaponAfter3Seconds(bot);

                if (AppSettings.IsDebug)
                {
                    await VerifyBotWeapon(bot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set weapon for bot {BotName}", botName);
            }

            async Task VerifyBotWeapon(CCSPlayerController? bot)
            {
                await Server.NextFrameAsync(() =>
                {
                    if (Utility.IsBotValid(bot))
                    {
                        var currentWeapon = bot.PlayerPawn.Value!.WeaponServices?.ActiveWeapon.Value?.DesignerName;
                        if (!Utility.IsWeaponMatch(item, currentWeapon ?? ""))
                        {
                            _logger.LogWarning("Bot {BotName} weapon mismatch. Expected: {Expected}, Actual: {Actual}",
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
                    if (Utility.IsBotValidAndAlive(eagleEye))
                        Utility.SetClientModel(eagleEye, Main.Instance.Config.EagleEyeModel);
                    else
                        _logger.LogError("Special bot {botName} is invalid or not found when setting model", BotProfile.Special[0]);

                    var rush = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[2]));
                    if (Utility.IsBotValidAndAlive(rush))
                        Utility.SetClientModel(rush, Main.Instance.Config.RushModel);
                    else
                        _logger.LogError("Special bot {botName} is invalid or not found when setting model", BotProfile.Special[2]);

                    var mimic = Utilities.GetPlayerFromSlot(Main.Instance.GetPlayerSlot(BotProfile.Special[1]));
                    if (Utility.IsBotValidAndAlive(mimic))
                    {
                        var randomSkin = Utility.WorkshopSkins.ElementAt(Random.Shared.Next(Utility.WorkshopSkins.Count));
                        Utility.SetClientModel(mimic, randomSkin.Key);
                    }
                    else
                        _logger.LogError("Special bot {botName} is invalid or not found when setting model", BotProfile.Special[1]);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexcepted error when setting special bot model");
                }
            });
        }

        async Task SetSpecialBotAttribute()
        {
            await Server.NextFrameAsync(() =>
            {
                try
                {
                    var specialBots = new (string Name, int Score, int? Health)[]
                    {
                        (BotProfile.Special[0], 999, null),
                        (BotProfile.Special[1], 888, null),
                        (BotProfile.Special[2], 777, Main.Instance.RoundCount > 1 ? 500 : null)
                    };

                    foreach (var (name, score, health) in specialBots)
                    {
                        try
                        {
                            var slot = Main.Instance.GetPlayerSlot(name);
                            var bot = Utilities.GetPlayerFromSlot(slot);

                            if (!Utility.IsBotValid(bot))
                            {
                                _logger.LogError("Special bot {BotName} is invalid", name);
                                continue;
                            }

                            bot.Score = score;

                            if (health.HasValue)
                            {
                                bot.PlayerPawn.Value!.Health = health.Value;
                            }

                            if (AppSettings.IsDebug)
                                _logger.LogInformation("Set attributes for {BotName}: Score={Score}, Health={Health}", name, score, health);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to set attributes for {BotName}", name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in SetSpecialBotAttribute");
                }
            });
        }

        async Task HandleBossRound()
        {
            _respawnTimes = 0;
            var currentRound = Main.Instance.RoundCount;

            await SetBossHealth();
            await SetBossWeapon();
            await SetBossAmmo();

            async Task SetBossWeapon()
            {
                bool isBossValid = ValidateBoss(out var boss);
                if (!isBossValid)
                    return;

                try
                {
                    await SetBotWeapon(boss!.PlayerName, currentRound == Main.Instance.Config.MidBossRound ? CsItem.UMP45 : CsItem.M249);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Set mid boss weapon error");
                }
            }

            async Task SetBossAmmo()
            {
                await Server.NextWorldUpdateAsync(() =>
                {
                    bool isMidBossValid = ValidateBoss(out var boss);
                    if (!isMidBossValid)
                        return;

                    var weaponService = boss!.PlayerPawn.Value!.WeaponServices;

                    if (weaponService == null)
                    {
                        _logger.LogError("Boss weapon service is null during setting boss weapon ammo");
                        return;
                    }

                    var bossWeapon = weaponService.MyWeapons
                        .FirstOrDefault(weapon =>
                            weapon is not null &&
                            weapon.IsValid &&
                            weapon.Value is not null &&
                            weapon.Value.IsValid &&
                            weapon.Value.DesignerName == Utility.GetActualWeaponName(currentRound == Main.Instance.Config.MidBossRound ? CsItem.UMP45 : CsItem.M249));

                    if (bossWeapon == null)
                    {
                        _logger.LogError("Cannot find the boss weapon");
                        return;
                    }

                    try
                    {
                        var weaponBase = bossWeapon.Value!.As<CCSWeaponBase>();

                        if (!Utility.IsWeaponBaseValid(weaponBase))
                        {
                            _logger.LogError("Converted weapon base is invalid");
                            return;
                        }

                        const int ammo = 999;
                        const int reservedAmmo = 0;

                        Utility.SetAmmoAmount(weaponBase, ammo);
                        Utility.SetReservedAmmoAmount(weaponBase, reservedAmmo);

                        if (AppSettings.IsDebug)
                            _logger.LogInformation("Set boss weapon ammo successfully. ammo: {ammo}, reservedAmmo: {reservedAmmo}", ammo, reservedAmmo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to convert or set weapon ammo");
                    }
                });
            }

            bool ValidateBoss(out CCSPlayerController? boss)
            {
                boss = currentRound == Main.Instance.Config.MidBossRound ?
                    Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(BotProfile.Boss[0])) :
                    Utilities.GetPlayers().FirstOrDefault(player => player.PlayerName.Contains(BotProfile.Boss[1]));

                if (!Utility.IsBotValid(boss))
                {
                    var bossType = currentRound == Main.Instance.Config.MidBossRound ? "Mid Boss" : "Final Boss";
                    _logger.LogError("Spawn {BossType} failed at round {RoundCount}", bossType, currentRound);
                    return false;
                }

                return true;
            }

            async Task SetBossHealth()
            {
                await Server.NextFrameAsync(() =>
                {
                    bool isBossValid = ValidateBoss(out var boss);
                    if (!isBossValid)
                        return;

                    boss!.PlayerPawn.Value!.Health = currentRound == Main.Instance.Config.MidBossRound ?
                        Main.Instance.Config.MidBossHealth :
                        Main.Instance.Config.FinalBossHealth;
                });
            }
        }

        async Task HandleNormalRound()
        {
            await SetSpecialBotAttribute();
            await SetSpecialBotModel();

            _respawnTimes = _maxRespawnTimes;

            if (Main.Instance.RoundCount > 1)
            {
                await SetBotWeapon(BotProfile.Special[0], CsItem.AWP); // "[ELITE]EagleEye"
                await SetBotWeapon(BotProfile.Special[1], CsItem.M4A1S); // "[ELITE]mimic"
                await SetBotWeapon(BotProfile.Special[2], CsItem.P90); // "[EXPERT]Rush" 

                foreach (var bot in Utilities.GetPlayers().Where(player => player.IsBot))
                {
                    if (!Utility.IsBotValid(bot)) continue;

                    await PreventBotPickupWeaponAfter3Seconds(bot);
                }
            }
        }
    }

    public async Task RoundEndBehavior(int winStreak, int looseStreak, string mapName)
    {
        var botTeam = GetBotTeam(mapName);

        if (Main.Instance.RoundCount > 0)
        {
            await SetDefaultWeapon();
            await AddSpecialOrBoss(botTeam);
            await KickNormalBotAsync();
        }

        if (Main.Instance.RoundCount != Main.Instance.Config.MidBossRound && Main.Instance.RoundCount < Main.Instance.Config.FinalBossRound)
        {
            SetDifficultyLevel(winStreak, looseStreak);
            await FillNormalBotAsync(_level, botTeam);
            SetMaxRespawnTimes();
        }

        async Task SetDefaultWeapon()
        {
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
                await PreventBotPickupWeaponAfter3Seconds(bot);

                if (AppSettings.IsDebug)
                {
                    await Server.NextFrameAsync(() =>
                    {
                        Main.Instance.AddTimer(1f, () =>
                        {
                            if (Utility.IsBotValid(bot))
                            {
                                var botTeam = GetBotTeam(mapName);
                                var expectedWeapon = Utility.GetCsItemEnumValue(botTeam == CsTeam.CounterTerrorist ? CsItem.M4A1 : CsItem.AK47);

                                if (bot.PlayerPawn.Value!.WeaponServices is null)
                                {
                                    _logger.LogError("Bot {botName} Weapon Service is null after respawning", bot.PlayerName);
                                    return;
                                }

                                if (!bot.PlayerPawn.Value.WeaponServices.MyWeapons.Any(w => w.IsValid && w.Value!.DesignerName == expectedWeapon))
                                {
                                    _logger.LogError("Respawn bot {BotName} lost {Expected}", bot.PlayerName, expectedWeapon);
                                }
                            }
                        });
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
        if (AppSettings.IsDebug)
            _logger.LogInformation("BossBehavior Start");
        if (!Utility.IsBotValidAndAlive(boss))
            return;
        var activeAbilityChance = GetChance();

        if (activeAbilityChance <= Main.Instance.Config.BossActiveAbilityChance)
        {
            var abilityChoice = Random.Shared.Next(1, 7);

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
                    if (!_isCurseActive)
                        Cursed();
                    break;
            }
        }

        double GetChance()
        {
            return Random.Shared.NextDouble() * 100; // Returns a value between 0-100
        }

        void FireTorture()
        {
            if (AppSettings.IsDebug)
                _logger.LogInformation("Boss actives FireTorture");
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
            if (AppSettings.IsDebug)
                _logger.LogInformation("Boss actives Freeze");
            Utility.PrintToAllCenter("The Boss locks the battlefield in ice!");
            var humanPlayers = Utility.GetAliveHumans();

            if (humanPlayers.Count == 0) return;

            var frozenPlayers = new List<CCSPlayerController>();

            Server.NextFrame(() =>
            {
                foreach (var player in humanPlayers)
                {
                    if (!Utility.IsHumanValidAndAlive(player)) continue;

                    player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_NONE;
                    ApplyScreenOverlay(player.PlayerPawn.Value, 3f);
                    frozenPlayers.Add(player);
                }
            });

            Main.Instance.AddTimer(2f, () =>
            {
                Server.NextFrame(() =>
                {
                    foreach (var player in frozenPlayers)
                    {
                        if (!Utility.IsHumanValidAndAlive(player)) continue;

                        player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_WALK;
                    }
                });
            });
        }

        void Flashbang()
        {
            if (AppSettings.IsDebug)
                _logger.LogInformation("Boss actives Flashbang");
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
            if (AppSettings.IsDebug)
                _logger.LogInformation("Boss actives Explosion");
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
            if (AppSettings.IsDebug)
                _logger.LogInformation("Boss actives ToxicSmoke");
            Utility.PrintToAllCenter("The Boss releases toxic clouds!");
            var humanPlayers = Utility.GetAliveHumans();
            if (humanPlayers.Count == 0)
                return;

            var targetCount = Math.Max(1, humanPlayers.Count / 3);
            var selectedPlayers = humanPlayers
                .OrderBy(x => Random.Shared.Next())
                .Take(targetCount)
                .ToList();

            var markedPositions = new List<Vector>();

            Server.NextFrame(() =>
            {
                foreach (var player in selectedPlayers)
                {
                    if (!Utility.IsHumanValidAndAlive(player))
                        continue;

                    var playerPosition = player.PlayerPawn.Value!.AbsOrigin;
                    if (playerPosition is null)
                        continue;

                    var markedPosition = new Vector(
                        playerPosition.X,
                        playerPosition.Y,
                        playerPosition.Z
                    );

                    if (AppSettings.IsDebug)
                        _logger.LogInformation("Toxic Smoke position X: {X} Y: {Y} Z: {Z}", markedPosition.X, markedPosition.Y, markedPosition.Z);

                    markedPositions.Add(markedPosition);

                    Utility.DrawBeaconOnPlayer(player, System.Drawing.Color.Green, 100.0f, 15.0f, 2.0f);
                }
            });

            Main.Instance.AddTimer(1.0f, () =>
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
                if (AppSettings.IsDebug)
                    _logger.LogInformation("Create Toxic Smoke damage zone");
                const float damageRadius = 150.0f;
                const int damagePerSecond = 5;
                const float smokeDuration = 15.0f;

                var startTime = Server.CurrentTime;

                var toxicTimer = Main.Instance.AddTimer(1f, () =>
                {
                    var currentTime = Server.CurrentTime;
                    if (currentTime - startTime >= smokeDuration)
                        return;

                    var humanPlayers = Utility.GetAliveHumans();

                    foreach (var player in humanPlayers)
                    {
                        if (!Utility.IsHumanValidAndAlive(player))
                            continue;

                        var playerPosition = player.PlayerPawn.Value!.AbsOrigin;
                        if (playerPosition is null)
                            continue;

                        var distance = CalculateDistance(playerPosition, smokePosition);

                        if (distance <= damageRadius)
                        {
                            if (AppSettings.IsDebug)
                                _logger.LogInformation("{playerName} enters in Toxic Smoke", player.PlayerName);
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

                Main.Instance.AddTimer(smokeDuration, () =>
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
            if (AppSettings.IsDebug)
                _logger.LogInformation("Boss actives Cursed");
            var bossHealth = boss.PlayerPawn.Value!.Health;
            var maxHealth = IsBoss(boss) && boss.PlayerName.Contains(BotProfile.Boss[0]) ? Main.Instance.Config.MidBossHealth : Main.Instance.Config.FinalBossHealth;
            var oneThirdHealth = maxHealth / 3;

            if (bossHealth > oneThirdHealth)
                return;

            _isCurseActive = true;

            var humanPlayers = Utility.GetAliveHumans();

            if (humanPlayers.Count == 0)
            {
                _isCurseActive = false;
                return;
            }

            Utility.PrintToAllCenter("The Boss casts a deadly curse upon all!");

            const int curseDamage = 2;
            const float curseDuration = 10.0f;
            var startTime = Server.CurrentTime;

            // Add purple beacon effect to all players to indicate curse
            Server.NextFrame(() =>
            {
                foreach (var player in humanPlayers)
                {
                    if (!Utility.IsHumanValidAndAlive(player)) continue;

                    // Use purple beacon to mark cursed players
                    Utility.DrawBeaconOnPlayer(player, System.Drawing.Color.Purple, 100.0f, curseDuration, 1.0f);
                }
            });

            // Create curse damage timer
            var cursedTimer = Main.Instance.AddTimer(1f, () =>
            {
                var currentTime = Server.CurrentTime;
                if (currentTime - startTime > curseDuration)
                    return;

                var alivePlayers = Utility.GetAliveHumans();

                foreach (var player in alivePlayers)
                {
                    if (!Utility.IsHumanValidAndAlive(player))
                        continue;

                    try
                    {
                        Utility.SlapPlayer(player, curseDamage, true);

                        player.PrintToCenter($"Cursed: -{curseDamage} HP");

                        var color = Color.FromArgb(102, 193, 45, 45);
                        Utility.ColorScreen(player, color, 0.3f, 0.2f);
                    }
                    catch (ArgumentException ex)
                    {
                        if (AppSettings.IsDebug)
                            _logger.LogWarning("Slap Error, {ExceptionMessage}, playerName {playerName}", ex.Message, player.PlayerName);
                        // Player is dead or invalid, ignore SlapPlayer error
                    }
                }
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

            _damageTimers.Add(cursedTimer);

            Main.Instance.AddTimer(curseDuration, () =>
            {
                _damageTimers.Remove(cursedTimer);
                cursedTimer?.Kill();
                _isCurseActive = false;
                Utility.PrintToAllCenter("The curse has been lifted...");
            });
        }

        void CreateTimedProjectileAttack(string message, System.Drawing.Color beaconColor, Action<Vector> createProjectileAction, float delayTime = 3.0f)
        {
            if (AppSettings.IsDebug)
                _logger.LogInformation("Marked projectile position and draw beacon, {projectAction}", createProjectileAction.Method.Name);
            Utility.PrintToAllCenter(message);
            var humanPlayers = Utility.GetAliveHumans();
            if (humanPlayers.Count == 0)
                return;

            var markedPositions = new List<Vector>();

            Server.NextFrame(() =>
            {
                foreach (var player in humanPlayers)
                {
                    if (!Utility.IsHumanValidAndAlive(player)) // check player is valid at next frame
                        continue;

                    var playerPosition = player.PlayerPawn.Value!.AbsOrigin;
                    if (playerPosition is null)
                        continue;

                    var markedPosition = new Vector(
                        playerPosition.X,
                        playerPosition.Y,
                        playerPosition.Z
                    );
                    if (AppSettings.IsDebug)
                        _logger.LogInformation("Projectile position X: {X}, Y: {Y}, Z: {Z}", markedPosition.X, markedPosition.Y, markedPosition.Z);
                    markedPositions.Add(markedPosition);

                    var beaconDuration = (beaconColor == System.Drawing.Color.Green) ? 15.0f : 6.0f;
                    Utility.DrawBeaconOnPlayer(player, beaconColor, 100.0f, beaconDuration, 2.0f);
                }
            });

            Main.Instance.AddTimer(delayTime, () =>
            {
                foreach (var position in markedPositions)
                {
                    createProjectileAction(position);
                }
            });
        }

        void CreateProjectileAtPosition<T>(Vector position, CCSPlayerController attacker, float cleanupTime = 3.0f) where T : CBaseCSGrenadeProjectile
        {
            if (AppSettings.IsDebug)
                _logger.LogInformation("Create projectile {projectType}", typeof(T).Name);
            // Early check: ensure attacker is valid before creating projectile
            if (!Utility.IsPlayerValidAndAlive(attacker))
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
            if (Utility.IsPlayerValidAndAlive(attacker) &&
                attacker.PlayerPawn.Value!.IsValid)
            {
                if (AppSettings.IsDebug)
                {
                    _logger.LogInformation("Projectile attacker name: {name}", attacker.PlayerName);
                }
                projectile.TeamNum = attacker.TeamNum;
                projectile.Thrower.Raw = attacker.PlayerPawn.Raw;
                projectile.OriginalThrower.Raw = attacker.PlayerPawn.Raw;
                projectile.OwnerEntity.Raw = attacker.PlayerPawn.Raw;
            }
            else if (AppSettings.IsDebug)
                _logger.LogWarning("Projectile attacker is invaild");
            // If attacker becomes invalid, projectile will have no owner
            // Players can still be damaged, but kills will count as environmental/suicide

            if (projectile is CSmokeGrenadeProjectile smokeProjectile)
            {
                smokeProjectile.SmokeColor.X = 0;
                smokeProjectile.SmokeColor.Y = 255;
                smokeProjectile.SmokeColor.Z = 0;
            }
        }
    }

    public void ClearDamageTimer()
    {
        foreach (var timer in _damageTimers)
        {
            timer?.Kill();
        }

        _damageTimers.Clear();
        _isCurseActive = false;
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
                if (AppSettings.IsDebug)
                    _logger.LogWarning("Bot team not found. {mapName}", mapName);
                return CsTeam.CounterTerrorist;
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

    private void SetDifficultyLevel(int winStreak, int looseStreak)
    {
        if (winStreak > 1 && _level < 7)
            _level++;
        else if (looseStreak > 2 && _level > 1)
            _level--;
    }

    private void SetMaxRespawnTimes()
    {
        _maxRespawnTimes = (_level < 3) ? 100 : (_level == 4) ? 120 : 150;
    }

    private async Task AddSpecialOrBoss(CsTeam botTeam)
    {
        var team = botTeam == CsTeam.CounterTerrorist ? "ct" : "t";
        if (AppSettings.IsDebug)
            Server.PrintToChatAll($"AddSpecialOrBoss Next Round: {Main.Instance.RoundCount}");
        if (Main.Instance.RoundCount == Main.Instance.Config.MidBossRound)
        {
            if (AppSettings.IsDebug)
                Server.PrintToChatAll($"AddSpecialOrBoss spawn Mid Boss");
            var midBossSpawn = Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Boss[0]) == 1;
            if (!midBossSpawn)
            {
                Main.Instance.AddTimer(1f, async () =>
                {
                    KickBot();
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
            if (AppSettings.IsDebug)
                Server.PrintToChatAll($"AddSpecialOrBoss spawn Final Boss");
            var finalBossSpawn = Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Boss[1]) == 1;
            if (!finalBossSpawn)
            {
                Main.Instance.AddTimer(1f, async () =>
                {
                    KickBot();
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
            if (AppSettings.IsDebug)
                Server.PrintToChatAll($"AddSpecialOrBoss spawn Special");
            var specialBotSpawn = Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Special[0]) == 1 &&
                Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Special[1]) == 1 &&
                Utilities.GetPlayers().Count(player => player.PlayerName == BotProfile.Special[2]) == 1;

            if (!specialBotSpawn)
            {
                await Server.NextWorldUpdateAsync(() =>
                {
                    KickBot();
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

    private static void KickBot()
    {
        Server.ExecuteCommand("bot_kick");
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

            Main.Instance.AddTimer(0.01f, () =>
            {
                if (pawn == null || !pawn.IsValid) return;
                pawn.HealthShotBoostExpirationTime = future;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
            });

            if (attempt < 3)
            {
                Main.Instance.AddTimer(0.15f, () =>
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

            if (bot.PlayerPawn.Value!.WeaponServices is null)
                throw new NullReferenceException("Bot Weapon Service is null, cannot allow bot pickup weapon");

            bot.PlayerPawn.Value!.WeaponServices.PreventWeaponPickup = false;
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

            var weaponToGive = weapon ?? (botTeam == CsTeam.CounterTerrorist ? CsItem.M4A1 : CsItem.AK47);

            if (bot.PlayerPawn.Value!.WeaponServices == null)
                throw new NullReferenceException("Bot Weapon Service is null, cannot give bot weapon");

            bot.GiveNamedItem(weaponToGive);
        });
    }

    private async Task PreventBotPickupWeaponAfter3Seconds(CCSPlayerController bot)
    {
        // prevent pickup = true
        await Server.NextFrameAsync(() =>
        {
            Main.Instance.AddTimer(3f, () =>
            {
                if (!Utility.IsBotValid(bot))
                    return;

                if (bot.PlayerPawn.Value!.WeaponServices is null)
                {
                    _logger.LogError("Bot {BotName} WeaponServices is null, cannot schedule prevent pickup", bot.PlayerName);
                    return;
                }

                bot.PlayerPawn.Value!.WeaponServices.PreventWeaponPickup = true;
            });
        });
    }
}
