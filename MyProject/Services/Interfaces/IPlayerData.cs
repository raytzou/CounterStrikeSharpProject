using MyProject.Domains;

namespace MyProject.Services.Interfaces
{
    public interface IPlayerData
    {
        Player? Get(ulong steamId);
        void Add(Player player);
        void Update(Player player);
        void SaveChanges();
    }
}
