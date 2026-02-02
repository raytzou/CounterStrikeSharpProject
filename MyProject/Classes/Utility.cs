using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using MyProject.Models;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

namespace MyProject.Classes
{
    public class Utility
    {
        public static IEnumerable<string> MapsInPhysicalDirectory => _mapsInPhysicalDirectory;
        public static IEnumerable<string> MapsFromWorkshop => _mapsFromWorkShop;
        public static IEnumerable<string> AllMaps => _mapsInPhysicalDirectory.Concat(_mapsFromWorkShop);
        public static Dictionary<string, SkinInfo> WorkshopSkins => _workshopSkins;
        public static Music SoundEvent => _soundEvent;
        public static List<(string EntityName, string DisplayName, int Price)> WeaponMenu => _weaponMenu;

        private static Music _soundEvent;
        private static List<string> _mapsFromWorkShop;
        private static List<string> _mapsInPhysicalDirectory;
        private static readonly Dictionary<CsItem, string> _enumValue;
        private static readonly Dictionary<string, SkinInfo> _workshopSkins;
        private static readonly Dictionary<string, string> _weaponNameMappings = new()
        {
            ["weapon_m4a1_silencer"] = "weapon_m4a1",
            ["m4a1-s"] = "weapon_m4a1_silencer",
            ["m4a1s"] = "weapon_m4a1_silencer",
            ["m4a1"] = "weapon_m4a1_silencer",
            ["m4a4"] = "weapon_m4a1",
            ["usp-s"] = "weapon_usp_silencer",
            ["usps"] = "weapon_usp_silencer",
            ["usp"] = "weapon_usp_silencer",
        };
        private static readonly List<(string EntityName, string DisplayName, int Price)> _weaponMenu = new()
        {
            // pistols
            {("weapon_glock", "Glock-18", 200)},
            {("weapon_hkp2000", "P2000", 200)},
            {("weapon_usp_silencer", "USP-S", 200)},
            {("weapon_elite", "Dual Berettas", 300)},
            {("weapon_p250", "P250", 300)},
            {("weapon_tec9", "TEC-9", 500)},
            {("weapon_cz75a", "CZ75-Auto", 500)},
            {("weapon_fiveseven", "Five-SeveN", 500)},
            {("weapon_deagle", "Desert Eagle", 700)},
            {("weapon_revolver", "R8 Revolver ", 600)},
            // SMGs
            {("weapon_mac10", "MAC-10", 1050)},
            {("weapon_mp9", "MP9", 1250)},
            {("weapon_mp7", "MP7", 1500)},
            {("weapon_mp5sd", "MP5-SD", 1500)},
            {("weapon_ump45", "UMP-45", 1200)},
            {("weapon_p90", "P90", 2350)},
            {("weapon_bizon", "PP-Bizon", 1400)},
            // heavy weapons
            {("weapon_nova", "Nova", 1050)},
            {("weapon_xm1014", "XM1014", 2000)},
            {("weapon_sawedoff", "Sawed-Off", 1100)},
            {("weapon_mag7", "MAG-7", 1300)},
            {("weapon_m249", "M249", 5200)},
            {("weapon_negev", "Negev", 1700)},
            // rifles
            {("weapon_galil", "Galil AR", 1800)},
            {("weapon_famas", "FAMAS", 1950)},
            {("weapon_ak47", "AK-47", 2700)},
            {("weapon_m4a1", "M4A4", 2900)},
            {("weapon_m4a1_silencer", "M4A1-S", 2900) },
            {("weapon_ssg08", "SSG 08", 1700)},
            {("weapon_sg556", "SG 553", 3000)},
            {("weapon_aug", "AUG", 3300)},
            {("weapon_awp", "AWP", 4750)},
            {("weapon_g3sg1", "G3SG1", 5000)},
            {("weapon_scar20", "SCAR-20", 5000)},
            // others
            {("weapon_healthshot", "Medi-Shot", 500)},
            //{("weapon_shield", "Riot Shield", 800)},
            //{("weapon_tagrenade", "Tactical Awareness Grenade", 200)},
            {("weapon_c4", "C4", 16000)},
        };

