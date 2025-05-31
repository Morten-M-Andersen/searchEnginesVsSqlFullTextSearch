namespace SearchBenchmarking.Library.DTOs
{
    public class SearchRequest
    {
        public string Query { get; set; }
        public int PageSize { get; set; } = 50; // Eller Rows, Size
        public int StartFrom { get; set; } = 0;  // Eller From, Start
        // Tilføj evt. andre fælles filterparametre her
        // public string? SpecificFieldFilter { get; set; }
    }
}