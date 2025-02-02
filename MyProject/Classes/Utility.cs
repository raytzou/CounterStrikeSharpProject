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
        public readonly static List<CounterStrikeSharp.API.Modules.Timers.Timer> Timers = [];
        private static readonly Dictionary<CsItem, string> EnumValueCache;

        public static IEnumerable<string> AllMaps => GetMapsInPhysicalDirectory().Concat(GetMapsFromWorkshop());

        static Utility()
        {
            EnumValueCache = new Dictionary<CsItem, string>();

            foreach (var field in typeof(CsItem).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
                if (attribute != null)
                {
                    var value = (CsItem)field.GetValue(null);
                    EnumValueCache[value] = attribute.Value;
                }
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
        public static CounterStrikeSharp.API.Modules.Timers.Timer MyAddTimer(float interval, Action callback, TimerFlags? flags = null)
        {
            var timer = new CounterStrikeSharp.API.Modules.Timers.Timer(interval, callback, flags ?? 0);
            Timers.Add(timer);
            return timer;
        }

        /// <summary>
        /// Uses reflection to get the <see cref="EnumMemberAttribute"/> value of a CsItem enum.
        /// </summary>
        /// <param name="item">The enum option of <see cref="CsItem"/>.</param>
        /// <returns><c>string</c> - The item entity name.</returns>
        public static string GetCsItemEnumValue(CsItem item)
        {
            return EnumValueCache.TryGetValue(item, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Get maps in the maplist.txt file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception">Can't find maplist.txt</exception>
        public static IEnumerable<string> GetMapsFromWorkshop()
        {
            // if there is an API like ServerCommandEx() from SourceMod then I don't need to use maplist.txt
            var mapListPath = Path.Join(Server.GameDirectory, "maplist.txt");
            if (!File.Exists(mapListPath)) throw new Exception("maplist.txt could not be found in root folder");

            return File.ReadAllLines(mapListPath);
        }

        /// <summary>
        /// Get maps in the physical directory
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetMapsInPhysicalDirectory()
        {
            string mapFolderPath = Path.Join(Server.GameDirectory, "csgo", "maps");

            return Directory.GetFiles(mapFolderPath)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(mapName =>
                    !string.IsNullOrEmpty(mapName) &&
                    !mapName.Contains("vanity") &&
                    !mapName.Contains("workshop_preview") &&
                    !mapName.Contains("graphics_settings") &&
                    !mapName.Contains("lobby_mapveto"))!;
        }
    }
}
