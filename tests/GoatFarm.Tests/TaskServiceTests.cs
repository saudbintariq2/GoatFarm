using GoatFarm.Application.ViewModels.Tasks;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Identity;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests;

public class TaskServiceTests
{
    private static (TaskService Svc, Infrastructure.Persistence.GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        db.Users.AddRange(
            new ApplicationUser { Id = "manager", UserName = "m@x", Email = "m@x", FullName = "Manager" },
            new ApplicationUser { Id = "staff", UserName = "s@x", Email = "s@x", FullName = "Staff" },
            new ApplicationUser { Id = "other", UserName = "o@x", Email = "o@x", FullName = "Other" });
        db.SaveChanges();
        return (new TaskService(db), db);
    }

    [Fact]
    public async Task Create_RejectsUnknownAssignee()
    {
        var (svc, _) = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new SaveTaskViewModel { Title = "Clean pen", AssigneeUserId = "ghost" }, "manager"));
    }

    [Fact]
    public async Task SetStatus_OnlyAssigneeOrEditorCanMoveAssignedTask()
    {
        var (svc, _) = Create();
        var task = await svc.CreateAsync(new SaveTaskViewModel { Title = "Trim hooves", AssigneeUserId = "staff" }, "manager");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetStatusAsync(task.Id, FarmTaskStatus.Completed, "other", canEditAny: false));

        var done = await svc.SetStatusAsync(task.Id, FarmTaskStatus.Completed, "staff", canEditAny: false);
        Assert.Equal(FarmTaskStatus.Completed, done!.Status);
        Assert.NotNull(done.CompletedDisplay);

        var reopened = await svc.SetStatusAsync(task.Id, FarmTaskStatus.ToDo, "other", canEditAny: true);
        Assert.Equal(FarmTaskStatus.ToDo, reopened!.Status);
        Assert.Null(reopened.CompletedDisplay);
    }

    [Fact]
    public async Task Page_CountsKpisFromRealTasks()
    {
        var (svc, _) = Create();
        await svc.CreateAsync(new SaveTaskViewModel { Title = "A", AssigneeUserId = "staff", Priority = FarmTaskPriority.Urgent }, "manager");
        await svc.CreateAsync(new SaveTaskViewModel { Title = "B", DueDate = TestDb.Today.AddDays(-2) }, "manager");
        var c = await svc.CreateAsync(new SaveTaskViewModel { Title = "C" }, "manager");
        await svc.SetStatusAsync(c.Id, FarmTaskStatus.Completed, "manager", canEditAny: true);

        var page = await svc.GetPageAsync("staff", canEdit: false, filter: "mine");

        Assert.Equal(2, page.Kpis.Open);
        Assert.Equal(1, page.Kpis.Mine);
        Assert.Equal(1, page.Kpis.Urgent);
        Assert.Equal(1, page.Kpis.Overdue);
        Assert.Equal(1, page.Kpis.CompletedLast30Days);
        Assert.Single(page.ToDo);
        Assert.Equal("A", page.ToDo[0].Title);
    }
}
