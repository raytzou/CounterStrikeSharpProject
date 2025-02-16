using CounterStrikeSharp.API.Modules.Utils;

namespace MyProject.Models
{
    public class Position
    {
        public Vector Origin { get; set; } = null!;
        public QAngle Rotation { get; set; } = null!;
        public Vector Velocity { get; set; } = null!;
    }
}
