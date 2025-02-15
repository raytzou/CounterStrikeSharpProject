using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MyProject.Models;

namespace MyProject.PluginInterfaces
{
    public interface ICommand
    {
        void OnKickCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundCount);

        void OnChangeMapCommand(CCSPlayerController client, CommandInfo command, float changeMapTimeBuffer);

        void OnMapsCommand(CCSPlayerController client, CommandInfo command);

        void OnCvarCommand(CCSPlayerController client, CommandInfo command);

        void OnPlayersCommand(CCSPlayerController client, CommandInfo command);

        void OnSlayCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnGodCommand(CCSPlayerController client, CommandInfo command);

        void OnReviveCommand(CCSPlayerController client, CommandInfo command, Position position);

        void OnWeaponCommand(CCSPlayerController client, CommandInfo command);
    }
}
