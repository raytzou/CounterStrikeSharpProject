using Microsoft.Extensions.Logging;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerManagementService(
        ILogger<PlayerManagementService> logger,
        IPlayerService playerService,
        IPlayerSkinService playerSkinService
        ) : IPlayerManagementService
    {
        private readonly ILogger<PlayerManagementService> _logger = logger;
        private readonly IPlayerService _playerService = playerService;
        private readonly IPlayerSkinService _playerSkinService = playerSkinService;

        public void SaveAllCachesToDB()
        {
            var allCaches = _playerService.GetAllCaches();

            _playerService.SaveCacheToDB(allCaches);

            foreach (var cache in allCaches)
            {
                _playerSkinService.SaveToDBFromCache(cache.PlayerSkins);
            }
        }

        public void SaveCacheToDB(ulong steamId)
        {
            var cache = _playerService.GetPlayerCache(steamId);

            if (cache is null)
            {
                _logger.LogWarning("Cannot save cache to DB, cache is not found. SteamID: {steamId}", steamId);
                return;
            }

            _playerService.SaveCacheToDB(cache);
            _playerSkinService.SaveToDBFromCache(cache.PlayerSkins);
        }
    }
}
