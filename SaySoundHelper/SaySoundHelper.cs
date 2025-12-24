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
        private static Dictionary<string, (string SoundEvent, string Content, string TWContent, string JPContent)>? _saysounds;
        private static string? _soundEventFile;

        static SaySoundHelper()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("SaysoundSheetDownloader", "1.0"));
        }

        public static Dictionary<string, (string SoundEvent, string Content, string TWContent, string JPContent)> SaySounds
            => _saysounds ?? throw new InvalidOperationException("SaySoundHelper not initialized");

        public static string SoundEventFile => _soundEventFile ?? throw new InvalidOperationException("SaySoundHelper not initialized");

        public static async Task InitializeAsync(string pluginDirectory)
        {
            var cfgProvider = new ConfigProvider(pluginDirectory);

            _soundEventFile = cfgProvider.Config.SoundEventFile;
            await DownloadSaySoundExcel(pluginDirectory, cfgProvider).ConfigureAwait(false);
            _saysounds = LoadSaySounds(pluginDirectory);
        }

        private static async Task DownloadSaySoundExcel(string pluginDirectory, ConfigProvider cfgProvider)
        {
            var url = cfgProvider.Config.DownloadUrl;

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("DownloadUrl is not configured in config.json");

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                throw new InvalidOperationException($"Invalid URL: {url}");

            var bytes = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
            var outputPath = Path.Combine(pluginDirectory, cfgProvider.Config.OutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            await File.WriteAllBytesAsync(outputPath, bytes).ConfigureAwait(false);
        }

        private static Dictionary<string, (string SoundEvent, string Content, string TWContent, string JPContent)> LoadSaySounds(string pluginDirectory)
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
            const int soundEventCol = 12;
            const int contentCol = 8;
            const int twContentCol = 7;
            const int jpContentCol = 6;

            return sheet.RowsUsed()
                .Skip(1)
                .Select(row => new
                {
                    Row = row,
                    RawSoundEvent = row.Cell(soundEventCol).GetString()
                })
                .Where(x =>
                    x.Row.Cell(saySoundCol) is not null
                    && !x.Row.Cell(saySoundCol).Value.IsBlank
                    && HasSoundEvent(x.RawSoundEvent))
                .Select(x => new
                {
                    SaySound = x.Row.Cell(saySoundCol).GetString(),
                    Contents = (
                        SoundEvent: GetSoundEventName(x.RawSoundEvent),
                        Content: x.Row.Cell(contentCol).GetString(),
                        TWContent: x.Row.Cell(twContentCol).GetString(),
                        JPContent: x.Row.Cell(jpContentCol).GetString()
                    )
                })
                .ToDictionary(keySelector: x => x.SaySound, elementSelector: x => x.Contents);

            bool HasSoundEvent(string rawSoundEvent)
                => !string.IsNullOrWhiteSpace(rawSoundEvent)
                && rawSoundEvent.StartsWith('"')
                && rawSoundEvent.Contains('=');

            string GetSoundEventName(string rawSoundEvent)
            {
                var firstPart = rawSoundEvent.Split('=')[0].Trim();
                return firstPart.Trim('"');
            }
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
            public string SoundEventFile { get; set; } = string.Empty;
        }
    }
}
