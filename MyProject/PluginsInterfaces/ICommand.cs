﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace MyProject.PluginsInterfaces
{
    public interface ICommand
    {
        void OnKickCommand(CCSPlayerController client, CommandInfo command, string targetName);

        void OnInfoCommand(CCSPlayerController client, CommandInfo command, int playerCount, int roundNum);

        void OnChangeMapCommand(CCSPlayerController client, CommandInfo command);

        void OnCvarCommand(CCSPlayerController client, CommandInfo command);
    }
}
