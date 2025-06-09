using System.Collections.Generic;

namespace SearchBenchmarking.Library.DTOs
{
    // Til at holde Id og (relevans) Score sammen
    public class DocumentHit
    {
        public string Id { get; set; }
        public float Score { get; set; }
    }

    public class SearchResult
    {
        public long TotalHits { get; set; } // Antal fundne dokumenter
        public int QueryTimeMs { get; set; } // Forespørgselstid i ms
        public string? ErrorMessage { get; set; }
        public List<DocumentHit> Hits { get; set; } = new List<DocumentHit>();
    }
}