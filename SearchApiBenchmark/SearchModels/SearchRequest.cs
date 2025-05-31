using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SearchApiBenchmark.SearchModels;
public class SearchRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; } = 50;

    [JsonPropertyName("from")]
    public int From { get; set; } = 0;

    [JsonPropertyName("unitNo")]
    public string? UnitNo { get; set; }
}