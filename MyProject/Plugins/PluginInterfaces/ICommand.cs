using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MyProject.Models;

namespace MyProject.Plugins.PluginInterfaces
{
    public interface ICommand
    {
        void OnKickCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundCount, int botRespawnRemaining);

        void OnChangeMapCommand(CCSPlayerController client, CommandInfo command, float changeMapTimeBuffer);

        void OnMapsCommand(CCSPlayerController client, CommandInfo command);

        void OnCvarCommand(CCSPlayerController client, CommandInfo command);

        void OnPlayersCommand(CCSPlayerController client, CommandInfo command);

        void OnSlayCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnGodCommand(CCSPlayerController client, CommandInfo command);

        void OnReviveCommand(CCSPlayerController client, CommandInfo command, int costScore, Position position, Dictionary<string, WeaponStatus> weaponStatus);

        void OnDebugCommand(CCSPlayerController client, CommandInfo command, Dictionary<string, WeaponStatus> weaponStatus);

        void OnModelsCommand(CCSPlayerController client, CommandInfo command);
    }
}
