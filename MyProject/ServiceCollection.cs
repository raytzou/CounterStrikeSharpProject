using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using MyProject.Plugins;

namespace MyProject
{
    public class ServiceCollection : IPluginServiceCollection<MyBase>
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MyBase>();
        }
    }
}
