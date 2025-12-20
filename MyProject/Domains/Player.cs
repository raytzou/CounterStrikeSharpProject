using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyProject.Domains
{
    [Table("player")]
    public class Player
    {
        [Key]
        [Column("steam_id")]
        public ulong SteamId { get; set; }
        [Column("player_name")]
        public string PlayerName { get; set; } = null!;
        [Column("ip_address")]
        public string IpAddress { get; set; } = null!;
        [Column("last_time_connect")]
        public DateTime LastTimeConnect { get; set; }
        [Column("default_skin_model_path")]
        public string DefaultSkinModelPath { get; set; } = null!;
        [Column("volume")]
        public byte Volume { get; set; }
        [Column("ss_volume")]
        public byte SaySoundVolume { get; set; }
        [Column("language")]
        public string? Language { get; set; }

        // navigation properties
        public virtual ICollection<PlayerSkin> PlayerSkins { get; set; } = new List<PlayerSkin>();
    }
}
