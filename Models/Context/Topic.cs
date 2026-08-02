using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MicroLearning.Models.Context
{
    [Table("topics")]
    public class Topic : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}