using CounterStrikeSharp.API.Core;
using MyProject.Domains;

namespace MyProject.Services.Interfaces
{
    public interface IPlayerService
    {
        void PlayerJoin(CCSPlayerController client);
        string GetDefaultSkin(ulong steamId);
        Player? GetPlayerCache(ulong steamId);
        IEnumerable<Player> GetAllCaches();
        void ClearPlayerCache();
        void ResetPlayerSkinFromCache(Player playerCache);
        void UpdateCache(Player player);
        void SaveCacheToDB(Player player);
        void SaveAllCachesToDB();
    }
}
