using CounterStrikeSharp.API;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace MyProject.Classes
{
    public static class AppSettings
    {
        public static IConfigurationRoot? Configuration { get; private set; }
        public static bool IsDebug => Configuration?.GetValue<bool>("DebugMode") == true;
        public static bool LogWeaponTracking => Configuration?.GetValue<bool>("LogWeaponTracking") == true;
        public static bool LogBotAdd => Configuration?.GetValue<bool>("LogBotAdd") == true;

        static AppSettings()
        {
            try
            {
                var projectName = Assembly.GetExecutingAssembly().GetName().Name;
                var pluginPath = Path.Join(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "plugins", projectName);

                var builder = new ConfigurationBuilder()
                .SetBasePath(pluginPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true); // counterstrikesharp/plugins/{projectName}\appsettings.json

                Configuration = builder.Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading appsettings.json: {0}", ex.Message);
            }
        }
    }
}
