using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using System.Runtime.InteropServices;

namespace MyProject.Factories
{
    public static class GrenadeProjectileFactory
    {
        private static readonly Dictionary<Type, GrenadeProjectileConfig> _projectileConfigs = new()
        {
            {
                typeof(CSmokeGrenadeProjectile),
                new GrenadeProjectileConfig
                {
                    WindowsSignature = "48 8B C4 48 89 58 ? 48 89 68 ? 48 89 70 ? 57 41 56 41 57 48 81 EC ? ? ? ? 48 8B B4 24 ? ? ? ? 4D 8B F8",
                    LinuxSignature = "55 4C 89 C1 48 89 E5 41 57 45 89 CF 41 56 49 89 FE",
                    EntityId = 45
                }
            },
            {
                typeof(CFlashbangProjectile),
                new GrenadeProjectileConfig
                {
                    WindowsSignature = "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC ? 48 8B 6C 24 ? 49 8B F8",
                    LinuxSignature = "55 4C 89 C1 48 89 E5 41 57 45 89 CF 41 56 49 89 D6 48 89 F2 48 89 FE 41 55 49 89 FD 41 54 48 8D 3D ? ? ? ? 4D 89 C4 53 48 83 EC ? E8 ? ? ? ? 4C 89 F6",
                    EntityId = 43
                }
            },
            {
                typeof(CDecoyProjectile),
                new GrenadeProjectileConfig
                {
                    WindowsSignature = "48 8B C4 55 56 48 81 EC",
                    LinuxSignature = "55 4C 89 C1 48 89 E5 41 57 45 89 CF 41 56 49 89 D6 48 89 F2 48 89 FE 41 55 49 89 FD 41 54 48 8D 3D ? ? ? ? 4D 89 C4 53 48 83 EC ? E8 ? ? ? ? 45 31 C0",
                    EntityId = 42
                }
            },
            {
                typeof(CMolotovProjectile),
                new GrenadeProjectileConfig
                {
                    WindowsSignature = "48 8B C4 48 89 58 ? 4C 89 40 ? 48 89 48 ? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24",
                    LinuxSignature = "55 48 8D 05 ? ? ? ? 48 89 E5 41 57 41 56 41 55 41 54 49 89 FC 53 48 81 EC ? ? ? ? 4C 8D 35",
                    EntityId = 46
                }
            },
            {
                typeof(CHEGrenadeProjectile),
                new GrenadeProjectileConfig
                {
                    WindowsSignature = "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC ? 48 8B AC 24 ? ? ? ? 49 8B F8",
                    LinuxSignature = "55 4C 89 C1 48 89 E5 41 57 49 89 D7",
                    EntityId = 44
                }
            }
        };

        private static readonly Dictionary<Type, object> _createFunctions = new();

        /// <summary>
        /// Creates a grenade projectile of the specified type
        /// </summary>
        /// <typeparam name="T">The type of grenade projectile to create</typeparam>
        /// <param name="position">The spawn position</param>
        /// <param name="angle">The spawn angle</param>
        /// <param name="velocity">The initial velocity</param>
        /// <returns>The created grenade projectile</returns>
        public static T? Create<T>(Vector position, QAngle angle, Vector velocity)
            where T : CBaseCSGrenadeProjectile
        {
            var projectileType = typeof(T);

            if (!_projectileConfigs.TryGetValue(projectileType, out var config))
            {
                throw new ArgumentException($"Unsupported grenade projectile type: {projectileType.Name}");
            }

            // Get or create the memory function
            if (!_createFunctions.TryGetValue(projectileType, out var createFunc))
            {
                var signature = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? config.LinuxSignature
                    : config.WindowsSignature;

                createFunc = new MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, T>(signature);
                _createFunctions[projectileType] = createFunc;
            }

            var memoryFunc = (MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, T>)createFunc;

            return memoryFunc.Invoke(
                position.Handle,
                angle.Handle,
                velocity.Handle, // Linear Velocity
                velocity.Handle, // Angular Velocity
                IntPtr.Zero,
                config.EntityId
            );
        }
    }

    /// <summary>
    /// Configuration for grenade projectile creation
    /// </summary>
    public class GrenadeProjectileConfig
    {
        public required string WindowsSignature { get; init; }
        public required string LinuxSignature { get; init; }
        public required int EntityId { get; init; }
    }
}
