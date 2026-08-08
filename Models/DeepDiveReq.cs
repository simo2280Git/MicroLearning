using System.Text.Json.Serialization;

namespace MicroLearning.Models
{
    public class DeepDiveReq
    {
        [JsonPropertyName("card_id")]
        public string? IdCard { get; set; }
    }
}
