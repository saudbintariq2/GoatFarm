using GoatFarm.Application.ViewModels.Settings;

namespace GoatFarm.Application.Interfaces;

public interface IPermissionService
{
    Task<string?> GetCurrentUserRoleAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetCurrentUserPermissionsAsync(CancellationToken cancellationToken = default);
    Task<bool> CanAsync(string tab, string action, CancellationToken cancellationToken = default);
}
