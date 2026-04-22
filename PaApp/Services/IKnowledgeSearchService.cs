using PaApp.Models;

namespace PaApp.Services;

public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<KnowledgeDocument>> SearchAsync(string query, int take = 5, CancellationToken cancellationToken = default);
}
