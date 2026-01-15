using CounterStrikeSharp.API.Core;
using Microsoft.EntityFrameworkCore;
using MyProject.Classes;
using MyProject.Domains;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerService(
        ProjectDbContext dbContext
        ) : IPlayerService
    {
        private readonly ProjectDbContext _dbContext = dbContext;

        public Player? GetPlayerCache(ulong steamId) => _playerCache.TryGetValue(steamId, out var cache) ? cache : null;
        public IEnumerable<Player> GetAllCaches() => _playerCache.Values;

        private static readonly Dictionary<ulong, Player> _playerCache = [];

        public void PlayerJoin(CCSPlayerController client)
        {
            var playerSteamId = client.SteamID;
            var playerData = _dbContext.Players
                .Include(x => x.PlayerSkins)
                .FirstOrDefault(x => x.SteamId == playerSteamId);

            if (playerData is null)
            {
                playerData = new Player
                {
                    SteamId = playerSteamId,
                    PlayerName = client.PlayerName,
                    IpAddress = client.IpAddress ?? string.Empty,
                    LastTimeConnect = DateTime.Now,
                    DefaultSkinModelPath = Utility.GetPlayerDefaultSkin(client),
                    Volume = 50,
                    SaySoundVolume = 50,
                    Language = LanguageOption.English
                };
                _dbContext.Players.Add(playerData);
            }
            else
            {
                playerData.DefaultSkinModelPath = Utility.GetPlayerDefaultSkin(client);
                playerData.LastTimeConnect = DateTime.Now;
                playerData.PlayerName = client.PlayerName;
                playerData.IpAddress = client.IpAddress ?? string.Empty;
            }

            _playerCache[client.SteamID] = playerData;
            _dbContext.SaveChanges();
        }

        public void ClearPlayerCache()
        {
            _playerCache.Clear();
        }

        public void ResetPlayerSkinFromCache(Player playerCache)
        {
            foreach (var skin in playerCache.PlayerSkins)
            {
                if (skin.IsActive)
                    skin.IsActive = false;
            }
        }

        public void UpdateCache(Player player)
        {
            _playerCache[player.SteamId] = player;
        }

        public void SaveCacheToDB(Player player)
        {
            _dbContext.Players.Update(player);
            _dbContext.SaveChanges();
        }

        public void SaveAllCachesToDB()
        {
            _dbContext.SaveChanges();
        }
    }
}
