namespace GoatFarm.Application.Interfaces;

public interface ILookupService
{
    Task<IReadOnlyList<string>> GetListAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> AddOptionAsync(string key, string value, CancellationToken cancellationToken = default);
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
