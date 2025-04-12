using MyProject.Classes;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerManagementService(
        ProjectDbContext dbContext,
        IPlayerService playerService,
        IPlayerSkinService playerSkinService
        ) : IPlayerManagementService
    {
        private readonly ProjectDbContext _dbContext = dbContext;

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
    }
}
