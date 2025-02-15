using CounterStrikeSharp.API.Modules.Utils;

namespace MyProject.Models
{
    public class Position
    {
        public Vector Origin { get; set; }
        public QAngle Rotation { get; set; }
        public Vector Velocity { get; set; }
    }
}
