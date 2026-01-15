using CounterStrikeSharp.API.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyProject.Classes;
using MyProject.Domains;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerService(
        ILogger<PlayerService> logger,
        ProjectDbContext dbContext
        ) : IPlayerService
    {
        private readonly ILogger<PlayerService> _logger = logger;
        private readonly ProjectDbContext _dbContext = dbContext;

        public string GetDefaultSkin(ulong steamId) => _dbContext.Players.FirstOrDefault(player => player.SteamId == steamId)?.DefaultSkinModelPath ?? throw new NullReferenceException($"Cannot get the default skin SteamID: {steamId}");
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

        public void UpdateDefaultSkin(ulong steamId, string skinPath)
        {
            var playerData = _dbContext.Players
                .FirstOrDefault(x => x.SteamId == steamId);
            if (playerData is null)
            {
                _logger.LogError("Player with SteamID {steamId} not found.", steamId);
                return;
            }

            playerData.DefaultSkinModelPath = skinPath;

            if (_playerCache.TryGetValue(steamId, out var cachedPlayer))
            {
                cachedPlayer.DefaultSkinModelPath = skinPath;
            }
            else
            {
                _logger.LogWarning("Player {steamId} not found in cache when updating default skin", steamId);
            }

            _dbContext.Players.Update(playerData);
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
