using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using MyProject.PluginClasses;

namespace MyProject.Classes
{
    public class ServiceCollection : IPluginServiceCollection<Main>
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Command>();
        }
    }
}
