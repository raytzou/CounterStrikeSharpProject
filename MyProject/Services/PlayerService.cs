using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.EntityFrameworkCore;
using MyProject.Classes;
using MyProject.Domains;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerService(
        IDbContextFactory<ProjectDbContext> dbContextFactory
        ) : IPlayerService
    {
        private readonly IDbContextFactory<ProjectDbContext> _dbContextFactory = dbContextFactory;

        public Player? GetPlayerCache(ulong steamId) => _playerCache.TryGetValue(steamId, out var cache) ? cache : null;
        public IEnumerable<Player> GetAllCaches() => _playerCache.Values;

        private static readonly Dictionary<ulong, Player> _playerCache = [];

        public void PrepareCache(CCSPlayerController client)
        {
            Server.NextFrame(() =>
            {
                using var dbContext = _dbContextFactory.CreateDbContext();

                var playerSteamId = client.SteamID;
                var playerData = dbContext.Players
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
                        DefaultSkinModelPath = string.Empty,
                        Volume = 50,
                        SaySoundVolume = 50,
                        Language = LanguageOption.English
                    };
                    dbContext.Players.Add(playerData);
                }
                else
                {
                    playerData.DefaultSkinModelPath = string.Empty;
                    playerData.LastTimeConnect = DateTime.Now;
                    playerData.PlayerName = client.PlayerName;
                    playerData.IpAddress = client.IpAddress ?? string.Empty;
                }

                _playerCache[client.SteamID] = playerData;
                dbContext.SaveChanges();
            });
        }

        public void ClearPlayerCache(ulong steamId)
        {
            _playerCache.Remove(steamId);
        }

        public void ClearPlayerCaches()
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
            using var dbContext = _dbContextFactory.CreateDbContext();
            dbContext.Players.Update(player);
            dbContext.SaveChanges();
        }

        public void SaveAllCachesToDB()
        {
            if (_playerCache.Count == 0)
                return;

            using var dbContext = _dbContextFactory.CreateDbContext();

            foreach (var player in _playerCache.Values)
            {
                dbContext.Players.Update(player);
            }

            dbContext.SaveChanges();
        }
    }
}
