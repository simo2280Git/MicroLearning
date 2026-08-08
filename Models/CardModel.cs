namespace MicroLearning.Models
{
    public class CardModel
    {
        public Guid Id { get; set; }

        public Guid TopicId { get; set; }

        public string? TopicName { get; set; }

        public string? Title { get; set; }

        public string? Subtitle { get; set; }

        public string? Body { get; set; }

        public string? DeepDive { get; set; }

        public string? KeyWords { get; set; }
    }
}
