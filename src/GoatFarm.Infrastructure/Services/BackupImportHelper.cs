using System.Text.Json;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

internal static class BackupImportHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsV19Format(JsonElement root)
    {
        if (!root.TryGetProperty("goats", out var goats) || goats.ValueKind != JsonValueKind.Array || goats.GetArrayLength() == 0)
            return false;

        var first = goats[0];
        return first.TryGetProperty("tag", out _) && !first.TryGetProperty("Tag", out _);
    }

    public static async Task ClearFarmDataAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        context.VaccinationHistories.RemoveRange(await context.VaccinationHistories.ToListAsync(cancellationToken));
        context.Goats.RemoveRange(await context.Goats.ToListAsync(cancellationToken));
        context.FeedPlanItems.RemoveRange(await context.FeedPlanItems.ToListAsync(cancellationToken));
        context.FeedPlans.RemoveRange(await context.FeedPlans.ToListAsync(cancellationToken));
        context.FeedPurchases.RemoveRange(await context.FeedPurchases.ToListAsync(cancellationToken));
        context.FeedPrices.RemoveRange(await context.FeedPrices.ToListAsync(cancellationToken));
        context.GoatGroups.RemoveRange(await context.GoatGroups.ToListAsync(cancellationToken));
        context.RecurringCosts.RemoveRange(await context.RecurringCosts.ToListAsync(cancellationToken));
        context.VaccinePurchases.RemoveRange(await context.VaccinePurchases.ToListAsync(cancellationToken));
        context.Assets.RemoveRange(await context.Assets.ToListAsync(cancellationToken));
        context.Incomes.RemoveRange(await context.Incomes.ToListAsync(cancellationToken));
        context.Expenses.RemoveRange(await context.Expenses.ToListAsync(cancellationToken));
        context.OwnerInvestments.RemoveRange(await context.OwnerInvestments.ToListAsync(cancellationToken));
        context.MilkProductions.RemoveRange(await context.MilkProductions.ToListAsync(cancellationToken));
        context.MilkSales.RemoveRange(await context.MilkSales.ToListAsync(cancellationToken));
        context.MilkWastes.RemoveRange(await context.MilkWastes.ToListAsync(cancellationToken));
        context.Vaccines.RemoveRange(await context.Vaccines.ToListAsync(cancellationToken));
        context.Reminders.RemoveRange(await context.Reminders.ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task ImportAsync(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        if (IsV19Format(root))
            await ImportV19Async(context, root, cancellationToken);
        else
            await ImportMvcAsync(context, root, cancellationToken);
    }

    private static async Task ImportV19Async(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        var groupNames = ReadStringArray(root, "groups");
        var groupMap = await ImportGroupsAsync(context, groupNames, cancellationToken);

        var feedTypeMap = BuildFeedTypeMap(root);
        await ImportFeedPricesV19Async(context, root, feedTypeMap, cancellationToken);
        await ImportFeedPlansV19Async(context, root, feedTypeMap, cancellationToken);

        var goatIdMap = await ImportGoatsV19Async(context, root, groupMap, cancellationToken);

        var vaccineIdMap = await ImportVaccinesV19Async(context, root, cancellationToken);
        await ImportVaccinationLogV19Async(context, root, goatIdMap, vaccineIdMap, cancellationToken);

        ImportSimpleList(root, "assets", ImportAsset);
        ImportSimpleList(root, "incomes", ImportIncome);
        ImportSimpleList(root, "expenses", ImportExpense);
        ImportSimpleList(root, "ownerInv", ImportOwnerInvestment);
        ImportSimpleList(root, "recurringCosts", ImportRecurringCost);
        ImportSimpleList(root, "vaccineBuys", ImportVaccinePurchase);
        ImportSimpleList(root, "feedBuys", el => ImportFeedPurchaseV19(context, el, feedTypeMap));
        ImportSimpleList(root, "milkProd", ImportMilkProduction);
        ImportSimpleList(root, "milkSales", ImportMilkSale);
        ImportSimpleList(root, "reminders", ImportReminderV19);

        await ImportLookupListsV19Async(context, root, cancellationToken);
        await ImportRemindDaysAsync(context, root, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        void ImportSimpleList<T>(JsonElement doc, string key, Func<JsonElement, T> map) where T : class
        {
            if (!doc.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
            foreach (var item in arr.EnumerateArray())
                context.Add(map(item));
        }
    }

    private static async Task ImportMvcAsync(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        var groupMap = await ImportGroupsAsync(context, ReadStringArray(root, "groups"), cancellationToken);

        if (root.TryGetProperty("feedPrices", out var feedPrices) && feedPrices.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in feedPrices.EnumerateArray())
            {
                context.FeedPrices.Add(new FeedPrice
                {
                    FeedType = GetString(item, "feedType", "FeedType") ?? "",
                    DisplayName = GetString(item, "displayName", "DisplayName") ?? "",
                    PricePerKg = GetDecimal(item, "pricePerKg", "PricePerKg"),
                    StockKg = GetDecimal(item, "stockKg", "StockKg")
                });
            }
        }
        else
        {
            var feedTypeMap = BuildFeedTypeMap(root);
            await ImportFeedPricesV19Async(context, root, feedTypeMap, cancellationToken);
        }

        if (root.TryGetProperty("feedPlans", out var plans) && plans.ValueKind == JsonValueKind.Array)
        {
            foreach (var planEl in plans.EnumerateArray())
            {
                var status = ParseStatus(GetString(planEl, "statusKey", "StatusKey"));
                var plan = new FeedPlan
                {
                    StatusKey = status,
                    MedicineCostPerGoatPerMonth = GetDecimal(planEl, "medicineCostPerGoatPerMonth", "MedicineCostPerGoatPerMonth")
                };
                if (planEl.TryGetProperty("items", out var items) || planEl.TryGetProperty("Items", out items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        plan.Items.Add(new FeedPlanItem
                        {
                            FeedType = GetString(item, "feedType", "FeedType") ?? "",
                            GramsPerDay = GetInt(item, "gramsPerDay", "GramsPerDay")
                        });
                    }
                }
                context.FeedPlans.Add(plan);
            }
        }
        else
        {
            var feedTypeMap = BuildFeedTypeMap(root);
            await ImportFeedPlansV19Async(context, root, feedTypeMap, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        var goatTagMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("goats", out var goats) && goats.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in goats.EnumerateArray())
            {
                var goat = MapGoatFromJson(g, groupMap);
                context.Goats.Add(goat);
                await context.SaveChangesAsync(cancellationToken);
                goatTagMap[goat.Tag] = goat.Id;
            }
        }

        var vaccineNameMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("vaccines", out var vaccines) && vaccines.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vaccines.EnumerateArray())
            {
                var vaccine = MapVaccineFromJson(v);
                context.Vaccines.Add(vaccine);
                await context.SaveChangesAsync(cancellationToken);
                vaccineNameMap[vaccine.Name] = vaccine.Id;
            }
        }

        ImportMvcCollection(root, "assets", ImportAsset);
        ImportMvcCollection(root, "incomes", ImportIncome);
        ImportMvcCollection(root, "expenses", ImportExpense);
        ImportMvcCollection(root, "ownerInv", ImportOwnerInvestment);
        ImportMvcCollection(root, "recurringCosts", ImportRecurringCost);
        ImportMvcCollection(root, "vaccineBuys", ImportVaccinePurchase);
        ImportMvcCollection(root, "milkProd", ImportMilkProduction);
        ImportMvcCollection(root, "milkSales", ImportMilkSale);
        ImportMvcCollection(root, "milkWastes", ImportMilkWaste);
        ImportMvcCollection(root, "reminders", ImportReminderMvc);

        if (root.TryGetProperty("feedBuys", out var feedBuys) && feedBuys.ValueKind == JsonValueKind.Array)
        {
            var nameToKey = await context.FeedPrices.AsNoTracking()
                .ToDictionaryAsync(p => p.DisplayName, p => p.FeedType, StringComparer.OrdinalIgnoreCase, cancellationToken);
            foreach (var item in feedBuys.EnumerateArray())
                context.FeedPurchases.Add(MapFeedPurchaseFromJson(item, nameToKey));
        }

        if (root.TryGetProperty("vaccLog", out var vaccLog) && vaccLog.ValueKind == JsonValueKind.Array)
        {
            var goatIdByOld = BuildGoatIdRemap(root, goatTagMap);
            var vaccineIdByOld = BuildVaccineIdRemap(root, vaccineNameMap);
            foreach (var entry in vaccLog.EnumerateArray())
            {
                var oldGoatId = GetString(entry, "goatId", "GoatId") ?? "";
                var oldVaccineId = GetString(entry, "vaccineId", "VaccineId") ?? "";
                if (!TryResolveGoatId(entry, goatTagMap, goatIdByOld, out var goatId)) continue;
                if (!TryResolveVaccineId(entry, vaccineNameMap, vaccineIdByOld, out var vaccineId)) continue;
                context.VaccinationHistories.Add(new VaccinationHistory
                {
                    GoatId = goatId,
                    VaccineId = vaccineId,
                    VaccinationDate = ParseDateRequired(entry, "date", "VaccinationDate", "vaccinationDate")
                });
            }
        }

        await ImportLookupSettingsAsync(context, root, cancellationToken);
        await ImportRemindDaysAsync(context, root, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        void ImportMvcCollection<T>(JsonElement doc, string key, Func<JsonElement, T> map) where T : class
        {
            if (!doc.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
            foreach (var item in arr.EnumerateArray())
                context.Add(map(item));
        }
    }

    private static async Task<Dictionary<string, int>> ImportGroupsAsync(
        GoatFarmDbContext context, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var group = new GoatGroup { Name = name.Trim() };
            context.GoatGroups.Add(group);
            await context.SaveChangesAsync(cancellationToken);
            map[name.Trim()] = group.Id;
        }
        return map;
    }

    private static Dictionary<string, (string Key, string Name)> BuildFeedTypeMap(JsonElement root)
    {
        var map = new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, name) in FeedTypes.All)
            map[key] = (key, name);

        if (root.TryGetProperty("FEEDS", out var feeds) && feeds.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in feeds.EnumerateArray())
            {
                var key = GetString(f, "k", "K") ?? "";
                var name = GetString(f, "n", "N") ?? key;
                if (!string.IsNullOrEmpty(key))
                    map[key] = (key, name);
            }
        }

        if (root.TryGetProperty("feedPrices", out var feedPrices) && feedPrices.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in feedPrices.EnumerateArray())
            {
                var key = GetString(f, "feedType", "FeedType") ?? "";
                var name = GetString(f, "displayName", "DisplayName") ?? key;
                if (!string.IsNullOrEmpty(key))
                    map[key] = (key, name);
            }
        }

        return map;
    }

    private static async Task ImportFeedPricesV19Async(
        GoatFarmDbContext context, JsonElement root,
        Dictionary<string, (string Key, string Name)> feedTypeMap, CancellationToken cancellationToken)
    {
        var prices = ReadDecimalDictionary(root, "prices");
        var stock = ReadDecimalDictionary(root, "feedStock");

        foreach (var (key, info) in feedTypeMap)
        {
            context.FeedPrices.Add(new FeedPrice
            {
                FeedType = info.Key,
                DisplayName = info.Name,
                PricePerKg = prices.GetValueOrDefault(key),
                StockKg = stock.GetValueOrDefault(key)
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task ImportFeedPlansV19Async(
        GoatFarmDbContext context, JsonElement root,
        Dictionary<string, (string Key, string Name)> feedTypeMap, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("plans", out var plans) || plans.ValueKind != JsonValueKind.Object)
            return;

        foreach (var statusProp in plans.EnumerateObject())
        {
            if (statusProp.Value.ValueKind != JsonValueKind.Object) continue;
            var status = ParseStatus(statusProp.Name);
            var plan = new FeedPlan
            {
                StatusKey = status,
                MedicineCostPerGoatPerMonth = GetDecimal(statusProp.Value, "med", "Med")
            };

            foreach (var feedKey in feedTypeMap.Keys)
            {
                var grams = 0;
                if (statusProp.Value.TryGetProperty(feedKey, out var gramsEl))
                    grams = gramsEl.ValueKind == JsonValueKind.Number ? gramsEl.GetInt32() : 0;
                plan.Items.Add(new FeedPlanItem { FeedType = feedKey, GramsPerDay = grams });
            }

            context.FeedPlans.Add(plan);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, int>> ImportGoatsV19Async(
        GoatFarmDbContext context, JsonElement root,
        Dictionary<string, int> groupMap, CancellationToken cancellationToken)
    {
        var idMap = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!root.TryGetProperty("goats", out var goats) || goats.ValueKind != JsonValueKind.Array)
            return idMap;

        foreach (var g in goats.EnumerateArray())
        {
            var oldId = GetIdKey(g) ?? "";
            var goat = new Goat
            {
                Tag = GetString(g, "tag", "Tag") ?? "",
                Name = GetString(g, "name", "Name"),
                Comment = GetString(g, "note", "Comment", "comment"),
                Breed = GetString(g, "breed", "Breed") ?? "",
                Gender = ParseGender(GetString(g, "gender", "Gender")),
                Status = ParseStatus(GetString(g, "status", "Status")),
                Source = ParseSource(GetString(g, "source", "Source")),
                PurchasePrice = GetDecimal(g, "price", "PurchasePrice", "purchasePrice"),
                EventDate = ParseDateRequired(g, "date", "EventDate", "eventDate"),
                PrepCrossDate = ParseDate(g, "prepCross", "PrepCrossDate", "prepCrossDate"),
                MatedDate = ParseDate(g, "matedDate", "MatedDate", "matedDate"),
                BuckTag = GetString(g, "buck", "BuckTag", "buckTag"),
                KidsCount = GetNullableInt(g, "kids", "KidsCount", "kidsCount"),
                UltrasoundDate = ParseDate(g, "usDate", "UltrasoundDate", "ultrasoundDate")
            };

            var groupName = GetString(g, "group", "Group");
            if (!string.IsNullOrWhiteSpace(groupName) && groupMap.TryGetValue(groupName.Trim(), out var groupId))
                goat.GroupId = groupId;

            context.Goats.Add(goat);
            await context.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrEmpty(oldId))
                idMap[oldId] = goat.Id;
        }

        return idMap;
    }

    private static Goat MapGoatFromJson(JsonElement g, Dictionary<string, int> groupMap)
    {
        var goat = new Goat
        {
            Tag = GetString(g, "tag", "Tag") ?? "",
            Name = GetString(g, "name", "Name"),
            Comment = GetString(g, "comment", "Comment", "note"),
            Breed = GetString(g, "breed", "Breed") ?? "",
            Gender = ParseGender(GetString(g, "gender", "Gender")),
            Status = ParseStatus(GetString(g, "status", "Status")),
            Source = ParseSource(GetString(g, "source", "Source")),
            PurchasePrice = GetDecimal(g, "purchasePrice", "PurchasePrice", "price"),
            EventDate = ParseDateRequired(g, "eventDate", "EventDate", "date"),
            PrepCrossDate = ParseDate(g, "prepCrossDate", "PrepCrossDate", "prepCross"),
            MatedDate = ParseDate(g, "matedDate", "MatedDate", "matedDate"),
            BuckTag = GetString(g, "buckTag", "BuckTag", "buck"),
            KidsCount = GetNullableInt(g, "kidsCount", "KidsCount", "kids"),
            UltrasoundDate = ParseDate(g, "ultrasoundDate", "UltrasoundDate", "usDate")
        };

        var groupName = ResolveGroupName(g);
        if (!string.IsNullOrWhiteSpace(groupName) && groupMap.TryGetValue(groupName.Trim(), out var groupId))
            goat.GroupId = groupId;

        return goat;
    }

    private static string? ResolveGroupName(JsonElement g)
    {
        if (g.TryGetProperty("group", out var groupObj))
        {
            if (groupObj.ValueKind == JsonValueKind.Object)
                return GetString(groupObj, "name", "Name");
            if (groupObj.ValueKind == JsonValueKind.String)
                return groupObj.GetString();
        }

        return GetString(g, "Group");
    }

    private static async Task<Dictionary<string, int>> ImportVaccinesV19Async(
        GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        var idMap = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!root.TryGetProperty("vaccines", out var vaccines) || vaccines.ValueKind != JsonValueKind.Array)
            return idMap;

        foreach (var v in vaccines.EnumerateArray())
        {
            var oldId = GetIdKey(v) ?? "";
            var vaccine = MapVaccineFromJson(v);
            context.Vaccines.Add(vaccine);
            await context.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrEmpty(oldId))
                idMap[oldId] = vaccine.Id;
        }

        return idMap;
    }

    private static Vaccine MapVaccineFromJson(JsonElement v)
    {
        var ruleType = ParseRuleType(GetString(v, "type", "RuleType", "ruleType"));
        var vaccine = new Vaccine
        {
            Name = GetString(v, "name", "Name") ?? "",
            Scope = ParseScope(GetString(v, "scope", "Scope")),
            RuleType = ruleType
        };

        if (ruleType == VaccineRuleType.Age)
            vaccine.Days = GetNullableInt(v, "ageDays", "Days", "days") ?? GetInt(v, "val", "Val");
        else
            vaccine.Months = GetNullableInt(v, "months", "Months") ?? GetInt(v, "val", "Val");

        return vaccine;
    }

    private static async Task ImportVaccinationLogV19Async(
        GoatFarmDbContext context, JsonElement root,
        Dictionary<string, int> goatIdMap, Dictionary<string, int> vaccineIdMap, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("vaccLog", out var log) || log.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in log.EnumerateArray())
        {
            var goatKey = GetString(entry, "goatId", "GoatId") ?? "";
            var vaccineKey = GetString(entry, "vaccineId", "VaccineId") ?? "";
            if (!goatIdMap.TryGetValue(goatKey, out var goatId)) continue;
            if (!vaccineIdMap.TryGetValue(vaccineKey, out var vaccineId)) continue;

            context.VaccinationHistories.Add(new VaccinationHistory
            {
                GoatId = goatId,
                VaccineId = vaccineId,
                VaccinationDate = ParseDateRequired(entry, "date", "VaccinationDate", "vaccinationDate")
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Asset ImportAsset(JsonElement el) => new()
    {
        Name = GetString(el, "name", "Name") ?? "",
        Type = GetString(el, "type", "Type") ?? "",
        Cost = GetDecimal(el, "cost", "Cost"),
        PurchaseDate = ParseDate(el, "purchaseDate", "PurchaseDate"),
        Comment = GetString(el, "note", "Comment", "comment")
    };

    private static Income ImportIncome(JsonElement el) => new()
    {
        Type = GetString(el, "type", "Type") ?? "",
        Amount = GetDecimal(el, "amount", "Amount"),
        Date = ParseDateRequired(el, "date", "Date"),
        Comment = GetString(el, "note", "Comment", "comment")
    };

    private static Expense ImportExpense(JsonElement el) => new()
    {
        Type = GetString(el, "type", "Type") ?? "",
        Amount = GetDecimal(el, "amount", "Amount"),
        Date = ParseDateRequired(el, "date", "Date"),
        Comment = GetString(el, "note", "Comment", "comment")
    };

    private static OwnerInvestment ImportOwnerInvestment(JsonElement el) => new()
    {
        Note = GetString(el, "note", "Note") ?? "",
        Amount = GetDecimal(el, "amount", "Amount"),
        Date = ParseDateRequired(el, "date", "Date")
    };

    private static RecurringCost ImportRecurringCost(JsonElement el) => new()
    {
        Name = GetString(el, "name", "Name") ?? "",
        Amount = GetDecimal(el, "amount", "Amount"),
        Period = ParseRecurringPeriod(GetString(el, "period", "Period"))
    };

    private static VaccinePurchase ImportVaccinePurchase(JsonElement el) => new()
    {
        Date = ParseDateRequired(el, "date", "Date"),
        Name = GetString(el, "name", "Name") ?? "",
        Qty = GetDecimal(el, "qty", "Qty"),
        Unit = GetString(el, "unit", "Unit") ?? "",
        Amount = GetDecimal(el, "amount", "Amount"),
        Comment = GetString(el, "note", "Comment", "comment")
    };

    private static FeedPurchase ImportFeedPurchaseV19(
        GoatFarmDbContext context, JsonElement el, Dictionary<string, (string Key, string Name)> feedTypeMap)
    {
        var feedLabel = GetString(el, "feed", "FeedType", "feedType") ?? "";
        var feedType = ResolveFeedType(feedLabel, feedTypeMap);
        return new FeedPurchase
        {
            Date = ParseDateRequired(el, "date", "Date"),
            FeedType = feedType,
            Kg = GetDecimal(el, "kg", "Kg"),
            RatePerKg = GetDecimal(el, "rate", "RatePerKg", "ratePerKg"),
            Amount = GetDecimal(el, "amount", "Amount"),
            Comment = GetString(el, "note", "Comment", "comment")
        };
    }

    private static FeedPurchase MapFeedPurchaseFromJson(
        JsonElement el, Dictionary<string, string> displayNameToFeedType)
    {
        var feedType = GetString(el, "feedType", "FeedType") ?? "";
        var feedDisplay = GetString(el, "feed", "Feed") ?? "";
        if (string.IsNullOrEmpty(feedType) && !string.IsNullOrEmpty(feedDisplay))
            displayNameToFeedType.TryGetValue(feedDisplay, out feedType!);

        return new FeedPurchase
        {
            Date = ParseDateRequired(el, "date", "Date"),
            FeedType = feedType,
            Kg = GetDecimal(el, "kg", "Kg"),
            RatePerKg = GetDecimal(el, "ratePerKg", "RatePerKg", "rate"),
            Amount = GetDecimal(el, "amount", "Amount"),
            Comment = GetString(el, "comment", "Comment", "note")
        };
    }

    private static MilkProduction ImportMilkProduction(JsonElement el) => new()
    {
        Date = ParseDateRequired(el, "date", "Date"),
        Breed = GetString(el, "breed", "Breed") ?? "Mixed",
        Liters = GetDecimal(el, "liters", "Liters"),
        Comment = GetString(el, "note", "Comment", "comment")
    };

    private static MilkSale ImportMilkSale(JsonElement el) => new()
    {
        Date = ParseDateRequired(el, "date", "Date"),
        Liters = GetDecimal(el, "liters", "Liters"),
        Rate = GetDecimal(el, "rate", "Rate"),
        Amount = GetDecimal(el, "amount", "Amount"),
        Comment = GetString(el, "note", "Comment", "comment")
    };

    private static MilkWaste ImportMilkWaste(JsonElement el) => new()
    {
        Date = ParseDateRequired(el, "date", "Date"),
        Liters = GetDecimal(el, "liters", "Liters"),
        Notes = GetString(el, "notes", "Notes", "note")
    };

    private static Reminder ImportReminderV19(JsonElement el) => new()
    {
        Title = GetString(el, "note", "Title", "title") ?? "",
        Scope = ParseScope(GetString(el, "scope", "Scope")),
        ReminderDate = ParseDateRequired(el, "date", "ReminderDate", "reminderDate")
    };

    private static Reminder ImportReminderMvc(JsonElement el) => new()
    {
        Title = GetString(el, "title", "Title") ?? "",
        Scope = ParseScope(GetString(el, "scope", "Scope")),
        ReminderDate = ParseDateRequired(el, "reminderDate", "ReminderDate", "date")
    };

    private static async Task ImportLookupListsV19Async(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        await SetLookupListAsync(context, LookupSettingKeys.IncomeTypes, ReadStringArray(root, "incomeTypes"), cancellationToken);
        await SetLookupListAsync(context, LookupSettingKeys.ExpenseTypes, ReadStringArray(root, "expenseTypes"), cancellationToken);
        await SetLookupListAsync(context, LookupSettingKeys.AssetTypes, ReadStringArray(root, "assetTypes"), cancellationToken);
        await SetLookupListAsync(context, LookupSettingKeys.Breeds, ReadStringArray(root, "breedNames"), cancellationToken);
        await SetLookupListAsync(context, LookupSettingKeys.VaccineNames, ReadStringArray(root, "vaccineNames"), cancellationToken);
        await SetLookupListAsync(context, LookupSettingKeys.VaccineUnits, ReadStringArray(root, "vaccineUnits"), cancellationToken);
    }

    private static async Task ImportLookupSettingsAsync(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("lookupSettings", out var settings) || settings.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in settings.EnumerateObject())
        {
            if (!prop.Name.StartsWith("Lookup.", StringComparison.Ordinal)) continue;
            var values = JsonSerializer.Deserialize<string[]>(prop.Value.GetRawText(), JsonOptions) ?? [];
            await SetLookupListAsync(context, prop.Name, values, cancellationToken);
        }
    }

    private static async Task ImportRemindDaysAsync(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        int? days = null;
        if (root.TryGetProperty("remindDays", out var rd) && rd.ValueKind == JsonValueKind.Number)
            days = rd.GetInt32();

        if (days is null) return;

        var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "RemindDays", cancellationToken);
        if (setting is null)
            context.AppSettings.Add(new AppSetting { Key = "RemindDays", Value = days.Value.ToString() });
        else
            setting.Value = days.Value.ToString();
    }

    private static async Task SetLookupListAsync(
        GoatFarmDbContext context, string key, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        if (values.Count == 0) return;
        var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        var json = JsonSerializer.Serialize(values);
        if (setting is null)
            context.AppSettings.Add(new AppSetting { Key = key, Value = json });
        else
            setting.Value = json;
    }

    private static string ResolveFeedType(string label, Dictionary<string, (string Key, string Name)> feedTypeMap)
    {
        if (feedTypeMap.ContainsKey(label))
            return label;

        foreach (var (key, info) in feedTypeMap)
        {
            if (string.Equals(info.Name, label, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return label.ToLowerInvariant().Replace(' ', '_');
    }

    private static Dictionary<string, int> BuildGoatIdRemap(JsonElement root, Dictionary<string, int> tagMap)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!root.TryGetProperty("goats", out var goats) || goats.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var g in goats.EnumerateArray())
        {
            var oldId = GetIdKey(g);
            var tag = GetString(g, "tag", "Tag");
            if (oldId is null || tag is null || !tagMap.TryGetValue(tag, out var newId)) continue;
            map[oldId] = newId;
        }

        return map;
    }

    private static Dictionary<string, int> BuildVaccineIdRemap(JsonElement root, Dictionary<string, int> nameMap)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!root.TryGetProperty("vaccines", out var vaccines) || vaccines.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var v in vaccines.EnumerateArray())
        {
            var oldId = GetIdKey(v);
            var name = GetString(v, "name", "Name");
            if (oldId is null || name is null || !nameMap.TryGetValue(name, out var newId)) continue;
            map[oldId] = newId;
        }

        return map;
    }

    private static bool TryResolveGoatId(
        JsonElement entry, Dictionary<string, int> tagMap, Dictionary<string, int> oldIdMap, out int goatId)
    {
        goatId = 0;
        if (entry.TryGetProperty("goatId", out var idEl) || entry.TryGetProperty("GoatId", out idEl))
        {
            var key = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32().ToString() : (idEl.GetString() ?? "");
            if (oldIdMap.TryGetValue(key, out goatId)) return true;
        }

        return false;
    }

    private static bool TryResolveVaccineId(
        JsonElement entry, Dictionary<string, int> nameMap, Dictionary<string, int> oldIdMap, out int vaccineId)
    {
        vaccineId = 0;
        if (entry.TryGetProperty("vaccineId", out var idEl) || entry.TryGetProperty("VaccineId", out idEl))
        {
            var key = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32().ToString() : (idEl.GetString() ?? "");
            if (oldIdMap.TryGetValue(key, out vaccineId)) return true;
        }

        return false;
    }

    private static string? GetIdKey(JsonElement el)
    {
        if (el.TryGetProperty("id", out var id) || el.TryGetProperty("Id", out id))
            return id.ValueKind == JsonValueKind.Number ? id.GetInt32().ToString() : id.GetString();
        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static Dictionary<string, decimal> ReadDecimalDictionary(JsonElement root, string key)
    {
        var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(key, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number)
                dict[prop.Name] = prop.Value.GetDecimal();
        }

        return dict;
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var val)) continue;
            return val.ValueKind switch
            {
                JsonValueKind.String => val.GetString(),
                JsonValueKind.Number => val.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static decimal GetDecimal(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var val) || val.ValueKind != JsonValueKind.Number)
                continue;
            return val.GetDecimal();
        }

        return 0;
    }

    private static int GetInt(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var val) || val.ValueKind != JsonValueKind.Number)
                continue;
            return val.GetInt32();
        }

        return 0;
    }

    private static int? GetNullableInt(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var val) || val.ValueKind != JsonValueKind.Number)
                continue;
            return val.GetInt32();
        }

        return null;
    }

    private static DateOnly? ParseDate(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var val) || val.ValueKind != JsonValueKind.String)
                continue;
            if (DateOnly.TryParse(val.GetString(), out var d))
                return d;
        }

        return null;
    }

    private static DateOnly ParseDateRequired(JsonElement el, params string[] names)
    {
        return ParseDate(el, names) ?? DateOnly.FromDateTime(DateTime.Today);
    }

    private static GoatStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return GoatStatus.Kid;
        if (Enum.TryParse<GoatStatus>(value, true, out var parsed)) return parsed;
        return value.ToLowerInvariant() switch
        {
            "kid" => GoatStatus.Kid,
            "milking" => GoatStatus.Milking,
            "pregnant" => GoatStatus.Pregnant,
            "dry" => GoatStatus.Dry,
            "buck" => GoatStatus.Buck,
            "sale" => GoatStatus.Sale,
            _ => GoatStatus.Kid
        };
    }

    private static GoatGender ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return GoatGender.Female;
        return Enum.TryParse<GoatGender>(value, true, out var parsed) ? parsed : GoatGender.Female;
    }

    private static GoatSource ParseSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return GoatSource.Bought;
        if (Enum.TryParse<GoatSource>(value, true, out var parsed)) return parsed;
        return value.Equals("born", StringComparison.OrdinalIgnoreCase) ? GoatSource.Born : GoatSource.Bought;
    }

    private static VaccineScope ParseScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return VaccineScope.None;
        if (Enum.TryParse<VaccineScope>(value, true, out var parsed)) return parsed;
        return value.ToLowerInvariant() switch
        {
            "all" => VaccineScope.All,
            "kid" => VaccineScope.Kid,
            "milking" => VaccineScope.Milking,
            "pregnant" => VaccineScope.Pregnant,
            "dry" => VaccineScope.Dry,
            "buck" => VaccineScope.Buck,
            "sale" => VaccineScope.Sale,
            "none" => VaccineScope.None,
            _ => VaccineScope.None
        };
    }

    private static VaccineRuleType ParseRuleType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return VaccineRuleType.Repeat;
        if (Enum.TryParse<VaccineRuleType>(value, true, out var parsed)) return parsed;
        return value.Equals("age", StringComparison.OrdinalIgnoreCase) ? VaccineRuleType.Age : VaccineRuleType.Repeat;
    }

    private static RecurringCostPeriod ParseRecurringPeriod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return RecurringCostPeriod.Month;
        if (Enum.TryParse<RecurringCostPeriod>(value, true, out var parsed)) return parsed;
        return value.Equals("year", StringComparison.OrdinalIgnoreCase) ? RecurringCostPeriod.Year : RecurringCostPeriod.Month;
    }
}
