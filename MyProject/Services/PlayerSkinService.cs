using MyProject.Classes;
using MyProject.Domains;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerSkinService(ProjectDbContext dbContext) : IPlayerSkinService
    {
        private readonly ProjectDbContext _dbContext = dbContext;

        public string GetActiveSkinName(ulong steamId) => _dbContext.PlayerSkins
            .FirstOrDefault(x => x.SteamId == steamId && x.IsActive)?.SkinName ?? string.Empty;

        public void Reset(ulong steamId)
        {
            var currentSkins = _dbContext.PlayerSkins
                .Where(x => x.SteamId == steamId && x.IsActive)
                .ToList();

            currentSkins.ForEach(x =>
            {
                x.IsActive = false;
            });

            _dbContext.PlayerSkins.UpdateRange(currentSkins);
            _dbContext.SaveChanges();
        }

        public void Update(ulong steamId, string skinName)
        {
            Reset(steamId);

            var skin = _dbContext.PlayerSkins
                .FirstOrDefault(x => x.SteamId == steamId && x.SkinName == skinName);

            if (skin is null)
            {
                var newSkin = new PlayerSkin
                {
                    Id = Guid.NewGuid(),
                    SteamId = steamId,
                    SkinName = skinName,
                    AcquiredAt = DateTime.Now,
                    IsActive = true,
                    ExpiresAt = null
                };

                _dbContext.PlayerSkins.Add(newSkin);
            }
            else
            {
                skin.IsActive = true;
                _dbContext.PlayerSkins.Update(skin);
            }

            _dbContext.SaveChanges();
        }

        public void SaveToDBFromCache(IEnumerable<PlayerSkin> skinsFromCache)
        {
            foreach (var skin in skinsFromCache)
            {
                var existingSkin = _dbContext.PlayerSkins
                    .FirstOrDefault(x => x.SteamId == skin.SteamId && x.SkinName == skin.SkinName);
                if (existingSkin is null)
                {
                    _dbContext.PlayerSkins.Add(skin);
                }
                else
                {
                    existingSkin.IsActive = skin.IsActive;
                    existingSkin.ExpiresAt = skin.ExpiresAt;
                    _dbContext.PlayerSkins.Update(existingSkin);
                }
            }

            _dbContext.SaveChanges();
        }
    }
}
