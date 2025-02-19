﻿using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using MyProject.PluginClasses;
using MyProject.PluginInterfaces;

namespace MyProject.Classes
{
    public class ServiceCollection : IPluginServiceCollection<Main>
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ICommand, Command>();
            services.AddSingleton<IBot, Bot>();
        }
    }
}
