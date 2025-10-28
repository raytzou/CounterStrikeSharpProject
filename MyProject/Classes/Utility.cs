using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MyProject.Models;
using System.Drawing;
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

        private readonly static List<CounterStrikeSharp.API.Modules.Timers.Timer> _timers;
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

        [Flags]
        public enum FadeFlags
        {
            None = 0,
            FadeIn = 0x0001,
            FadeOut = 0x0002,
            FadeStayOut = 0x0008,
            Purge = 0x0010
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
            if (!_workshopSkins.ContainsKey(modelName))
            {
                if (!isPath(modelName))
                    throw new ArgumentException($"{modelName} is not a default skin path nor a workshop skin name");
                client.PlayerPawn.Value!.SetModel(modelName);
                return;
            }
            var skin = _workshopSkins[modelName];
            client.PlayerPawn.Value!.SetModel(skin.ModelPath);
            if (skin.MeshGroupIndex.HasValue)
            {
                client.PlayerPawn.Value.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.MeshGroupMask =
                    Utility.ComputeMeshGroupMask(new int[] { skin.MeshGroupIndex.Value }, new Dictionary<int, int>());
            }

            static bool isPath(string path)
            {
                try
                {
                    Path.GetFullPath(path);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Draws a beacon effect on a player
        /// </summary>
        /// <param name="player">The player to draw the beacon on</param>
        /// <param name="beamColor">Color of the beacon beams</param>
        /// <param name="beamRadius">Maximum radius of the beacon circle</param>
        /// <param name="beamLifeTime">How long each beam should last</param>
        /// <param name="beamWidth">Width of the beacon beams</param>
        public static void DrawBeaconOnPlayer(CCSPlayerController player, Color beamColor, float beamRadius, float beamLifeTime = 2f, float beamWidth = 2f)
        {
            // Parameter validation
            if (player?.PlayerPawn?.Value == null || !player.IsValid)
                return;

            if (player.Pawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                return;

            if (beamLifeTime <= 0 || beamWidth <= 0 || beamRadius <= 0)
                return;

            var centerPosition = new Vector(
                player.PlayerPawn.Value!.AbsOrigin!.X,
                player.PlayerPawn.Value!.AbsOrigin!.Y,
                player.PlayerPawn.Value!.AbsOrigin!.Z + 35);

            const int lineCount = 20;
            var beamEntities = new List<CBeam>();

            float angleStep = (float)(2.0 * Math.PI) / lineCount;
            float currentRadius = 20.0f;
            float beaconTimerSeconds = 0.0f;
            bool hasReachedTargetRadius = false;

            // Calculate animation duration based on radius growth
            const float radiusIncrement = 10.0f;

            // Create initial beacon circle
            for (int i = 0; i < lineCount; i++)
            {
                float startAngle = i * angleStep;
                float endAngle = ((i + 1) % lineCount) * angleStep;

                Vector startPosition = GetAngleOnCircle(startAngle, currentRadius, centerPosition);
                Vector endPosition = GetAngleOnCircle(endAngle, currentRadius, centerPosition);

                var beam = DrawLaserBetween(startPosition, endPosition, beamColor, beamLifeTime, beamWidth);
                if (beam != null)
                {
                    beamEntities.Add(beam);
                }
            }

            // Animation timer with proper cleanup
            var animationTimer = AddTimer(0.1f, () =>
            {
                // Check if player is still valid
                if (!player.IsValid || player.Pawn.Value?.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                {
                    CleanupBeams();
                    return;
                }

                // Check if total time exceeded beamLifeTime
                if (beaconTimerSeconds >= beamLifeTime - 0.1f)
                {
                    CleanupBeams();
                    return;
                }

                // Only update positions if we haven't reached target radius yet
                if (!hasReachedTargetRadius && currentRadius < beamRadius)
                {
                    // Update beam positions
                    for (int i = 0; i < Math.Min(beamEntities.Count, lineCount); i++)
                    {
                        if (beamEntities[i] == null || !beamEntities[i].IsValid)
                            continue;

                        float startAngle = i * angleStep;
                        float endAngle = ((i + 1) % lineCount) * angleStep;

                        Vector startPosition = GetAngleOnCircle(startAngle, currentRadius, centerPosition);
                        Vector endPosition = GetAngleOnCircle(endAngle, currentRadius, centerPosition);

                        TeleportLaser(beamEntities[i], startPosition, endPosition);
                    }

                    currentRadius += radiusIncrement;

                    // Check if we've reached or exceeded the target radius
                    if (currentRadius >= beamRadius)
                    {
                        hasReachedTargetRadius = true;
                        // Set final position to exact target radius
                        currentRadius = beamRadius;

                        // Final position update
                        for (int i = 0; i < Math.Min(beamEntities.Count, lineCount); i++)
                        {
                            if (beamEntities[i] == null || !beamEntities[i].IsValid)
                                continue;

                            float startAngle = i * angleStep;
                            float endAngle = ((i + 1) % lineCount) * angleStep;

                            Vector startPosition = GetAngleOnCircle(startAngle, currentRadius, centerPosition);
                            Vector endPosition = GetAngleOnCircle(endAngle, currentRadius, centerPosition);

                            TeleportLaser(beamEntities[i], startPosition, endPosition);
                        }
                    }
                }

                beaconTimerSeconds += 0.1f;

                void CleanupBeams()
                {
                    foreach (var beam in beamEntities)
                    {
                        if (beam != null && beam.IsValid)
                        {
                            beam.Remove();
                        }
                    }
                    beamEntities.Clear();
                }
            }, TimerFlags.REPEAT);

            #region Local Methods
            // Local method: Calculates a point position on a circle at the specified angle
            static Vector GetAngleOnCircle(float angle, float radius, Vector center)
            {
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                return new Vector(x, y, center.Z);
            }

            // Local method: Draws a laser beam between two points
            static CBeam? DrawLaserBetween(Vector start, Vector end, Color color, float beamLifeTime, float width)
            {
                var beam = Utilities.CreateEntityByName<CBeam>("env_beam");
                if (beam == null)
                    return null;

                // Set beam start position
                beam.Teleport(start);

                // Set beam end position
                beam.EndPos.X = end.X;
                beam.EndPos.Y = end.Y;
                beam.EndPos.Z = end.Z;

                // Set beam properties
                beam.RenderMode = RenderMode_t.kRenderTransAdd;
                beam.Render = color;
                beam.Width = width;

                // Spawn the entity
                beam.DispatchSpawn();

                // Schedule beam removal after the specified lifetime
                if (beamLifeTime > 0)
                {
                    AddTimer(beamLifeTime, () =>
                    {
                        if (beam != null && beam.IsValid)
                        {
                            beam.Remove();
                        }
                    });
                }

                return beam;
            }

            // Local method: Moves a laser beam to new positions
            static void TeleportLaser(CBeam beam, Vector start, Vector end)
            {
                if (beam == null || !beam.IsValid)
                    return;

                beam.Teleport(start);
                beam.EndPos.X = end.X;
                beam.EndPos.Y = end.Y;
                beam.EndPos.Z = end.Z;

                // Trigger position update
                Utilities.SetStateChanged(beam, "CBeam", "m_vecEndPos");
            }
            #endregion
        }

        /// <summary>
        /// Slaps a player with damage, random knockback and sound effects
        /// </summary>
        /// <param name="player">Target player controller</param>
        /// <param name="damage">Damage to deal, default is 0</param>
        /// <param name="playSound">Whether to play sound effects, default is true</param>
        public static void SlapPlayer(CCSPlayerController player, int damage = 0, bool playSound = true)
        {
            // Validate player
            if (player?.PlayerPawn?.Value == null || !player.IsValid)
            {
                throw new ArgumentException("Player is invalid");
            }

            if (!player.PawnIsAlive)
            {
                throw new ArgumentException("Player is not in game or is dead");
            }

            var playerPawn = player.PlayerPawn.Value;
            bool shouldKill = false;

            // Handle health reduction
            if (damage > 0)
            {
                var currentHealth = playerPawn.Health;

                if (currentHealth - damage <= 0)
                {
                    playerPawn.Health = 1;
                    shouldKill = true;
                }
                else
                {
                    playerPawn.Health = currentHealth - damage;
                }

                // Sync health changes
                Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
            }

            // Apply random knockback (simulate slap effect)
            if (playerPawn.AbsVelocity != null)
            {
                var currentVelocity = playerPawn.AbsVelocity;
                var random = new Random();

                // Calculate random velocity increment (simulating original code's random calculation)
                var randomX = (random.Next(180) + 50) * (random.Next(2) == 1 ? -1 : 1);
                var randomY = (random.Next(180) + 50) * (random.Next(2) == 1 ? -1 : 1);
                var randomZ = random.Next(200) + 100;

                // Apply new velocity
                var newVelocity = new Vector(
                    currentVelocity.X + randomX,
                    currentVelocity.Y + randomY,
                    currentVelocity.Z + randomZ
                );

                playerPawn.Teleport(null, null, newVelocity);
            }

            // Play slap sound effect
            if (playSound && playerPawn.AbsOrigin != null)
            {
                PlaySlapSound();
            }

            // Record original score (avoid suicide affecting score)
            var originalScore = player.Score;

            // Force kill player if health reaches zero
            if (shouldKill)
            {
                playerPawn.CommitSuicide(false, true);

                // Restore original score
                AddTimer(0.1f, () =>
                {
                    if (player.IsValid)
                    {
                        player.Score = originalScore;
                        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
                    }
                });
            }

            void PlaySlapSound()
            {
                var slapSounds = new[]
                {
                    "Player.DamageKevlar",
                    "Flesh.BulletImpact"
                };

                var random = new Random();
                var selectedSound = slapSounds[random.Next(slapSounds.Length)];

                if (player?.PlayerPawn?.Value != null)
                {
                    player.PlayerPawn.Value.EmitSound(selectedSound);
                }
            }
        }

        /// <summary>
        /// Applies a screen fade effect to a player's viewport with customizable color, timing, and transition behavior.
        /// This method creates visual overlay effects such as damage indicators, death screens, or environmental effects
        /// by sending a fade user message to the client.
        /// </summary>
        /// <param name="player">The target player to apply the screen fade effect to</param>
        /// <param name="color">The RGBA color of the fade effect overlay</param>
        /// <param name="hold">Duration in seconds to maintain the fade effect at full intensity (default: 0.1s)</param>
        /// <param name="fade">Duration in seconds for the fade transition animation (default: 0.2s)</param>
        /// <param name="flags">Fade behavior flags controlling transition type (FadeIn, FadeOut, FadeStayOut)</param>
        /// <param name="withPurge">Whether to clear any existing fade effects before applying the new one (default: true)</param>
        public static void ColorScreen(CCSPlayerController player, Color color, float hold = 0.1f, float fade = 0.2f, FadeFlags flags = FadeFlags.FadeIn, bool withPurge = true)
        {
            // User message ID 106 represents the "Fade" message type in Counter-Strike 2's network protocol
            var fadeMsg = UserMessage.FromId(106);

            fadeMsg.SetInt("duration", Convert.ToInt32(fade * 512));
            fadeMsg.SetInt("hold_time", Convert.ToInt32(hold * 512));

            var flag = (int)flags;
            if (withPurge)
                flag |= (int)FadeFlags.Purge;

            fadeMsg.SetInt("flags", flag);
            fadeMsg.SetInt("color", color.R | color.G << 8 | color.B << 16 | color.A << 24);
            fadeMsg.Send(player);
        }

        public static bool IsPlayerValidAndAlive(CCSPlayerController player) =>
            player.IsValid &&
            !player.IsBot &&
            player.PlayerPawn.Value != null &&
            player.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE;

        public static List<CCSPlayerController> GetAliveHumanPlayers() =>
            Utilities.GetPlayers()
                .Where(player => Utility.IsPlayerValidAndAlive(player))
                .ToList();

        public static void PrintToAllCenter(string message)
        {
            foreach (var player in Utilities.GetPlayers().Where(p =>
                p.IsValid &&
                !p.IsBot &&
                p.PlayerPawn.Value is not null))
            {
                player.PrintToCenter(message);
            }
        }
    }
}
