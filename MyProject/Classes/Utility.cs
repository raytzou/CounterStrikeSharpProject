using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using MyProject.Models;
using System.Reflection;
using System.Runtime.Serialization;

namespace MyProject.Classes
{
    public class Utility
    {
        public static IEnumerable<string> MapsInPhysicalDirectory => _mapsInPhysicalDirectory;
        public static IEnumerable<string> MapsFromWorkshop => _mapsFromWorkShop;
        public static IEnumerable<string> AllMaps => _mapsInPhysicalDirectory.Concat(_mapsFromWorkShop);
        public static Dictionary<string, SkinInfo> WorkshopSkins => _workshopSkins;

        public readonly static List<CounterStrikeSharp.API.Modules.Timers.Timer> _timers;
        private static List<string> _mapsFromWorkShop;
        private static List<string> _mapsInPhysicalDirectory;
        private static readonly Dictionary<CsItem, string> _enumValue;
        private static readonly Dictionary<string, SkinInfo> _workshopSkins;

        static Utility()
        {
            _timers = [];
            _enumValue = [];
            _workshopSkins = [];
            _mapsFromWorkShop = [];
            _mapsInPhysicalDirectory = [];

            InitializeEnumValues();
            InitializeMaps();
            InitializeWorkshopSkins();

            static void InitializeEnumValues()
            {
                foreach (var field in typeof(CsItem).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
                    if (attribute != null && attribute.Value != null)
                    {
                        var value = (CsItem)field.GetValue(null)!;
                        _enumValue[value] = attribute.Value;
                    }
                }
            }

            static void InitializeMaps()
            {
                var mapListPath = Path.Join(Server.GameDirectory, "maplist.txt");
                if (!File.Exists(mapListPath)) throw new Exception("maplist.txt could not be found in root folder");
                _mapsFromWorkShop = File.ReadAllLines(mapListPath).ToList();

                string mapFolderPath = Path.Join(Server.GameDirectory, "csgo", "maps");
                _mapsInPhysicalDirectory = Directory.GetFiles(mapFolderPath)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(mapName =>
                        !string.IsNullOrEmpty(mapName) &&
                        !mapName.Contains("vanity") &&
                        !mapName.Contains("workshop_preview") &&
                        !mapName.Contains("graphics_settings") &&
                        !mapName.Contains("lobby_mapveto")).ToList()!;
            }

            static void InitializeWorkshopSkins()
            {
                var modelsPath = Path.Join(Server.GameDirectory, "models.txt");
                if (!File.Exists(modelsPath)) throw new Exception("models.txt could not be found in root folder");

                var lines = File.ReadAllLines(modelsPath)
                    .Select(line => line.Split(["//"], StringSplitOptions.None)[0].Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line));

                foreach (var line in lines)
                {
                    var split = line.Split(',');
                    var modelName = split[0];
                    var modelPath = split[1];
                    var armPath = split.Length > 2 ? split[2] : null;
                    var meshGroupIndex = split.Length > 3 ? split[3] : null;

                    _workshopSkins.Add(modelName, new SkinInfo());
                    _workshopSkins[modelName].ModelPath = modelPath;
                    if (!string.IsNullOrEmpty(armPath))
                        _workshopSkins[modelName].ArmPath = armPath;
                    else
                        _workshopSkins[modelName].ArmPath = null;
                    if (!string.IsNullOrEmpty(meshGroupIndex))
                        _workshopSkins[modelName].MeshGroupIndex = int.Parse(meshGroupIndex);
                    else
                        _workshopSkins[modelName].MeshGroupIndex = null;
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

        /// <summary>
        /// Calculate the mesh group mask for rendering
        /// </summary>
        /// <param name="enabledMeshGroups">The specific mesh group you like.</param>
        /// <param name="fixedMeshGroups">To be honest, idk</param>
        /// <returns></returns>
        public static ulong ComputeMeshGroupMask(int[] enabledMeshGroups, Dictionary<int, int> fixedMeshGroups)
        {
            ulong meshGroupMask = 0;

            foreach (var meshGroup in enabledMeshGroups)
            {
                meshGroupMask |= (ulong)1 << meshGroup;
            }

            foreach (var fixedMeshGroup in fixedMeshGroups)
            {
                if (fixedMeshGroup.Value == 0)
                {
                    meshGroupMask &= ~((ulong)1 << fixedMeshGroup.Key);
                }
                else
                {
                    meshGroupMask |= (ulong)1 << fixedMeshGroup.Key;
                }
            }

            return meshGroupMask;
        }

        /// <summary>
        /// Set the model for a client.
        /// </summary>
        /// <param name="client">player controller</param>
        /// <param name="modelName">skin name</param>
        public static void SetClientModel(CCSPlayerController client, string modelName)
        {
            var skin = _workshopSkins[modelName];
            client.PlayerPawn.Value!.SetModel(skin.ModelPath);
            if (skin.MeshGroupIndex.HasValue)
            {
                client.PlayerPawn.Value.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.MeshGroupMask =
                    Utility.ComputeMeshGroupMask(new int[] { skin.MeshGroupIndex.Value }, new Dictionary<int, int>());
            }
        }
    }
}
