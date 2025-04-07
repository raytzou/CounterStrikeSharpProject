using CounterStrikeSharp.API.Core;

namespace MyProject.Services.Interfaces
{
    public interface IPlayerService
    {
        void PlayerJoin(CCSPlayerController client);
        void UpdateDefaultSkin(ulong steamId, string skinPath);
    }
}