        static Utility()
        {
            _enumValue = [];
            _workshopSkins = [];
            _mapsFromWorkShop = [];
            _mapsInPhysicalDirectory = [];
            _soundEvent = new();

            InitializeEnumValues();
            InitializeMaps();
            InitializeWorkshopSkins();
            InitializeSoundEvent();

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

            static void InitializeSoundEvent()
            {
                var soundsPath = Path.Join(Server.GameDirectory, "sounds.json");
                if (!File.Exists(soundsPath)) throw new Exception("sounds.json could not be found in root folder");

                try
                {
                    var json = File.ReadAllText(soundsPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };

                    _soundEvent = JsonSerializer.Deserialize<Music>(json, options)!;

                    if (_soundEvent == null)
                        throw new Exception("sounds.json deserialization failed");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Invalid JSON format in sounds.json: {ex.Message}");
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
        /// Uses reflection to get the <see cref="EnumMemberAttribute"/> value of a CsItem enum.
        /// </summary>
        /// <param name="item">The enum option of <see cref="CsItem"/>.</param>
        /// <returns><c>string</c> - The item entity name.</returns>
        public static string GetCsItemEnumValue(CsItem item)
        {
            return _enumValue.TryGetValue(item, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Converts CsItem enum to the actual in-game weapon DesignerName.
        /// Handles cases where the enum value differs from the actual weapon name in game.
        /// </summary>
        /// <param name="item">The enum option of <see cref="CsItem">.</param>
        /// <returns><c>string</c> - The actual weapon DesignerName used in game.</returns>
        public static string GetActualWeaponName(CsItem item)
        {
            var enumValue = GetCsItemEnumValue(item);
            return _weaponNameMappings.TryGetValue(enumValue, out var actualName) ? actualName : enumValue;
        }

        /// <summary>
        /// Checks if the actual weapon name matches the expected CsItem enum.
        /// Accounts for discrepancies between enum values and actual in-game weapon names.
        /// </summary>
        /// <param name="expectedItem">The expected weapon as CsItem enum.</param>
        /// <param name="actualWeaponName">The actual weapon DesignerName from the game.</param>
        /// <returns>True if the weapons match, considering name mappings; otherwise, false.</returns>
        public static bool IsWeaponMatch(CsItem expectedItem, string actualWeaponName)
        {
            var expectedName = GetCsItemEnumValue(expectedItem);
            var actualExpectedName = GetActualWeaponName(expectedItem);

            return actualWeaponName == expectedName || actualWeaponName == actualExpectedName;
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
            if (!IsPlayerValidAndAlive(player))
                return;

            if (beamLifeTime <= 0 || beamWidth <= 0 || beamRadius <= 0)
                return;

            var centerPosition = new Vector(
                player.PlayerPawn.Value!.AbsOrigin!.X,
                player.PlayerPawn.Value!.AbsOrigin!.Y,
                player.PlayerPawn.Value!.AbsOrigin!.Z + 35);

            const int lineCount = 20;
            const float radiusIncrement = 10.0f;
            const float updateInterval = 0.1f;

            var beamEntities = new List<CBeam>(lineCount);
            float angleStep = (float)(2.0 * Math.PI) / lineCount;
            float currentRadius = 20.0f;
            float elapsedTime = 0.0f;

            // Create initial beacon circle
            for (int i = 0; i < lineCount; i++)
            {
                var beam = CreateBeam(i, currentRadius);
                if (beam != null)
                {
                    beamEntities.Add(beam);
                }
            }

            // Animation timer with proper cleanup
            CounterStrikeSharp.API.Modules.Timers.Timer? animationTimer = null;
            animationTimer = Main.Instance.AddTimer(updateInterval, () =>
            {
                // Validate player state
                if (!IsPlayerValidAndAlive(player))
                {
                    CleanupBeamsAndTimer();
                    return;
                }

                elapsedTime += updateInterval;

                // Check if animation duration exceeded
                if (elapsedTime >= beamLifeTime)
                {
                    CleanupBeamsAndTimer();
                    return;
                }

                // Animate radius growth
                if (currentRadius < beamRadius)
                {
                    currentRadius = Math.Min(currentRadius + radiusIncrement, beamRadius);
                    UpdateBeamPositions(currentRadius);
                }
            }, TimerFlags.REPEAT);

            #region Local Methods
            // Creates and spawns a beam entity
            CBeam? CreateBeam(int index, float radius)
            {
                float startAngle = index * angleStep;
                float endAngle = ((index + 1) % lineCount) * angleStep;

                var beam = Utilities.CreateEntityByName<CBeam>("env_beam");
                if (beam == null)
                    return null;

                Vector startPos = GetAngleOnCircle(startAngle, radius, centerPosition);
                Vector endPos = GetAngleOnCircle(endAngle, radius, centerPosition);

                beam.Teleport(startPos);
                beam.EndPos.X = endPos.X;
                beam.EndPos.Y = endPos.Y;
                beam.EndPos.Z = endPos.Z;
                beam.RenderMode = RenderMode_t.kRenderTransAdd;
                beam.Render = beamColor;
                beam.Width = beamWidth;
                beam.DispatchSpawn();

                return beam;
            }

            // Updates all beam positions to new radius
            void UpdateBeamPositions(float radius)
            {
                for (int i = 0; i < beamEntities.Count; i++)
                {
                    var beam = beamEntities[i];
                    if (beam == null || !beam.IsValid)
                        continue;

                    float startAngle = i * angleStep;
                    float endAngle = ((i + 1) % lineCount) * angleStep;

                    Vector startPos = GetAngleOnCircle(startAngle, radius, centerPosition);
                    Vector endPos = GetAngleOnCircle(endAngle, radius, centerPosition);

                    beam.Teleport(startPos);
                    beam.EndPos.X = endPos.X;
                    beam.EndPos.Y = endPos.Y;
                    beam.EndPos.Z = endPos.Z;

                    Utilities.SetStateChanged(beam, "CBeam", "m_vecEndPos");
                }
            }

            // Cleanup beams and stop timer
            void CleanupBeamsAndTimer()
            {
                foreach (var beam in beamEntities)
                {
                    if (beam != null && beam.IsValid)
                    {
                        beam.Remove();
                    }
                }
                beamEntities.Clear();

                if (animationTimer != null)
                {
                    animationTimer.Kill();
                }
            }

            // Calculates a point position on a circle at the specified angle
            static Vector GetAngleOnCircle(float angle, float radius, Vector center)
            {
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                return new Vector(x, y, center.Z);
            }
            #endregion
        }

        /// <summary>
        /// Slaps a player with damage, random knockback and sound effects
        /// </summary>
        /// <param name="player">Target player controller</param>
        /// <param name="damage">Damage to deal, default is 0</param>
        /// <param name="playSound">Whether to play sound effects, default is true</param>
        public static void SlapPlayer(CCSPlayerController player, int damage = 0, bool playSound = true, bool randomKnokback = true)
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
            if (playerPawn.AbsVelocity != null && randomKnokback)
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
                Main.Instance.AddTimer(0.1f, () =>
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

        /// <summary>
        /// Sends sound event parameters to modify volume and pitch of an already playing sound
        /// </summary>
        /// <param name="client">The target player</param>
        /// <param name="soundEventId">The sound event ID returned by EmitSound</param>
        /// <param name="volume">Sound volume (0.0 to 1.0)</param>
        /// <param name="pitch">Sound pitch multiplier (default: 1.0)</param>
        /// <returns>Task for async operation</returns>
        public static void SendSoundEventPackage(CCSPlayerController client, uint soundEventId, float volume, float pitch = 1f)
        {
            if (volume == 0.5f && pitch == 1f) return;
            if (volume is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be between 0.0 and 1.0");

            using var memString = new MemoryStream();
            using var writer = new BinaryWriter(memString);

            if (volume != 0.5f)
            {
                writer.Write((UInt32)0xBD6054E9); // volume parameter identifier
                writer.Write((Byte)8); // parameter length
                writer.Write((Byte)4); // data type (float)
                writer.Write((Byte)0); // padding byte
                writer.Write(volume); // actual volume value
            }

            if (pitch != 1f)
            {
                writer.Write((UInt32)0x929A57A4); // pitch parameter identifier
                writer.Write((Byte)8);
                writer.Write((Byte)4);
                writer.Write((Byte)0);
                writer.Write(pitch);
            }

            writer.Close();

            var packedParams = memString.ToArray();

            if (packedParams.Length > 0)
            {
                Server.NextFrame(() =>
                {
                    var userMessage = UserMessage.FromId(210);
                    userMessage.SetInt("soundevent_guid", (int)soundEventId);
                    userMessage.SetBytes("packed_params", packedParams);
                    userMessage.Send(client);
                });
            }
        }

        /// <summary>
        /// Drops a specific weapon from a player's inventory. Optionally removes the dropped weapon from the game world.
        /// </summary>
        /// <param name="player">The player controller who will drop the weapon</param>
        /// <param name="weaponName">The designer name of the weapon to drop (e.g., "weapon_ak47", "weapon_awp")</param>
        /// <param name="removeWeapon">If true, the dropped weapon will be deleted from the game world after 0.1 seconds; if false, the weapon remains on the ground for pickup</param>
        public static void DropWeapon(CCSPlayerController player, string weaponName, bool removeWeapon = false)
        {
            if (!IsHumanValid(player)) return;

            var weaponServices = player.PlayerPawn.Value!.WeaponServices;
            if (weaponServices is null) return;

            var matchedWeapon = weaponServices.MyWeapons
            .FirstOrDefault(w => w.IsValid && w.Value is not null && w.Value.DesignerName == weaponName);

            if (matchedWeapon is not null && matchedWeapon.IsValid)
            {
                weaponServices.ActiveWeapon.Raw = matchedWeapon.Raw;

                var activeWeapon = weaponServices.ActiveWeapon;
                if (activeWeapon is null || !activeWeapon.IsValid || activeWeapon.Value is null || !activeWeapon.Value.IsValid)
                    return;

                var weaponEntity = activeWeapon.Value.As<CBaseEntity>();
                if (weaponEntity is null || !weaponEntity.IsValid)
                    return;

                player.DropActiveWeapon();
                if (removeWeapon) weaponEntity?.AddEntityIOEvent("Kill", weaponEntity, null, string.Empty, 0.1f);
            }
        }

        /// <summary>
        /// Gives a weapon to a player, automatically dropping conflicting weapons of the same category first.
        /// Handles weapon category conflicts by dropping existing pistols when giving a new pistol, 
        /// or dropping main weapons (SMGs, rifles, heavy weapons) when giving a new main weapon.
        /// </summary>
        /// <param name="player">The player controller who will receive the weapon</param>
        /// <param name="weaponEntity">The entity name of the weapon to give (e.g., "weapon_ak47", "weapon_awp", "weapon_glock")</param>
        public static void GiveWeapon(CCSPlayerController player, string weaponEntity)
        {
            if (!IsHumanValid(player))
                throw new ArgumentNullException("Player is null or invalid");
            if (string.IsNullOrEmpty(weaponEntity))
                throw new ArgumentException($"Weapon Entity is null or empty");

            var playerWeaponService = player.PlayerPawn.Value!.WeaponServices
                ?? throw new NullReferenceException("Player Weapon Service is null");

            var pistols = _weaponMenu
                .GetRange(0, 10)
                .Select(weapon => weapon.EntityName);
            var mainWeapons = _weaponMenu
                .GetRange(10, 24)
                .Select(weapon => weapon.EntityName);
            var others = _weaponMenu
                .GetRange(34, 2)
                .Select(weapon => weapon.EntityName);

            if (!pistols.Contains(weaponEntity) && !mainWeapons.Contains(weaponEntity) && !others.Contains(weaponEntity))
                throw new ArgumentException("Weapon Entity is invalid");

            if (!others.Contains(weaponEntity))
            {
                var playerWeapons = playerWeaponService.MyWeapons;
                bool isPistol = pistols.Contains(weaponEntity);

                foreach (var weapon in playerWeapons)
                {
                    if (weapon is null || !weapon.IsValid || weapon.Value is null)
                        continue;

                    var weaponsToCheck = isPistol ? pistols : mainWeapons;
                    if (weaponsToCheck.Contains(weapon.Value.DesignerName))
                        DropWeapon(player, weapon.Value.DesignerName);
                }
            }

            player.GiveNamedItem(weaponEntity);
        }

        /// <summary>
        /// Resolves user input to the correct weapon entity name.
        /// Handles both exact matches and common aliases.
        /// </summary>
        /// <param name="input">User input (e.g., "m4a1-s", "ak", "weapon_ak47")</param>
        /// <returns>The resolved weapon entity name, or null if not found</returns>
        public static string? ResolveWeaponEntity(string input)
        {
            var normalized = input.ToLowerInvariant().Trim();

            // Check if it's a direct alias
            if (_weaponNameMappings.TryGetValue(normalized, out var mapped))
                return mapped;

            // Check if it's already a valid entity name
            if (_weaponMenu.Any(w => w.EntityName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                return normalized;

            // Fuzzy match on entity names (existing behavior)
            var matches = _weaponMenu
                .Where(w => w.EntityName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return matches.Count == 1 ? matches[0].EntityName : null;
        }

        public static bool IsHumanValid([NotNullWhen(true)] CCSPlayerController? player) =>
            IsPlayerValid(player) &&
            !player.IsBot;

        public static bool IsHumanValidAndAlive([NotNullWhen(true)] CCSPlayerController player) =>
            IsHumanValid(player) &&
            player.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE;

        public static List<CCSPlayerController> GetAliveHumans() =>
            Utilities.GetPlayers()
                .Where(IsHumanValidAndAlive)
                .ToList();

        public static List<CCSPlayerController> GetHumans() =>
            Utilities.GetPlayers()
                .Where(IsHumanValid)
                .ToList();

        public static bool IsBotValid([NotNullWhen(true)] CCSPlayerController? bot) =>
            IsPlayerValid(bot) &&
            bot.IsBot;

        public static bool IsBotValidAndAlive([NotNullWhen(true)] CCSPlayerController? bot) =>
            IsBotValid(bot) &&
            bot.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE;

        public static bool IsPlayerValid([NotNullWhen(true)] CCSPlayerController? player) =>
            player != null &&
            player.IsValid &&
            player.PlayerPawn != null &&
            player.PlayerPawn.Value != null &&
            player.PlayerPawn.IsValid;

        public static bool IsPlayerValidAndAlive([NotNullWhen(true)] CCSPlayerController? player) =>
            IsPlayerValid(player) &&
            player.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE;

        public static bool IsEntityValid<T>([NotNullWhen(true)] T entity) where T : CEntityInstance =>
            entity != null &&
            entity.IsValid &&
            entity.Entity != null;

        public static void PrintToAllCenter(string message)
        {
            var humans = GetHumans();

            foreach (var player in humans)
            {
                player.PrintToCenter(message);
            }
        }

        public static void PrintToAllCenterWithHtml(string html)
        {
            var humans = GetHumans();

            foreach (var player in humans)
            {
                player.PrintToCenterHtml(html);
            }
        }

        public static void PrintToChatAllWithColor(string message)
        {
            Server.PrintToChatAll($" {ChatColors.Yellow}{message}");
        }

        public static void PrintToChatWithTeamColor(CCSPlayerController? player, string message)
        {
            player?.PrintToChat($" {GetPlayerTeamChatColor(player)}{message}");
        }

        public static char GetPlayerTeamChatColor(CCSPlayerController? player) => player is null ? ChatColors.Default : ChatColors.ForPlayer(player);

        public static void SetAmmoAmount(CCSWeaponBase weaponBase, int amount)
        {
            if (!IsWeaponBaseValid(weaponBase))
                throw new InvalidOperationException("Weapon Base is invalid");

            weaponBase.VData!.MaxClip1 = amount;
            weaponBase.VData.DefaultClip1 = amount;
            weaponBase.Clip1 = amount;
        }


        public static void SetReservedAmmoAmount(CCSWeaponBase weaponBase, int amount)
        {
            if (!IsWeaponBaseValid(weaponBase))
                throw new InvalidOperationException("Weapon Base is invalid");

            weaponBase.VData!.PrimaryReserveAmmoMax = amount;
            weaponBase.ReserveAmmo[0] = amount;
        }

        public static bool IsWeaponBaseValid([NotNullWhen(true)] CCSWeaponBase weaponBase) =>
            weaponBase is not null && weaponBase.IsValid && weaponBase.Entity is not null && weaponBase.VData is not null;
    }
}
