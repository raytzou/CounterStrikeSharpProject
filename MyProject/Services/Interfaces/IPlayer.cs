using MyProject.Domains;

namespace MyProject.Services.Interfaces
{
    public interface IPlayer
    {
        Domains.Player? Get(ulong steamId);
        void Add(Domains.Player player);
        void Update(Domains.Player player);
        void SaveChanges();
    }
}
