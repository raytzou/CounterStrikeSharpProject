using CounterStrikeSharp.API.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyProject.Modules;
using MyProject.Modules.Interfaces;
using MyProject.Services;
using MyProject.Services.Interfaces;

namespace MyProject.Classes
{
    public class ServiceCollection : IPluginServiceCollection<Main>
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = AppSettings.Configuration?.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Cannot find the connection string");
            services.AddDbContext<ProjectDbContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<ICommand, Command>();
            services.AddSingleton<IBot, Bot>();
            services.AddSingleton<IMusic, Music>();
        }
    }
}
