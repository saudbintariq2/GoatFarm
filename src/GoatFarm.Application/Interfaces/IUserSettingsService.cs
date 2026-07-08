using GoatFarm.Application.ViewModels.Settings;

namespace GoatFarm.Application.Interfaces;

public interface IUserSettingsService
{
    Task<SettingsPageViewModel> GetSettingsPageAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserViewModel>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserViewModel> CreateUserAsync(CreateUserViewModel model, CancellationToken cancellationToken = default);
    Task<UserViewModel?> UpdateUserAsync(string id, UpdateUserViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(string id, ResetPasswordViewModel model, CancellationToken cancellationToken = default);
    Task<PasswordPolicyViewModel> GetPasswordPolicyAsync(CancellationToken cancellationToken = default);
    Task SavePasswordPolicyAsync(PasswordPolicyViewModel model, CancellationToken cancellationToken = default);
    Task<RolePermissionsViewModel> GetRolePermissionsAsync(CancellationToken cancellationToken = default);
    Task SaveRolePermissionsAsync(RolePermissionsViewModel model, CancellationToken cancellationToken = default);
    Task<UserPermissionsViewModel?> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveUserPermissionsAsync(string userId, SaveUserPermissionsViewModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default);
    IReadOnlyList<string> ValidatePassword(string password, PasswordPolicyViewModel? policy = null);
}
