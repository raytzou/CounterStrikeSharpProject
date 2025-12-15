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

        internal ConfigProvider(string pluginDirectory, string configFileName = "config.json")
        {
            var configPath = Path.Combine(pluginDirectory, configFileName);
            var json = File.ReadAllText(configPath);

            _config = JsonSerializer.Deserialize<ConfigModel>(json) ?? new ConfigModel();
        }

        internal class ConfigModel
        {
            public string DownloadUrl { get; set; } = string.Empty;
            public string OutputPath { get; set; } = string.Empty;
        }
    }
}
