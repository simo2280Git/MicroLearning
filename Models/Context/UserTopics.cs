using MicroLearning.Models.Context;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace MicroLearning.Models
{
    [Table("user_topics")]
    public class UserTopics : BaseModel
    {
        [PrimaryKey("user_id", false)]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [PrimaryKey("topic_id", false)]
        [Column("topic_id")]
        public Guid TopicId { get; set; }

        [Column("weight")]
        public float Weight { get; set; }

        [Reference(typeof(Topic))]
        public Topic? Topic { get; set; }
    }
}