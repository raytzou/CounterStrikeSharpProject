using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CS2MenuManager.API.Menu;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Domains;
using MyProject.Models;
using MyProject.Modules.Interfaces;
using MyProject.Services.Interfaces;
using System.Reflection;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace MyProject.Modules;

public class Command(
    ILogger<Command> logger,
    IPlayerService playerService,
    IMusic music,
    IBot bot
    ) : ICommand
{
    private readonly ILogger<Command> _logger = logger;
    private readonly IPlayerService _playerService = playerService;
    private readonly IMusic _music = music;
    private readonly IBot _bot = bot;

    private readonly Dictionary<string, CommandMetadata> _registeredCommands = [];

    public IReadOnlyDictionary<string, CommandMetadata> AllCommands => _registeredCommands;

    public void RegisterCommands()
    {
        var mainType = typeof(Main);
        var methods = mainType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var consoleCommandAttrs = method.GetCustomAttributes<ConsoleCommandAttribute>();
            var requiresPermissionAttr = method.GetCustomAttribute<RequiresPermissions>();

            foreach (var cmdAttr in consoleCommandAttrs)
            {
                _registeredCommands[cmdAttr.Command] = new CommandMetadata
                {
                    Name = cmdAttr.Command,
                    Description = cmdAttr.Description,
                    Permissions = requiresPermissionAttr?.Permissions.ToArray()
                };

                if (AppSettings.IsDebug)
                    _logger.LogInformation("Registered command: {CommandName} (Permission: {Permission})",
                        cmdAttr.Command, string.Join(",", requiresPermissionAttr?.Permissions ?? []) ?? "none");
            }
        }

        _logger.LogInformation("Successfully registered {Count} commands", _registeredCommands.Count);
    }

    public IEnumerable<CommandMetadata> GetAvailableCommandsForPlayer(CCSPlayerController player) =>
        _registeredCommands.Values.Where(cmd =>
            cmd.Permissions == null || AdminManager.PlayerHasPermissions(player, cmd.Permissions));

    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Usage: css_kick <target>");
            return;
        }

        var targetName = Main.Instance.GetTargetNameByKeyword(command.GetArg(1));

        if (string.IsNullOrEmpty(targetName))
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Target not found.");
            return;
        }

        Server.ExecuteCommand($"kick {targetName}");
        ReplyToCommandWithTeamColor(client, command, "[css] You kick {targetName}");
        _logger.LogInformation("[css] {admin} kicked {targetName} at {DT}", client.PlayerName, targetName, DateTime.Now);
        Utility.PrintToChatAllWithColor($"Admin kicked {targetName}");
    }

    public void OnInfoCommand(CCSPlayerController client, CommandInfo command, int botRespawnRemaining)
    {
        command.ReplyToCommand($" {ChatColors.Grey}----- {ChatColors.Purple}{ConVar.Find("hostname")!.StringValue} {ChatColors.Grey}-----");
        ReplyToCommandWithTeamColor(client, command, $"Server local time: {ChatColors.Red}{DateTime.Now}");
        ReplyToCommandWithTeamColor(client, command, $"Current map: {ChatColors.Lime}{Server.MapName}");
        ReplyToCommandWithTeamColor(client, command, $"Player: {ChatColors.Yellow}{Main.Instance.PlayerCount}{ChatColors.Grey}/{Server.MaxPlayers}");
        ReplyToCommandWithTeamColor(client, command, $"Round: {ChatColors.Yellow}{Main.Instance.RoundCount}{ChatColors.Grey}/{ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>() - 1}");
        ReplyToCommandWithTeamColor(client, command, $"Bot respawn remaining: {ChatColors.Green}{botRespawnRemaining}");
        command.ReplyToCommand($" {ChatColors.Grey}----------");
    }

    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command, float changeMapTimeBuffer)
    {
        if (command.ArgCount < 2)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Usage: css_map <map name>");
            return;
        }

        var mapName = command.GetArg(1);

        if (!Utility.AllMaps.Contains(mapName))
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] Map not found: {mapName}");
            return;
        }

        _logger.LogInformation("{admin} changed map to {mapName} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, mapName, DateTime.Now);
        Utility.PrintToChatAllWithColor($"Admin changed map to {mapName}");

        Utility.AddTimer(changeMapTimeBuffer, () =>
        {
            if (Utility.MapsFromWorkshop.Contains(mapName))
                Server.ExecuteCommand($"ds_workshop_changelevel {mapName}");
            else if (Utility.MapsInPhysicalDirectory.Contains(mapName))
                Server.ExecuteCommand($"changelevel {mapName}");
        });
    }

    public void OnMapsCommand(CCSPlayerController client, CommandInfo command)
    {
        foreach (var map in Utility.AllMaps)
        {
            ReplyToCommandWithTeamColor(client, command, map);
        }
    }

    public void OnCvarCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Usage: css_cvar <ConVar> <Value>");
            return;
        }

        var cvar = ConVar.Find(command.GetArg(1));

        if (cvar is null)
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] Cannot find the ConVar: {command.GetArg(1)}");
            return;
        }
        else if (command.ArgCount == 2)
        {
            switch (cvar.Type)
            {
                case ConVarType.Int16:
                case ConVarType.Int32:
                case ConVarType.Int64:
                case ConVarType.UInt16:
                case ConVarType.UInt32:
                case ConVarType.UInt64:
                    ReplyToCommandWithTeamColor(client, command, $"{cvar.Name}: {cvar.GetPrimitiveValue<int>()}");
                    break;
                case ConVarType.Float32:
                case ConVarType.Float64:
                    ReplyToCommandWithTeamColor(client, command, $"{cvar.Name}: {cvar.GetPrimitiveValue<float>()}");
                    break;
                case ConVarType.Bool:
                    ReplyToCommandWithTeamColor(client, command, $"{cvar.Name}: {cvar.GetPrimitiveValue<bool>()}");
                    break;
                default:
                    ReplyToCommandWithTeamColor(client, command, $"[css] ConVar: {cvar.Name}, type: {cvar.Type}");
                    break;
            }

            return;
        }

        string value = string.Empty;

        switch (cvar.Type)
        {
            case ConVarType.Int16:
            case ConVarType.Int32:
            case ConVarType.Int64:
            case ConVarType.UInt16:
            case ConVarType.UInt32:
            case ConVarType.UInt64:
                if (int.TryParse(command.GetArg(2), out int parseInt))
                {
                    cvar.SetValue(parseInt);
                    value = parseInt.ToString();
                }
                else
                {
                    ReplyToCommandWithTeamColor(client, command, "[css] Value type error!");
                    return;
                }
                break;
            case ConVarType.Float32:
            case ConVarType.Float64:
                if (float.TryParse(command.GetArg(2), out float parseFloat))
                {
                    cvar.SetValue(parseFloat);
                    value = parseFloat.ToString();
                }
                else
                {
                    ReplyToCommandWithTeamColor(client, command, "[css] Value type error!");
                    return;
                }
                break;
            case ConVarType.Bool:
                if (command.GetArg(2) == "0")
                {
                    cvar.SetValue(false);
                    value = "false";
                }
                else if (command.GetArg(2) == "1")
                {
                    cvar.SetValue(true);
                    value = "true";
                }
                else
                {
                    string arg = command.GetArg(2).ToLower();

                    if (arg == "false")
                    {
                        cvar.SetValue(false);
                        value = "false";
                    }
                    else if (arg == "true")
                    {
                        cvar.SetValue(true);
                        value = "true";
                    }
                    else
                    {
                        ReplyToCommandWithTeamColor(client, command, "[css] Value type error!");
                        return;
                    }
                }
                break;
            default:
                try
                {
                    cvar.SetValue($"{command.GetArg(2)}");
                }
                catch (Exception ex)
                {
                    _logger.LogError("{ex}", ex.Message);
                    //bot_stop 1
                    //Reference types must be accessed using `GetReferenceValue`
                    _logger.LogError("cvar: {name}, type: {type}, arg: {arg}", cvar.Name, cvar.Type, command.GetArg(2));
                    return;
                }
                break;
        }

        if (!string.IsNullOrEmpty(value))
        {
            Utility.PrintToChatAllWithColor($"{cvar.Name} changed to {value}");
            _logger.LogInformation("{admin} changed {cvar} to {value} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, cvar.Name, value, DateTime.Now);
        }
    }

    public void OnRconCommand(CCSPlayerController? client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] Usage: css_rcon <rcon>");
            return;
        }

        var rcon = string.Join(" ", command.GetCommandString.Split(' ').Skip(1));
        var admin = client?.PlayerName ?? "console";

        try
        {
            Server.ExecuteCommand(rcon);
            _logger.LogInformation("{admin} executed RCON command '{rcon}' at {DT}", admin, rcon, DateTime.Now);
        }
        catch (Exception ex)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Failed to execute RCON command");
            _logger.LogError("RCON command execution failed. Admin: {admin}, Command: {rcon}, Error: {error}",
            admin, rcon, ex.Message);
        }
    }

    public void OnPlayersCommand(CCSPlayerController client, CommandInfo command)
    {
        var players = Utilities.GetPlayers();
        if (players.Count == 0)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] There is no any player");
            return;
        }

        foreach (var player in players)
        {
            ReplyToCommandWithTeamColor(client, command, player.PlayerName);
        }
    }

    public void OnSlayCommand(CCSPlayerController client, CommandInfo command)
    {
        ExecutePlayerCommand(
            client,
            command,
            minArgCount: 2,
            usageMessage: "[css] Usage: css_slay <target>",
            commandName: "slay",
            pastTenseVerb: "slew",
            playerAction: player => player.CommitSuicide(true, true)
        );
    }

    public void OnGodCommand(CCSPlayerController client, CommandInfo command)
    {
        if (client is null) return;
        var takeDamage = client.PlayerPawn.Value!.TakesDamage;

        if (takeDamage)
            ReplyToCommandWithTeamColor(client, command, "[css] God mode on");
        else
            ReplyToCommandWithTeamColor(client, command, "[css] God mode off");

        client.PlayerPawn.Value.TakesDamage = !takeDamage;
    }

    public void OnReviveCommand(CCSPlayerController client, CommandInfo command, Position position, WeaponStatus weaponStatus)
    {
        var reviveCost = Main.Instance?.Config?.CostScoreToRevive;

        if (Main.Instance is null || reviveCost is null)
        {
            _logger.LogError("Singleton instance or Config instance is null");
            return;
        }

        if (client is null) return;

        if (client.Team == CounterStrikeSharp.API.Modules.Utils.CsTeam.Spectator)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] You are in spectator mode, cannot revive.");
            return;
        }

        if (client.PawnIsAlive)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] You are alive.");
            return;
        }

        if (client.Score - reviveCost < 0 && !AppSettings.IsDebug)
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] You don't have enough score ({reviveCost}) to revive.");
            return;
        }

        if (position.Origin is null || position.Rotation is null || position.Velocity is null)
        {
            var humanTeam = Main.Instance.GetHumanTeam();
            var humanSpawnPoint = new SpawnPoint(0);

            if (humanTeam == CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist)
            {
                humanSpawnPoint = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").First();
            }
            else if (humanTeam == CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist)
            {
                humanSpawnPoint = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist").First();
            }

            position.Origin = humanSpawnPoint.AbsOrigin!;
            position.Rotation = humanSpawnPoint.AbsRotation!;
            position.Velocity = humanSpawnPoint.AbsVelocity!;
        }

        if (AppSettings.IsDebug)
            command.ReplyToCommand($"X: {position.Origin.X} Y: {position.Origin.Y} Z: {position.Origin.Z}");
        else
            client.Score -= 50;

        float time = 1f;
        CounterStrikeSharp.API.Modules.Timers.Timer? timer = null;

        timer = Utility.AddTimer(time, () =>
        {
            ReviveCallBack(ref time, client, position, timer, weaponStatus);
        }, TimerFlags.REPEAT);
    }

    public void OnDebugCommand(CCSPlayerController client, CommandInfo command, Dictionary<string, WeaponStatus> weaponStatus)
    {
        if (!AppSettings.IsDebug)
        {
            command.ReplyToCommand("debug command is available only in debug mode");
            return;
        }

        if (client is null || client.PlayerPawn.Value is null)
            return;

        var weaponServices = client.PlayerPawn.Value.WeaponServices;
        if (weaponServices is null)
        {
            command.ReplyToCommand($"weapon services is null");
            return;
        }

        var weaponServiceValue = weaponServices.ActiveWeapon.Value;
        if (weaponServiceValue is null)
            command.ReplyToCommand($"weapon service value is null");
        else
            command.ReplyToCommand($"Weapon: {weaponServiceValue.DesignerName}");

        foreach (var weapon in weaponServices.MyWeapons)
        {
            if (weapon.Value is not null)
                command.ReplyToCommand($"my weapons: {weapon.Value.DesignerName}");
        }

        if (weaponStatus is null)
        {
            command.ReplyToCommand("weaponStatus is null");
            return;
        }

        foreach (var weapon in weaponStatus[client.PlayerName].Weapons)
        {
            command.ReplyToCommand($"weapon cache: {weapon}");
        }
    }

    public void OnModelsCommand(CCSPlayerController client, CommandInfo command, Main thePlugin)
    {
        var menu = new WasdMenu("Select Models", thePlugin);
        var playerCache = _playerService.GetPlayerCache(client.SteamID);

        if (playerCache is null)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Cannot open model menu, please reconnect to server!");
            _logger.LogWarning("Cannot open model menu, player cache is not found. SteamID: {steamID}", client.SteamID);
            return;
        }

        menu.AddItem("Default", (player, option) =>
        {
            var defaultSkin = playerCache.DefaultSkinModelPath;
            Utility.SetClientModel(player, defaultSkin);
            _playerService.ResetPlayerSkinFromCache(playerCache);
        });
        foreach (var skin in Utility.WorkshopSkins)
        {
            menu.AddItem(skin.Key, (player, option) =>
            {
                Utility.SetClientModel(player, skin.Key);
                _playerService.ResetPlayerSkinFromCache(playerCache);
                var playerSkin = playerCache.PlayerSkins.FirstOrDefault(cacheSkin => cacheSkin.SkinName == skin.Key);
                if (playerSkin is not null)
                    playerSkin.IsActive = true;
                else
                {
                    var newCacheSkin = new PlayerSkin
                    {
                        SteamId = player.SteamID,
                        SkinName = skin.Key,
                        AcquiredAt = DateTime.Now,
                        IsActive = true,
                        ExpiresAt = null
                    };
                    playerCache.PlayerSkins.Add(newCacheSkin);
                    _playerService.UpdateCache(playerCache);
                }
            });
        }

        menu.Display(client, Main.Instance.Config.DisplayMenuInterval);
    }

    public void OnSlapCommand(CCSPlayerController client, CommandInfo command)
    {
        // Validate slap-specific damage parameter
        if (command.ArgCount >= 2 && !string.IsNullOrWhiteSpace(command.GetArg(2)))
        {
            var dmgString = command.GetArg(2);
            if (!int.TryParse(dmgString, out var damage) || damage < 0)
            {
                ReplyToCommandWithTeamColor(client, command, $"[css] {dmgString} is invalid amount.");
                return;
            }
        }

        ExecutePlayerCommand(
            client,
            command,
            minArgCount: 1,
            maxArgCount: 3,
            usageMessage: "[css] Usage: css_slap <target> [damage]",
            commandName: "slap",
            pastTenseVerb: "slapped",
            playerAction: player =>
            {
                var dmgString = string.IsNullOrWhiteSpace(command.GetArg(2)) ? "0" : command.GetArg(2);
                int.TryParse(dmgString, out var damage);
                Utility.SlapPlayer(player, damage, true);
            },
            customSingleMessage: targetName =>
            {
                var dmgString = command.ArgCount >= 2 ? command.GetArg(2) : "0";
                int.TryParse(dmgString, out var damage);
                return $"[css] You slapped {targetName} {damage} hp.";
            },
            customBroadcastMessage: targetName =>
            {
                var dmgString = command.ArgCount >= 2 ? command.GetArg(2) : "0";
                int.TryParse(dmgString, out var damage);
                return string.IsNullOrEmpty(targetName)
                    ? "Admin slapped all players"
                    : $"Admin slapped {targetName} {damage} hp";
            }
        );
    }

    public void OnVolumeCommand(CCSPlayerController client, CommandInfo command)
    {
        if (!TryValidateVolumeCommand(
            client,
            command,
            "css_volume",
            VolumeType.Music,
            out var playerCache,
            out var
            volume))
            return;

        // Update player's sound volume immediately
        Server.NextWorldUpdate(() =>
        {
            if (!Utility.IsHumanPlayerValid(client))
                return;

            playerCache.Volume = volume;
            _playerService.UpdateCache(playerCache);
            ReplyToCommandWithTeamColor(client, command, $"[css] Volume set to {volume}%");

            var soundEventId = _music.GetPlayingRoundSoundID(client.Slot);

            if (soundEventId != null)
                Utility.SendSoundEventPackage(client, soundEventId.Value, volume / 100f);
        });
    }

    public void OnSaySoundVolumeCommand(CCSPlayerController client, CommandInfo command)
    {
        if (!TryValidateVolumeCommand(
            client,
            command,
            "css_ss_volume",
            VolumeType.SaySound,
            out var playerCache,
            out var volume))
            return;

        playerCache.SaySoundVolume = volume;
        _playerService.UpdateCache(playerCache);
        ReplyToCommandWithTeamColor(client, command, $"[css] Volume set to {volume}%");
    }

    public void OnBuyCommand(CCSPlayerController client, CommandInfo command, Main thePlugin)
    {
        if (!Utility.IsHumanPlayerValid(client)) return;

        var buyTime = ConVar.Find("mp_buytime")!.GetPrimitiveValue<float>();
        if (Main.Instance.RoundSecond >= buyTime)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] buying time is over!");
            return;
        }

        if (!CanBuy())
        {
            ReplyToCommandWithTeamColor(client, command, "[css] You can't buy anything right now");
            return;
        }

        if (command.ArgCount < 2)
        {
            OpenBuyMenu();
            return;
        }

        var buyTarget = command.GetArg(1).ToLower();
        var weaponSet = Utility.WeaponMenu
            .Select(weapon => weapon.EntityName);
        var exactMatch = weaponSet
            .FirstOrDefault(w => w.Equals(buyTarget, StringComparison.OrdinalIgnoreCase));
        var fuzzyMatches = weaponSet
            .Where(w => w.Contains(buyTarget, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var targetWeapon = Utility.ResolveWeaponEntity(buyTarget);
        if (targetWeapon == null)
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] Invalid weapon: {buyTarget}");
            return;
        }

        var targetPrice = Utility.WeaponMenu
                .First(weapon => weapon.EntityName == targetWeapon)
                .Price;
        if (!IsMoneyEnough(client, targetPrice))
        {
            ReplyToCommandWithTeamColor(client, command, "[css] You don't have enough money to buy the item!");
            return;
        }

        BuyItem(client, targetWeapon);

        void OpenBuyMenu()
        {
            var menu = new WasdMenu("Buy Menu", thePlugin);

            menu.AddItem("Close Menu", (player, option) =>
            {
                return;
            });

            foreach (var (EntityName, DisplayName, Price) in Utility.WeaponMenu)
            {
                menu.AddItem($"{DisplayName} ${Price}", (player, option) =>
                {
                    if (!IsMoneyEnough(player, Price))
                    {
                        Utility.PrintToChatWithTeamColor(player, "[css] You don't have enough money to buy the item!");
                    }
                    else
                    {
                        BuyItem(player, EntityName);
                    }
                });
            }

            menu.Display(client, Main.Instance.Config.DisplayMenuInterval);
        }

        bool CanBuy()
        {
            var maxRound = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

            return client.PlayerPawn.Value!.WeaponServices is not null &&
                client.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE &&
                client.InGameMoneyServices is not null &&
                Main.Instance.RoundCount > 0 &&
                Main.Instance.RoundCount < maxRound;
        }

        bool IsMoneyEnough(CCSPlayerController player, int itemCost) => player.InGameMoneyServices!.Account >= itemCost;

        void BuyItem(CCSPlayerController client, string item)
        {
            try
            {
                var price = Utility.WeaponMenu.FirstOrDefault(w => w.EntityName == item).Price;

                Utility.GiveWeapon(client, item);
                client.InGameMoneyServices!.Account -= price;
                Utilities.SetStateChanged(client, "CCSPlayerController", "m_pInGameMoneyServices");
            }
            catch (Exception ex)
            {
                ReplyToCommandWithTeamColor(client, command, "[css] Failed to buy weapon. Please contact admin!");
                _logger.LogError("Buy Command error {error}", ex);
            }
        }
    }

    public void OnLanguageCommand(CCSPlayerController client, CommandInfo command, string language)
    {
        if (!Utility.IsHumanPlayerValid(client))
            return;

        var playerCache = _playerService.GetPlayerCache(client.SteamID);
        if (playerCache is null)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Cannot update language, please reconnect to server!");
            _logger.LogWarning("Cannot update language, player cache is not found. SteamID: {steamID}", client.SteamID);
            return;
        }

        if (language != LanguageOption.English &&
            language != LanguageOption.TraditionalChinese &&
            language != LanguageOption.Japanese)
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] Invalid language: {language}");
            return;
        }

        playerCache.Language = language;
        _playerService.UpdateCache(playerCache);

        var languageName = language switch
        {
            LanguageOption.English => "English",
            LanguageOption.TraditionalChinese => "Traditional Chinese (繁體中文)",
            LanguageOption.Japanese => "Japanese (日本語)",
            _ => "Unknown"
        };

        ReplyToCommandWithTeamColor(client, command, $"SaySound language set to {languageName}");
    }

    public void OnHelpCommand(CCSPlayerController client, CommandInfo command)
    {
        if (!Utility.IsHumanPlayerValid(client))
            return;

        var availableCommands = GetAvailableCommandsForPlayer(client)
            .OrderBy(cmd => cmd.Name)
            .ToList();

        if (availableCommands.Count == 0)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] No commands available");
            return;
        }

        var adminCommands = availableCommands.Where(cmd => cmd.Permissions != null).ToList();
        var playerCommands = availableCommands.Where(cmd => cmd.Permissions == null).ToList();

        command.ReplyToCommand($" {ChatColors.Grey}===== {ChatColors.Purple}Available Commands {ChatColors.Grey}=====");

        if (playerCommands.Count != 0)
        {
            command.ReplyToCommand($" {ChatColors.Green}Player Commands:");
            foreach (var cmd in playerCommands)
            {
                ReplyToCommandWithTeamColor(client, command, $"{ChatColors.Yellow}{cmd.Name} {ChatColors.Grey}- {cmd.Description}");
            }
        }

        if (adminCommands.Count != 0)
        {
            command.ReplyToCommand($" {ChatColors.Red}Admin Commands:");
            foreach (var cmd in adminCommands)
            {
                ReplyToCommandWithTeamColor(client, command,
                    $"  {ChatColors.Yellow}{cmd.Name} {ChatColors.Grey}[{ChatColors.Orange}{string.Join(",", cmd.Permissions?.ToArray() ?? [])}{ChatColors.Grey}] - {cmd.Description}");
            }
        }

        command.ReplyToCommand($" {ChatColors.Grey}==================");
        ReplyToCommandWithTeamColor(client, command, $"Total: {ChatColors.Lime}{availableCommands.Count} {ChatColors.Grey}commands");
    }

    private void ExecutePlayerCommand(
        CCSPlayerController client,
        CommandInfo command,
        int minArgCount,
        string usageMessage,
        string commandName,
        string pastTenseVerb,
        Action<CCSPlayerController> playerAction,
        int maxArgCount = int.MaxValue,
        Func<string, string>? customSingleMessage = null,
        Func<string, string>? customBroadcastMessage = null)
    {
        // Validate argument count
        if (command.ArgCount < minArgCount || command.ArgCount > maxArgCount)
        {
            ReplyToCommandWithTeamColor(client, command, usageMessage);
            return;
        }

        var target = command.GetArg(1);
        var adminName = client?.PlayerName ?? "console";

        // Handle team/group targets
        if (target == "@all")
        {
            foreach (var player in Utilities.GetPlayers())
            {
                playerAction(player);
            }

            var message = customBroadcastMessage?.Invoke("") ?? $"Admin {pastTenseVerb} all players";
            Utility.PrintToChatAllWithColor(message);
            _logger.LogInformation("[css] {admin} {pastTense} all players at {DT}", adminName, pastTenseVerb, DateTime.Now);
            return;
        }
        else if (target == "@ct")
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.Team == CsTeam.CounterTerrorist))
            {
                playerAction(player);
            }

            Utility.PrintToChatAllWithColor($"Admin {pastTenseVerb} all CT players");
            _logger.LogInformation("[css] {admin} {pastTense} all CT players at {DT}", adminName, pastTenseVerb, DateTime.Now);
            return;
        }
        else if (target == "@t")
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.Team == CsTeam.Terrorist))
            {
                playerAction(player);
            }

            Utility.PrintToChatAllWithColor($"Admin {pastTenseVerb} all T players");
            _logger.LogInformation("[css] {admin} {pastTense} all T players at {DT}", adminName, pastTenseVerb, DateTime.Now);
            return;
        }

        // Handle individual player target
        var targetName = Main.Instance.GetTargetNameByKeyword(target);

        if (string.IsNullOrEmpty(targetName))
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Target not found.");
            return;
        }

        var targetPlayer = Utilities.GetPlayers().First(p => p.PlayerName == targetName);
        playerAction(targetPlayer);

        // Send messages
        var replyMessage = customSingleMessage?.Invoke(targetName) ?? $"[css] You {commandName} {targetName}";
        var broadcastMessage = customBroadcastMessage?.Invoke(targetName) ?? $"Admin {pastTenseVerb} {targetName}";

        ReplyToCommandWithTeamColor(client, command, replyMessage);
        _logger.LogInformation("[css] {admin} {pastTense} {targetName} at {DT}", adminName, pastTenseVerb, targetName, DateTime.Now);
        Utility.PrintToChatAllWithColor(broadcastMessage);
    }

    private static void ReviveCallBack(ref float time, CCSPlayerController client, Position position, Timer? timer, WeaponStatus weaponStatus)
    {
        time++;

        if ((5 - time) >= 1)
        {
            client.PrintToCenter($"Revive in {5 - time}");
        }
        else
        {
            client.PrintToCenter($"You have been revived.");
            client.Respawn();
            Server.NextFrameAsync(() =>
            {
                client.Pawn.Value!.Teleport(position.Origin, position.Rotation, position.Velocity);
                client.RemoveWeapons();

                if (weaponStatus.Weapons.Count == 0)
                {
                    weaponStatus.Weapons.Add(Utility.GetCsItemEnumValue(CsItem.Knife));
                    if (client.Team == CsTeam.Terrorist)
                    {
                        //var defaultTSecondaryWeapon = ConVar.Find("mp_t_default_secondary")!.StringValue; // StringValue won't work with value length that over specific bits I guess
                        weaponStatus.Weapons.Add(Utility.GetCsItemEnumValue(CsItem.Glock));
                    }
                    else if (client.Team == CsTeam.CounterTerrorist)
                    {
                        //var defaultCTSecondaryWeapon = ConVar.Find("mp_ct_default_secondary")!.StringValue;
                        weaponStatus.Weapons.Add(Utility.GetCsItemEnumValue(CsItem.USP));
                    }
                }

                foreach (var weapon in weaponStatus.Weapons)
                {
                    if (AppSettings.LogWeaponTracking)
                        client.PrintToChat($"try to give: {weapon}");
                    if (weapon == Utility.GetCsItemEnumValue(CsItem.C4))
                        continue;

                    client.GiveNamedItem(weapon);
                }
            });
            timer?.Kill();
        }
    }

    private bool TryValidateVolumeCommand(
        CCSPlayerController client,
        CommandInfo command,
        string commandName,
        VolumeType volumeType,
        out Player playerCache,
        out byte volume)
    {
        playerCache = null!;
        volume = 0;

        if (!Utility.IsHumanPlayerValid(client))
            return false;

        var tryGetCache = _playerService.GetPlayerCache(client.SteamID);
        if (tryGetCache is null)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Cannot update volume, please reconnect to server!");
            _logger.LogWarning("Cannot update volume, player cache is not found. SteamID: {steamID}", client.SteamID);
            return false;
        }

        playerCache = tryGetCache;

        if (command.ArgCount != 2)
        {
            ReplyToCommandWithTeamColor(client, command, $"[css] Usage: {commandName} [volume]");

            var currentVolume = volumeType switch
            {
                VolumeType.SaySound => playerCache.SaySoundVolume,
                VolumeType.Music => playerCache.Volume,
                _ => playerCache.Volume
            };

            Utility.AddTimer(0.5f, () =>
            {
                Utility.PrintToChatWithTeamColor(client, $"{volumeType} volume: {currentVolume}%");
            });
            return false;
        }

        var volumeString = command.GetArg(1);

        if (string.IsNullOrEmpty(volumeString) || !byte.TryParse(volumeString, out volume) || volume > 100)
        {
            ReplyToCommandWithTeamColor(client, command, "[css] Invalid volume");
            return false;
        }

        return true;
    }

    private void ReplyToCommandWithTeamColor(CCSPlayerController? player, CommandInfo command, string message)
    {
        command.ReplyToCommand($" {Utility.GetPlayerTeamChatColor(player)}{message}");
    }
}