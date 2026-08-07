using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MicroLearn.Models;

public class CardsReq
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("requests")]
    public List<TopicItem> Requests { get; set; } = new();
}

public class CardDeepDiveReq
{
    [JsonPropertyName("card_id")]
    public Guid CardId { get; set; }
}

public class TopicItem
{
    [JsonPropertyName("topic_id")]
    public Guid TopicId { get; set; }

    [JsonPropertyName("topic_name")]
    public string TopicName { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}