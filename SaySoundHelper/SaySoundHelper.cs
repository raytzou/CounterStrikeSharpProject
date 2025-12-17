using ClosedXML.Excel;
using System.Text.Json;

namespace SaySoundHelper
{
    public class SaySoundHelper
    {
        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static SaySoundHelper()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("SaysoundSheetDownloader", "1.0"));
        }

        public static async Task DownloadSaySoundExcel(string pluginDirectory)
        {
            var cfgProvider = new ConfigProvider(pluginDirectory);
            var url = cfgProvider.Config.DownloadUrl;

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("DownloadUrl is not configured in config.json");

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                throw new InvalidOperationException($"Invalid URL: {url}");

            var bytes = await _httpClient.GetByteArrayAsync(url);
            var outputPath = Path.Combine(pluginDirectory, cfgProvider.Config.OutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            await File.WriteAllBytesAsync(outputPath, bytes);
        }

        public static async Task<List<(string SaySound, string Content, string TWContent, string JPContent)>> LoadSaySounds(string pluginDirectory)
        {
            var cfgProvider = new ConfigProvider(pluginDirectory);
            var outputPath = cfgProvider.Config.OutputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("OutputPath is not configured in config.json");

            var saysoundDocument = Path.Combine(pluginDirectory, outputPath);
            if (!File.Exists(saysoundDocument))
                throw new InvalidOperationException("Cannot find the SaySound document");

            using var workbook = new XLWorkbook(saysoundDocument);
            var sheetName = cfgProvider.Config.SheetName;

            if (string.IsNullOrWhiteSpace(sheetName))
                throw new InvalidOperationException("SheetName is not configured in config.json");

            var sheet = workbook.Worksheet(sheetName);

            const int saySoundCol = 1;
            const int contentCol = 8;
            const int twContentCol = 7;
            const int jpContentCol = 6;

            return sheet.RowsUsed()
                .Skip(1)
                .Where(row => row.Cell(saySoundCol) is not null && !row.Cell(saySoundCol).Value.IsBlank)
                .Select(row => (
                    SaySound: row.Cell(saySoundCol).GetString(),
                    Content: row.Cell(contentCol).GetString(),
                    TWContent: row.Cell(twContentCol).GetString(),
                    JPContent: row.Cell(jpContentCol).GetString()
                ))
                .ToList();
        }
    }

    internal class ConfigProvider
    {
        private readonly ConfigModel _config;

        internal ConfigModel Config => _config;

        internal ConfigProvider(string pluginDirectory, string configFileName = "config.json")
        {
            var configPath = Path.Combine(pluginDirectory, configFileName);

            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Configuration file not found: {configPath}");

            var json = File.ReadAllText(configPath);

            _config = JsonSerializer.Deserialize<ConfigModel>(json) ?? new ConfigModel();
        }

        internal class ConfigModel
        {
            public string DownloadUrl { get; set; } = string.Empty;
            public string OutputPath { get; set; } = string.Empty;
            public string SheetName { get; set; } = string.Empty;
        }
    }
}
