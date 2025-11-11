using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
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
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace MyProject.Modules;

public class Command(
    ILogger<Command> logger,
    IPlayerService playerService
    ) : ICommand
{
    private readonly ILogger<Command> _logger = logger;
    private readonly IPlayerService _playerService = playerService;

    public void OnKickCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_kick <target>");
            return;
        }

        var targetName = Main.Instance.GetTargetNameByKeyword(command.GetArg(1));

        if (string.IsNullOrEmpty(targetName))
        {
            command.ReplyToCommand("[css] Target not found.");
            return;
        }

        Server.ExecuteCommand($"kick {targetName}");
        command.ReplyToCommand($"[css] You kick {targetName}");
        _logger.LogInformation("[css] {admin} kicked {targetName} at {DT}", client.PlayerName, targetName, DateTime.Now);
        Server.PrintToChatAll($"Admin kicked {targetName}");
    }

    public void OnInfoCommand(CCSPlayerController client, CommandInfo command, int botRespawnRemaining)
    {
        command.ReplyToCommand("----------");
        command.ReplyToCommand($"Server local time: {DateTime.Now}");
        command.ReplyToCommand($"Current map: {Server.MapName}");
        command.ReplyToCommand($"Player: {Main.Instance.PlayerCount}/{Server.MaxPlayers}");
        command.ReplyToCommand($"Round: {Main.Instance.RoundCount}/{ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>()}");
        command.ReplyToCommand($"Bot respawn remaining: {botRespawnRemaining}");
        command.ReplyToCommand("----------");
    }

    public void OnChangeMapCommand(CCSPlayerController client, CommandInfo command, float changeMapTimeBuffer)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_map <map name>");
            return;
        }

        var mapName = command.GetArg(1);

        if (!Utility.AllMaps.Contains(mapName))
        {
            command.ReplyToCommand($"[css] Map not found: {mapName}");
            return;
        }

        _logger.LogInformation("{admin} changed map to {mapName} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, mapName, DateTime.Now);
        Server.PrintToChatAll($"Admin changed map to {mapName}");

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
            command.ReplyToCommand(map);
        }
    }

    public void OnCvarCommand(CCSPlayerController client, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand("[css] Usage: css_cvar <ConVar> <Value>");
            return;
        }

        var cvar = ConVar.Find(command.GetArg(1));

        if (cvar is null)
        {
            command.ReplyToCommand($"[css] Cannot find the ConVar: {command.GetArg(1)}");
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
                    command.ReplyToCommand($"{cvar.Name}: {cvar.GetPrimitiveValue<int>()}");
                    break;
                case ConVarType.Float32:
                case ConVarType.Float64:
                    command.ReplyToCommand($"{cvar.Name}: {cvar.GetPrimitiveValue<float>()}");
                    break;
                case ConVarType.Bool:
                    command.ReplyToCommand($"{cvar.Name}: {cvar.GetPrimitiveValue<bool>()}");
                    break;
                default:
                    command.ReplyToCommand($"[css] ConVar: {cvar.Name}, type: {cvar.Type}");
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
                    command.ReplyToCommand("[css] Value type error!");
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
                    command.ReplyToCommand("[css] Value type error!");
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
                        command.ReplyToCommand("[css] Value type error!");
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
            Server.PrintToChatAll($"{cvar.Name} changed to {value}");
            _logger.LogInformation("{admin} changed {cvar} to {value} at {DT}", client?.PlayerName is null ? "console" : client.PlayerName, cvar.Name, value, DateTime.Now);
        }
    }

    public void OnPlayersCommand(CCSPlayerController client, CommandInfo command)
    {
        var players = Utilities.GetPlayers();
        if (players.Count == 0)
        {
            command.ReplyToCommand("no any player");
            return;
        }

        foreach (var player in players)
        {
            command.ReplyToCommand(player.PlayerName);
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
            command.ReplyToCommand("[css] God mode on");
        else
            command.ReplyToCommand("[css] God mode off");

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
            command.ReplyToCommand("[css] You are in spectator mode, cannot revive.");
            return;
        }

        if (client.PawnIsAlive)
        {
            command.ReplyToCommand("[css] You are alive."); // after connecting server, the Health is alawys 100, no matter player is dead or alive
            return;
        }

        if (client.Score - reviveCost < 0 && !AppSettings.IsDebug)
        {
            command.ReplyToCommand($"[css] You don't have enough score ({reviveCost}) to revive.");
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
        var menu = new ScreenMenu("Select Models", thePlugin);
        var playerCache = _playerService.GetPlayerCache(client.SteamID);
        const int displayMenuInterval = 10; // second? TODO: need to check the unit

        if (playerCache is null)
        {
            command.ReplyToCommand("Cannot open model menu, please reconnect to server!");
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
                        Id = Guid.NewGuid(),
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

        menu.Display(client, displayMenuInterval);
    }

    public void OnSlapCommand(CCSPlayerController client, CommandInfo command)
    {
        // Validate slap-specific damage parameter
        if (command.ArgCount >= 2 && !string.IsNullOrWhiteSpace(command.GetArg(2)))
        {
            var dmgString = command.GetArg(2);
            if (!int.TryParse(dmgString, out var damage) || damage < 0)
            {
                command.ReplyToCommand($"[css] {dmgString} is invalid amount.");
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
        if (!Utility.IsHumanPlayerValid(client)) return;

        if (command.ArgCount != 2)
        {
            command.ReplyToCommand("[css] Usage: css_volume [volume]");
            return;
        }

        var volumeString = command.GetArg(2);

        if (string.IsNullOrEmpty(volumeString) || !byte.TryParse(volumeString, out var volume) || volume < 10 || volume > 100)
        {
            command.ReplyToCommand("Invalid volume");
            return;
        }

        var playerCache = _playerService.GetPlayerCache(client.SteamID);

        if (playerCache is null)
        {
            command.ReplyToCommand("Cannot update volume, please reconnect to server!");
            _logger.LogWarning("Cannot update volume, player cache is not found. SteamID: {steamID}", client.SteamID);
            return;
        }

        playerCache.Volume = volume;
        _playerService.UpdateCache(playerCache);
        command.ReplyToCommand($"Volume set to {volume}%");
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
            command.ReplyToCommand(usageMessage);
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
            Server.PrintToChatAll(message);
            _logger.LogInformation("[css] {admin} {pastTense} all players at {DT}", adminName, pastTenseVerb, DateTime.Now);
            return;
        }
        else if (target == "@ct")
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.Team == CsTeam.CounterTerrorist))
            {
                playerAction(player);
            }

            Server.PrintToChatAll($"Admin {pastTenseVerb} all CT players");
            _logger.LogInformation("[css] {admin} {pastTense} all CT players at {DT}", adminName, pastTenseVerb, DateTime.Now);
            return;
        }
        else if (target == "@t")
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.Team == CsTeam.Terrorist))
            {
                playerAction(player);
            }

            Server.PrintToChatAll($"Admin {pastTenseVerb} all T players");
            _logger.LogInformation("[css] {admin} {pastTense} all T players at {DT}", adminName, pastTenseVerb, DateTime.Now);
            return;
        }

        // Handle individual player target
        var targetName = Main.Instance.GetTargetNameByKeyword(target);

        if (string.IsNullOrEmpty(targetName))
        {
            command.ReplyToCommand("[css] Target not found.");
            return;
        }

        var targetPlayer = Utilities.GetPlayers().First(p => p.PlayerName == targetName);
        playerAction(targetPlayer);

        // Send messages
        var replyMessage = customSingleMessage?.Invoke(targetName) ?? $"[css] You {commandName} {targetName}";
        var broadcastMessage = customBroadcastMessage?.Invoke(targetName) ?? $"Admin {pastTenseVerb} {targetName}";

        command.ReplyToCommand(replyMessage);
        _logger.LogInformation("[css] {admin} {pastTense} {targetName} at {DT}", adminName, pastTenseVerb, targetName, DateTime.Now);
        Server.PrintToChatAll(broadcastMessage);
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
}