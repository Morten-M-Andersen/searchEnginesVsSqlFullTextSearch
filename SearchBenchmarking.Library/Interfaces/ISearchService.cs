using SearchBenchmarking.Library.DTOs;
using System.Threading.Tasks;

namespace SearchBenchmarking.Library.Interfaces
{
    public interface ISearchService
    {
        Task<SearchResult> SearchAsync(SearchRequest request);
    }
}