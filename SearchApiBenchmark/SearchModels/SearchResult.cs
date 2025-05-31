using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SearchApiBenchmark.SearchModels;
public class SearchResult
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("took")]
    public int Took { get; set; }

    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = [];
}