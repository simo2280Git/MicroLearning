using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using System.Text.Json.Serialization;

namespace MicroLearning.Models.Context
{
    [Table("cards")]
    public class Card : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("topic_id")]
        public Guid TopicId { get; set; } 

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("subtitle")]
        public string Subtitle { get; set; } = string.Empty;

        [Column("body")]
        public string Body { get; set; } = string.Empty;

        [Column("deep_dive")]
        public string? DeepDive { get; set; }
        
        [Column("key_words")]
        [JsonPropertyName("key_words")]
        public string? KeyWords { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Reference(typeof(Topic))]
        [JsonPropertyName("topics")]
        public Topic? Topic { get; set; }
    }
}