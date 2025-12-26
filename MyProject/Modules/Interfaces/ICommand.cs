using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MyProject.Models;

namespace MyProject.Modules.Interfaces
{
    public interface ICommand
    {
        IReadOnlyDictionary<string, CommandMetadata> AllCommands { get; }

        void RegisterCommands();

        IEnumerable<CommandMetadata> GetAvailableCommandsForPlayer(CCSPlayerController player);

        void OnKickCommand(CCSPlayerController client, CommandInfo command);

        void OnInfoCommand(CCSPlayerController client, CommandInfo command, int botRespawnRemaining);

        void OnChangeMapCommand(CCSPlayerController client, CommandInfo command, float changeMapTimeBuffer);

        void OnMapsCommand(CCSPlayerController client, CommandInfo command);

        void OnCvarCommand(CCSPlayerController client, CommandInfo command);

        void OnRconCommand(CCSPlayerController? client, CommandInfo command);

        void OnPlayersCommand(CCSPlayerController client, CommandInfo command);

        void OnSlayCommand(CCSPlayerController client, CommandInfo command);

        void OnGodCommand(CCSPlayerController client, CommandInfo command);

        void OnReviveCommand(CCSPlayerController client, CommandInfo command, Position position, WeaponStatus weaponStatus);

        void OnDebugCommand(CCSPlayerController client, CommandInfo command, Dictionary<string, WeaponStatus> weaponStatus);

        void OnModelsCommand(CCSPlayerController client, CommandInfo command, Main thePlugin);

        void OnSlapCommand(CCSPlayerController client, CommandInfo command);

        void OnVolumeCommand(CCSPlayerController client, CommandInfo command);

        void OnSaySoundVolumeCommand(CCSPlayerController client, CommandInfo command);

        void OnBuyCommand(CCSPlayerController client, CommandInfo command, Main thePlugin);

        void OnLanguageCommand(CCSPlayerController client, CommandInfo command, string language);

        void OnHelpCommand(CCSPlayerController client, CommandInfo command);
    }
}
