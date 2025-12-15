using System.Reflection;
using System.Text.Json;

namespace SaySoundHelper
{
    public class SaySoundHelper
    {
        
    }

    internal class ConfigProvider
    {
        private readonly ConfigModel _config;

        internal ConfigModel Config => _config;

        internal ConfigProvider(string configPath = "config.json")
        {
            if (!Path.IsPathRooted(configPath))
            {
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;
                configPath = Path.Combine(assemblyDirectory, configPath);
            }

            string json = File.ReadAllText(configPath);

            _config = JsonSerializer.Deserialize<ConfigModel>(json) ?? new ConfigModel();
        }

        internal class ConfigModel
        {
            internal string DownbloadUrl { get; set; } = string.Empty;
            internal string OutputPath { get; set; } = string.Empty;
        }
    }
}
