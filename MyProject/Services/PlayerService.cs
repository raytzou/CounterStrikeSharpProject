using CounterStrikeSharp.API.Core;
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
        private ProjectDbContext _dbContext = dbContext;

        public void PlayerJoin(CCSPlayerController client)
        {
            var playerSteamId = client.SteamID;
            var playerData = _dbContext.Players
                .FirstOrDefault(x => x.SteamId == playerSteamId);
            if (playerData is null)
            {
                var newPlayer = new Player
                {
                    SteamId = playerSteamId,
                    PlayerName = client.PlayerName,
                    IpAddress = client.IpAddress ?? string.Empty,
                    LastTimeConnect = DateTime.Now
                };
                _dbContext.Players.Add(newPlayer);
            }
            else
            {
                playerData.LastTimeConnect = DateTime.Now;
                playerData.PlayerName = client.PlayerName;
                playerData.IpAddress = client.IpAddress ?? string.Empty;
                _dbContext.Players.Update(playerData);
            }

            _dbContext.SaveChanges();
        }
    }
}
