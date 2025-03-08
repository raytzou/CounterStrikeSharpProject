using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Serialization;

namespace MyProject.Classes
{
    public class Utility
    {
        public static IEnumerable<string> MapsInPhysicalDirectory => _mapsInPhysicalDirectory;
        public static IEnumerable<string> MapsFromWorkshop => _mapsFromWorkShop;
        public static IEnumerable<string> AllMaps => _mapsInPhysicalDirectory.Concat(_mapsFromWorkShop);
        public static Dictionary<string, string> WorkshopSkins => _workshopSkins;

        public readonly static List<CounterStrikeSharp.API.Modules.Timers.Timer> _timers;
        private static readonly Dictionary<CsItem, string> _enumValue;
        private static readonly List<string> _mapsFromWorkShop;
        private static readonly List<string> _mapsInPhysicalDirectory;
        private static readonly Dictionary<string, string> _workshopSkins;

        static Utility()
        {
            _timers = [];
            _enumValue = [];
            _workshopSkins = [];

            foreach (var field in typeof(CsItem).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
                if (attribute != null && attribute.Value != null)
                {
                    var value = (CsItem)field.GetValue(null)!;
                    _enumValue[value] = attribute.Value;
                }
            }

            var mapListPath = Path.Join(Server.GameDirectory, "maplist.txt");
            if (!File.Exists(mapListPath)) throw new Exception("maplist.txt could not be found in root folder");
            _mapsFromWorkShop = [.. File.ReadAllLines(mapListPath)];

            string mapFolderPath = Path.Join(Server.GameDirectory, "csgo", "maps");
            _mapsInPhysicalDirectory = Directory.GetFiles(mapFolderPath)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(mapName =>
                    !string.IsNullOrEmpty(mapName) &&
                    !mapName.Contains("vanity") &&
                    !mapName.Contains("workshop_preview") &&
                    !mapName.Contains("graphics_settings") &&
                    !mapName.Contains("lobby_mapveto")).ToList()!;

            var modelsPath = Path.Join(Server.GameDirectory, "models.txt");
            if (!File.Exists(modelsPath)) throw new Exception("models.txt could not be found in root folder");

            foreach (var line in File.ReadAllLines(modelsPath))
            {
                var split = line.Split(',');
                var modelName = split[0];
                var modelPath = split[1];

                _workshopSkins.Add(modelName, modelPath);
            }

        }

        /// <summary>
        /// This is for debugging purposes only. There should be no references to this method.
        /// </summary>
        public static void DebugLogger<T>(ILogger<T> logger, string content) where T : class
        {
            logger.LogInformation("{content}", content);
        }

        /// <summary>
        /// It's the same as AddTimer() in BasicPlugin, but I want to use it elsewhere
        /// </summary>
        public static CounterStrikeSharp.API.Modules.Timers.Timer AddTimer(float interval, Action callback, TimerFlags? flags = null)
        {
            var timer = new CounterStrikeSharp.API.Modules.Timers.Timer(interval, callback, flags ?? 0);
            _timers.Add(timer);
            return timer;
        }

        /// <summary>
        /// Uses reflection to get the <see cref="EnumMemberAttribute"/> value of a CsItem enum.
        /// </summary>
        /// <param name="item">The enum option of <see cref="CsItem"/>.</param>
        /// <returns><c>string</c> - The item entity name.</returns>
        public static string GetCsItemEnumValue(CsItem item)
        {
            return _enumValue.TryGetValue(item, out var value) ? value : string.Empty;
        }
    }
}
