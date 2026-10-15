using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure;
using GoatFarm.Infrastructure.Identity;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class UserAdminPgTests
{
    private sealed class Harness : IAsyncDisposable
    {
        private readonly PostgresTestDatabase _pg;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private Harness(PostgresTestDatabase pg, ServiceProvider provider)
        {
            _pg = pg;
            _provider = provider;
            _scope = provider.CreateAsyncScope();
        }

        public UserManager<ApplicationUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        public IUserSettingsService Settings => _scope.ServiceProvider.GetRequiredService<IUserSettingsService>();

        public static async Task<Harness> CreateAsync()
        {
            var pg = await PostgresTestDatabase.CreateAsync();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = pg.ConnectionString })
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(config);
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<GoatFarmDbContext>();
            var h = new Harness(pg, services.BuildServiceProvider());
            var roles = h._scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in FarmRoles.All)
                if (!await roles.RoleExistsAsync(role))
                    await roles.CreateAsync(new IdentityRole(role));
            return h;
        }

        public async Task<string> AddUserAsync(string email, string role)
        {
            var created = await Settings.CreateUserAsync(new CreateUserViewModel
            {
                FullName = email, Email = email, Password = "Str0ng!Passw0rd", Role = role
            });
            return created.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _pg.DisposeAsync();
        }
    }

    private static UpdateUserViewModel Update(string role, bool locked = false) =>
        new() { FullName = "Changed", Role = role, IsLocked = locked };

    [SkippableFact]
    public async Task LastAdmin_CannotBeDemotedOrLocked()
    {
        await using var h = await Harness.CreateAsync();
        var admin = await h.AddUserAsync("owner@farm.test", FarmRoles.Admin);

        var demote = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.UpdateUserAsync(admin, Update(FarmRoles.Manager)));
        Assert.Contains("administrator", demote.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.UpdateUserAsync(admin, Update(FarmRoles.Admin, locked: true)));

        var user = await h.Users.FindByIdAsync(admin);
        Assert.Contains(FarmRoles.Admin, await h.Users.GetRolesAsync(user!));
        Assert.Null(user!.LockoutEnd);
        Assert.NotEqual("Changed", user.FullName);
    }

    [SkippableFact]
    public async Task Admin_CanBeDemotedWhenAnotherActiveAdminRemains()
    {
        await using var h = await Harness.CreateAsync();
        var first = await h.AddUserAsync("owner@farm.test", FarmRoles.Admin);
        await h.AddUserAsync("partner@farm.test", FarmRoles.Admin);

        var result = await h.Settings.UpdateUserAsync(first, Update(FarmRoles.Manager));

        Assert.Equal(FarmRoles.Manager, result!.Role);
    }

    [SkippableFact]
    public async Task LockedSecondAdmin_DoesNotCountTowardsKeepingAccess()
    {
        await using var h = await Harness.CreateAsync();
        var owner = await h.AddUserAsync("owner@farm.test", FarmRoles.Admin);
        var partner = await h.AddUserAsync("partner@farm.test", FarmRoles.Admin);
        await h.Settings.UpdateUserAsync(partner, Update(FarmRoles.Admin, locked: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.UpdateUserAsync(owner, Update(FarmRoles.Staff)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.DeleteUserAsync(owner));
    }

    [SkippableFact]
    public async Task NonAdminUsers_CanStillBeEditedAndLocked()
    {
        await using var h = await Harness.CreateAsync();
        await h.AddUserAsync("owner@farm.test", FarmRoles.Admin);
        var staff = await h.AddUserAsync("staff@farm.test", FarmRoles.Staff);

        var result = await h.Settings.UpdateUserAsync(staff, Update(FarmRoles.Staff, locked: true));

        Assert.True(result!.IsLocked);
    }
}
