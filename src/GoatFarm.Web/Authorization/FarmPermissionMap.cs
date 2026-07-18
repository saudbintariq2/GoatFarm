using GoatFarm.Domain.Constants;

namespace GoatFarm.Web.Authorization;

public static class FarmPermissionMap
{
    public static readonly IReadOnlyDictionary<string, string> IndexTabs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = FarmTabs.Dashboard,
            ["Goat"] = FarmTabs.Herd,
            ["Breeding"] = FarmTabs.Breeding,
            ["Search"] = FarmTabs.Search,
            ["Feed"] = FarmTabs.Feed,
            ["Milk"] = FarmTabs.Milk,
            ["Finance"] = FarmTabs.Finance,
            ["Vaccine"] = FarmTabs.Vaccines,
            ["Settings"] = FarmTabs.Settings
        };

    public static readonly IReadOnlyDictionary<string, (string Tab, string Action)> ActionPermissions =
        new Dictionary<string, (string Tab, string Action)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard.Export"] = (FarmTabs.Dashboard, FarmActions.View),
            ["Dashboard.Import"] = (FarmTabs.Dashboard, FarmActions.View),
            ["Dashboard.Stats"] = (FarmTabs.Dashboard, FarmActions.View),

            ["Goat.GetAll"] = (FarmTabs.Herd, FarmActions.View),
            ["Goat.Create"] = (FarmTabs.Herd, FarmActions.Add),
            ["Goat.Update"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.Delete"] = (FarmTabs.Herd, FarmActions.Delete),
            ["Goat.BulkMove"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.CreateGroup"] = (FarmTabs.Herd, FarmActions.Add),

            ["Breeding.GetData"] = (FarmTabs.Breeding, FarmActions.View),
            ["Breeding.LookupTag"] = (FarmTabs.Breeding, FarmActions.View),
            ["Breeding.RecordPrep"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RecordCross"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RecordUltrasound"] = (FarmTabs.Breeding, FarmActions.Edit),
            ["Breeding.MarkKidded"] = (FarmTabs.Breeding, FarmActions.Edit),
            ["Breeding.CrossFromPrep"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RemovePrep"] = (FarmTabs.Breeding, FarmActions.Delete),
            ["Breeding.RemoveCross"] = (FarmTabs.Breeding, FarmActions.Delete),

            ["Feed.GetData"] = (FarmTabs.Feed, FarmActions.View),
            ["Feed.UpdatePrice"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.UpdatePlan"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.AddPurchase"] = (FarmTabs.Feed, FarmActions.Add),
            ["Feed.UpdatePurchase"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.DeletePurchase"] = (FarmTabs.Feed, FarmActions.Delete),
            ["Feed.AddFeedType"] = (FarmTabs.Feed, FarmActions.Add),
            ["Feed.DeleteFeedType"] = (FarmTabs.Feed, FarmActions.Delete),
            ["Feed.UpdateStock"] = (FarmTabs.Feed, FarmActions.Edit),

            ["Lookup.Get"] = (FarmTabs.Herd, FarmActions.View),
            ["Lookup.AddOption"] = (FarmTabs.Herd, FarmActions.Add),

            ["Milk.GetData"] = (FarmTabs.Milk, FarmActions.View),
            ["Milk.AddProduction"] = (FarmTabs.Milk, FarmActions.Add),
            ["Milk.UpdateProduction"] = (FarmTabs.Milk, FarmActions.Edit),
            ["Milk.DeleteProduction"] = (FarmTabs.Milk, FarmActions.Delete),
            ["Milk.AddSale"] = (FarmTabs.Milk, FarmActions.Add),
            ["Milk.UpdateSale"] = (FarmTabs.Milk, FarmActions.Edit),
            ["Milk.DeleteSale"] = (FarmTabs.Milk, FarmActions.Delete),
            ["Milk.AddWaste"] = (FarmTabs.Milk, FarmActions.Add),
            ["Milk.UpdateWaste"] = (FarmTabs.Milk, FarmActions.Edit),
            ["Milk.DeleteWaste"] = (FarmTabs.Milk, FarmActions.Delete),

            ["Finance.GetData"] = (FarmTabs.Finance, FarmActions.View),
            ["Finance.AddAsset"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.UpdateAsset"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.DeleteAsset"] = (FarmTabs.Finance, FarmActions.Delete),
            ["Finance.AddIncome"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.UpdateIncome"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.DeleteIncome"] = (FarmTabs.Finance, FarmActions.Delete),
            ["Finance.AddExpense"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.UpdateExpense"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.DeleteExpense"] = (FarmTabs.Finance, FarmActions.Delete),
            ["Finance.AddOwnerInvestment"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.UpdateOwnerInvestment"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.DeleteOwnerInvestment"] = (FarmTabs.Finance, FarmActions.Delete),
            ["Finance.AddRecurringCost"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.UpdateRecurringCost"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.DeleteRecurringCost"] = (FarmTabs.Finance, FarmActions.Delete),

            ["Vaccine.GetData"] = (FarmTabs.Vaccines, FarmActions.View),
            ["Vaccine.Add"] = (FarmTabs.Vaccines, FarmActions.Add),
            ["Vaccine.Update"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.Delete"] = (FarmTabs.Vaccines, FarmActions.Delete),
            ["Vaccine.MarkDone"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.UpdateHistoryBatch"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.DeleteHistoryBatch"] = (FarmTabs.Vaccines, FarmActions.Delete),
            ["Vaccine.SetReminderWindow"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.AddPurchase"] = (FarmTabs.Vaccines, FarmActions.Add),
            ["Vaccine.UpdatePurchase"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.DeletePurchase"] = (FarmTabs.Vaccines, FarmActions.Delete),

            ["Reminder.GetAll"] = (FarmTabs.Vaccines, FarmActions.View),
            ["Reminder.Create"] = (FarmTabs.Vaccines, FarmActions.Add),
            ["Reminder.Update"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Reminder.Delete"] = (FarmTabs.Vaccines, FarmActions.Delete),

            ["Settings.CreateUser"] = (FarmTabs.Settings, FarmActions.Add),
            ["Settings.UpdateUser"] = (FarmTabs.Settings, FarmActions.Edit),
            ["Settings.DeleteUser"] = (FarmTabs.Settings, FarmActions.Delete),
            ["Settings.ResetPassword"] = (FarmTabs.Settings, FarmActions.Edit),
            ["Settings.SavePasswordPolicy"] = (FarmTabs.Settings, FarmActions.Edit),
            ["Settings.SaveRolePermissions"] = (FarmTabs.Settings, FarmActions.Edit),
            ["Settings.GetUserPermissions"] = (FarmTabs.Settings, FarmActions.View),
            ["Settings.SaveUserPermissions"] = (FarmTabs.Settings, FarmActions.Edit)
        };
}
