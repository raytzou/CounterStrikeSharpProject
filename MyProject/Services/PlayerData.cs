using MyProject.Classes;
using MyProject.Domains;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class PlayerData : IPlayerData
    {
        private ProjectDbContext _dbContext;

        public PlayerData(ProjectDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Player? Get(ulong steamId)
        {
            return _dbContext.Players.FirstOrDefault(p => p.SteamId == steamId);
        }

        public void Add(Player player)
        {
            _dbContext.Players.Add(player);
        }

        public void SaveChanges()
        {
            _dbContext.SaveChanges();
        }

        public void Update(Player player)
        {
            _dbContext.Players.Update(player);
        }
    }
}
