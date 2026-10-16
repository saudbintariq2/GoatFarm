using GoatFarm.Domain.Constants;

namespace GoatFarm.Web.Authorization;

public static class FarmPermissionMap
{
    /// <summary>Controllers whose actions need only an authenticated user. Every other action must be mapped below.</summary>
    public static readonly IReadOnlySet<string> OpenControllers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Home" };

    public static readonly IReadOnlyDictionary<string, string> IndexTabs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = FarmTabs.Dashboard,
            ["Goat"] = FarmTabs.Herd,
            ["Breeding"] = FarmTabs.Breeding,
            ["Search"] = FarmTabs.Search,
            ["Feed"] = FarmTabs.Feed,
            ["Milk"] = FarmTabs.Products,
            ["Products"] = FarmTabs.Products,
            ["Finance"] = FarmTabs.Finance,
            ["Reports"] = FarmTabs.Reports,
            ["Vaccine"] = FarmTabs.Vaccines,
            ["Settings"] = FarmTabs.Settings,
            ["Tasks"] = FarmTabs.Tasks,
            ["Trading"] = FarmTabs.Trading,
            ["Products"] = FarmTabs.Products,
            ["Projects"] = FarmTabs.Projects,
            ["Admin"] = FarmTabs.Admin,
            ["BackupCenter"] = FarmTabs.Backup
        };

    public static readonly IReadOnlyDictionary<string, (string Tab, string Action)> ActionPermissions =
        new Dictionary<string, (string Tab, string Action)>(StringComparer.OrdinalIgnoreCase)
        {
            // Full backup contains finance/payroll data; import wipes and replaces the whole farm.
            ["Dashboard.Export"] = (FarmTabs.Settings, FarmActions.View),
            ["Dashboard.Import"] = (FarmTabs.Settings, FarmActions.Edit),
            ["Dashboard.Stats"] = (FarmTabs.Dashboard, FarmActions.View),

            ["Goat.GetAll"] = (FarmTabs.Herd, FarmActions.View),
            ["Goat.GetByTag"] = (FarmTabs.Herd, FarmActions.View),
            ["Goat.ParentOptions"] = (FarmTabs.Herd, FarmActions.View),
            ["Goat.Create"] = (FarmTabs.Herd, FarmActions.Add),
            ["Goat.Update"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.Delete"] = (FarmTabs.Herd, FarmActions.Delete),
            ["Goat.BulkMove"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.CreateGroup"] = (FarmTabs.Herd, FarmActions.Add),
            ["Goat.RecordWeight"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.RecordDeath"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.RecordSale"] = (FarmTabs.Herd, FarmActions.Edit),
            ["Goat.SaleFormData"] = (FarmTabs.Herd, FarmActions.View),
            ["Goat.RecoverTreatment"] = (FarmTabs.Herd, FarmActions.Edit),

            ["Breeding.GetData"] = (FarmTabs.Breeding, FarmActions.View),
            ["Breeding.LookupTag"] = (FarmTabs.Breeding, FarmActions.View),
            ["Breeding.RecordPrep"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RecordCross"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RecordUltrasound"] = (FarmTabs.Breeding, FarmActions.Edit),
            ["Breeding.MarkKidded"] = (FarmTabs.Breeding, FarmActions.Edit),
            ["Breeding.CrossFromPrep"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RemovePrep"] = (FarmTabs.Breeding, FarmActions.Delete),
            ["Breeding.RemoveCross"] = (FarmTabs.Breeding, FarmActions.Delete),
            ["Breeding.CheckCross"] = (FarmTabs.Breeding, FarmActions.View),
            ["Breeding.RecordBirth"] = (FarmTabs.Breeding, FarmActions.Add),
            ["Breeding.RecordAbortion"] = (FarmTabs.Breeding, FarmActions.Edit),

            ["Search.Lookup"] = (FarmTabs.Search, FarmActions.View),

            ["Feed.GetData"] = (FarmTabs.Feed, FarmActions.View),
            ["Feed.UpdatePrice"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.UpdatePlan"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.AddPurchase"] = (FarmTabs.Feed, FarmActions.Add),
            ["Feed.UpdatePurchase"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.DeletePurchase"] = (FarmTabs.Feed, FarmActions.Delete),
            ["Feed.AddFeedType"] = (FarmTabs.Feed, FarmActions.Add),
            ["Feed.DeleteFeedType"] = (FarmTabs.Feed, FarmActions.Delete),
            ["Feed.UpdateStock"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.GetMixRecipe"] = (FarmTabs.Feed, FarmActions.View),
            ["Feed.UpdateMixRecipe"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.UpdateMixRecipeForStatus"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.GetFodderPool"] = (FarmTabs.Feed, FarmActions.View),
            ["Feed.UpdateFodderPool"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.AddFodderPoolItem"] = (FarmTabs.Feed, FarmActions.Add),
            ["Feed.RemoveFodderPoolItem"] = (FarmTabs.Feed, FarmActions.Delete),
            ["Feed.GetFeedSettings"] = (FarmTabs.Feed, FarmActions.View),
            ["Feed.UpdateFeedSettings"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.RunAutoUsage"] = (FarmTabs.Feed, FarmActions.Edit),
            ["Feed.SaveStockCheck"] = (FarmTabs.Feed, FarmActions.Edit),

            ["Lookup.Get"] = (FarmTabs.Herd, FarmActions.View),
            ["Lookup.AddOption"] = (FarmTabs.Herd, FarmActions.Add),

            ["Milk.GetData"] = (FarmTabs.Products, FarmActions.View),
            ["Milk.AddProduction"] = (FarmTabs.Products, FarmActions.Add),
            ["Milk.UpdateProduction"] = (FarmTabs.Products, FarmActions.Edit),
            ["Milk.DeleteProduction"] = (FarmTabs.Products, FarmActions.Delete),
            ["Milk.AddSale"] = (FarmTabs.Products, FarmActions.Add),
            ["Milk.UpdateSale"] = (FarmTabs.Products, FarmActions.Edit),
            ["Milk.DeleteSale"] = (FarmTabs.Products, FarmActions.Delete),
            ["Milk.AddWaste"] = (FarmTabs.Products, FarmActions.Add),
            ["Milk.UpdateWaste"] = (FarmTabs.Products, FarmActions.Edit),
            ["Milk.DeleteWaste"] = (FarmTabs.Products, FarmActions.Delete),

            ["Finance.GetData"] = (FarmTabs.Finance, FarmActions.View),
            ["Finance.SalaryReconciliation"] = (FarmTabs.Finance, FarmActions.View),
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
            ["Finance.AddEmployee"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.UpdateEmployee"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.DeleteEmployee"] = (FarmTabs.Finance, FarmActions.Delete),

            ["Reports.GetData"] = (FarmTabs.Reports, FarmActions.View),

            ["Vaccine.GetData"] = (FarmTabs.Vaccines, FarmActions.View),
            ["Vaccine.Add"] = (FarmTabs.Vaccines, FarmActions.Add),
            ["Vaccine.Update"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.Delete"] = (FarmTabs.Vaccines, FarmActions.Delete),
            ["Vaccine.MarkDone"] = (FarmTabs.Vaccines, FarmActions.Edit),
            ["Vaccine.GiveToGroup"] = (FarmTabs.Vaccines, FarmActions.Edit),
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
            ["Settings.SaveUserPermissions"] = (FarmTabs.Settings, FarmActions.Edit),

            ["Tasks.GetData"] = (FarmTabs.Tasks, FarmActions.View),
            ["Tasks.Create"] = (FarmTabs.Tasks, FarmActions.Add),
            ["Tasks.Update"] = (FarmTabs.Tasks, FarmActions.Edit),
            ["Tasks.SetStatus"] = (FarmTabs.Tasks, FarmActions.View),
            ["Tasks.Delete"] = (FarmTabs.Tasks, FarmActions.Delete),

            ["Assistant.Chat"] = (FarmTabs.Assistant, FarmActions.View),

            ["Trading.GetData"] = (FarmTabs.Trading, FarmActions.View),
            ["Trading.SaveMaster"] = (FarmTabs.Trading, FarmActions.Edit),
            ["Trading.SaveEntry"] = (FarmTabs.Trading, FarmActions.Add),
            ["Trading.DeleteEntry"] = (FarmTabs.Trading, FarmActions.Delete),

            ["Products.GetData"] = (FarmTabs.Products, FarmActions.View),
            ["Products.SaveCatalog"] = (FarmTabs.Products, FarmActions.Edit),
            ["Products.SaveWarehouse"] = (FarmTabs.Products, FarmActions.Edit),
            ["Products.SaveStockMovement"] = (FarmTabs.Products, FarmActions.Add),
            ["Products.SaveMilkingSession"] = (FarmTabs.Products, FarmActions.Add),
            ["Products.SaveSlaughter"] = (FarmTabs.Products, FarmActions.Add),
            ["Products.SaveSale"] = (FarmTabs.Products, FarmActions.Add),

            ["Projects.GetData"] = (FarmTabs.Projects, FarmActions.View),
            ["Projects.SaveProject"] = (FarmTabs.Projects, FarmActions.Edit),
            ["Projects.DeleteProject"] = (FarmTabs.Projects, FarmActions.Delete),
            ["Projects.SaveExpenditure"] = (FarmTabs.Projects, FarmActions.Add),
            ["Projects.SaveFixedAsset"] = (FarmTabs.Projects, FarmActions.Add),
            ["Projects.SaveSolarLog"] = (FarmTabs.Projects, FarmActions.Add),

            ["Admin.Notices"] = (FarmTabs.Dashboard, FarmActions.View),
            ["Admin.PendingApprovalCount"] = (FarmTabs.Admin, FarmActions.View),
            ["Admin.Approvals"] = (FarmTabs.Admin, FarmActions.View),
            ["Admin.ReviewApproval"] = (FarmTabs.Admin, FarmActions.Edit),
            ["Admin.ChangePassword"] = (FarmTabs.Admin, FarmActions.Edit),

            ["BackupCenter.Export"] = (FarmTabs.Backup, FarmActions.View),
            ["BackupCenter.Import"] = (FarmTabs.Backup, FarmActions.Edit),

            ["Finance.LedgerData"] = (FarmTabs.Finance, FarmActions.View),
            ["Finance.SaveMoneyAccount"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.SaveLedgerTransaction"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.SaveCustomerInvoice"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.SaveBusinessCustomer"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.SaveSupplierPayable"] = (FarmTabs.Finance, FarmActions.Add),
            ["Finance.SaveBudgetLine"] = (FarmTabs.Finance, FarmActions.Edit),
            ["Finance.SaveProfitPartner"] = (FarmTabs.Finance, FarmActions.Edit),

            ["Vaccine.Treatments"] = (FarmTabs.Vaccines, FarmActions.View),
            ["Vaccine.SaveTreatment"] = (FarmTabs.Vaccines, FarmActions.Add),
            ["Vaccine.DeleteTreatment"] = (FarmTabs.Vaccines, FarmActions.Delete)
        };
}
