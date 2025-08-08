using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerManagementService(
        IPlayerService playerService,
        IPlayerSkinService playerSkinService
        ) : IPlayerManagementService
    {
        private readonly IPlayerService _playerService = playerService;
        private readonly IPlayerSkinService _playerSkinService = playerSkinService;

        public void SaveAllCachesToDB()
        {
            var allCaches = _playerService.GetAllCaches();

            _playerService.SaveCachesToDB(allCaches);

            foreach (var cache in allCaches)
            {
                _playerSkinService.SaveToDBFromCache(cache.PlayerSkins);
            }
        }

        public void SaveCacheToDB(ulong steamId)
        {
            var cache = _playerService.GetPlayerCache(steamId);

            _playerService.SaveCacheToDB(cache);
            _playerSkinService.SaveToDBFromCache(cache.PlayerSkins);
        }
    }
}
