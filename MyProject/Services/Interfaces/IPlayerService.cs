using CounterStrikeSharp.API.Core;
using MyProject.Domains;

namespace MyProject.Services.Interfaces
{
    public interface IPlayerService
    {
        void PrepareCache(CCSPlayerController client);
        Player? GetPlayerCache(ulong steamId);
        IEnumerable<Player> GetAllCaches();
        void ClearPlayerCaches();
        void ClearPlayerCache(ulong steamId);
        void ResetPlayerSkinFromCache(Player playerCache);
        void UpdateCache(Player player);
        void SaveCacheToDB(Player player);
        void SaveAllCachesToDB();
    }
}
