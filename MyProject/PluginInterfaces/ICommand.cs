using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace MyProject.PluginInterfaces
{
    public interface ICommand
    {
        void OnKickCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundCount);

        void OnChangeMapCommand(CCSPlayerController client, CommandInfo command);

        void OnCvarCommand(CCSPlayerController client, CommandInfo command);

        void OnPlayersCommand(CCSPlayerController client, CommandInfo command, Dictionary<ulong, string> players);

        void OnSlayCommand(CCSPlayerController client, CommandInfo command, string targetName);
    }
}
