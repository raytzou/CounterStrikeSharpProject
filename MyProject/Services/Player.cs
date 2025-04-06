using MyProject.Classes;
using MyProject.Domains;
using MyProject.Services.Interfaces;

namespace MyProject.Services
{
    public class Player : IPlayer
    {
        private ProjectDbContext _dbContext;

        public Player(ProjectDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Domains.Player? Get(ulong steamId)
        {
            return _dbContext.Players.FirstOrDefault(p => p.SteamId == steamId);
        }

        public void Add(Domains.Player player)
        {
            _dbContext.Players.Add(player);
        }

        public void SaveChanges()
        {
            _dbContext.SaveChanges();
        }

        public void Update(Domains.Player player)
        {
            _dbContext.Players.Update(player);
        }
    }
}
