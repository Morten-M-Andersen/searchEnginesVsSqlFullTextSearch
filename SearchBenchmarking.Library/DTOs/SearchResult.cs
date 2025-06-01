namespace SearchBenchmarking.Library.DTOs
{
    public class SearchResult
    {
        public long TotalHits { get; set; } // Antal fundne dokumenter
        public int QueryTimeMs { get; set; } // Forespørgselstid i ms
        public List<string> Ids { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }
    }
}