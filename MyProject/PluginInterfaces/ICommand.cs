using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace MyProject.PluginInterfaces
{
    public interface ICommand
    {
        void OnKickCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundCount);

        string OnChangeMapCommand(CCSPlayerController client, CommandInfo command);

        void OnCvarCommand(CCSPlayerController client, CommandInfo command);

        void OnPlayersCommand(CCSPlayerController client, CommandInfo command);

        void OnSlayCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnGodCommand(CCSPlayerController client, CommandInfo command);
    }
}
