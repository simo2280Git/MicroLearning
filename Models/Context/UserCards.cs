using MicroLearning.Models.Context;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MicroLearn.Models
{
    [Table("user_cards")]
    public class UserCards : BaseModel
    {
        [PrimaryKey("user_id", false)]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [PrimaryKey("card_id", false)]
        [Column("card_id")]
        public Guid CardId { get; set; }

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }
        
        [Column("is_personal_feed")]
        public bool IsPersonalFeed { get; set; }

        [Reference(typeof(Card))]
        public Card? Card { get; set; }
    }
}