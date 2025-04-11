using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyProject.Domains
{
    [Table("player_skin")]
    public class PlayerSkin
    {
        [Key]
        public Guid Id { get; set; }

        [Column("steam_id")]
        public ulong SteamId { get; set; }

        [ForeignKey(nameof(SteamId))]
        public virtual Player Player { get; set; } = null!;

        [Column("skin_name")]
        public string SkinName { get; set; } = null!;

        [Column("acquired_at")]
        public DateTime AcquiredAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }
}
