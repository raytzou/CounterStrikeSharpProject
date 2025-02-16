using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MyProject.Models
{
    public class WeaponStatus
    {
        public bool IsActive { get; set; }

        public NetworkedVector<CHandle<CBasePlayerWeapon>> Weapons;
    }
}
