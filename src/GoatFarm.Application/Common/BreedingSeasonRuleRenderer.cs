using GoatFarm.Application.ViewModels.Breeding;

namespace GoatFarm.Application.Common;

public static class BreedingSeasonRuleRenderer
{
    public static string RuleHtml(BreedingSeasonStepViewModel step, BreedingSeasonPanelViewModel season)
    {
        if (step.Label == "Start extra feed")
            return $"<input type=\"number\" id=\"se-build\" min=\"0\" value=\"{season.BuildUp}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>before crossing</b>";
        if (step.Label == "Check the bucks")
            return $"<input type=\"number\" id=\"se-check\" min=\"0\" value=\"{season.BuckCheckDays}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>before crossing</b> — feet, teeth, testicles, weight";
        if (step.Label == "Weigh young does")
            return $"<input type=\"number\" id=\"se-weigh\" min=\"0\" value=\"{season.WeighYoungDoesDays}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>before crossing</b> — need at least <input type=\"number\" id=\"se-minkg\" min=\"0\" step=\"0.1\" value=\"{season.MinYoungDoeKg}\" style=\"width:58px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> kg";
        if (step.Label == "Stop milking")
            return $"<input type=\"number\" id=\"se-drycross\" min=\"0\" value=\"{season.DryBeforeCross}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>before crossing</b>";
        if (step.Label == "Cross the does")
            return $"<span class=\"breed\">{BreedingHelper.GestationDays} days <b>before birth</b> — a goat's pregnancy</span>";
        if (step.Label == "Ultrasound scan")
            return $"<input type=\"number\" id=\"se-scan\" min=\"0\" value=\"{season.ScanAfter}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>after crossing</b>";
        if (step.Label == "Dry off the milkers")
            return $"<input type=\"number\" id=\"se-dry\" min=\"0\" value=\"{season.DryOff}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>before birth</b>";
        if (step.Label == "Get kidding pens ready")
            return $"<input type=\"number\" id=\"se-pens\" min=\"0\" value=\"{season.KiddingPensDays}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>before birth</b> — clean, dry straw, warm corner";
        if (step.Label == "Birth window")
        {
            var b = season.Batches.FirstOrDefault(x => x.Id == season.SelectedBatchId) ?? season.Batches.First();
            var kidY = DateTime.Today.Year;
            var from = $"{kidY}-{b.From}";
            var to = $"{kidY}-{b.To}";
            return $"<b>you choose this</b> — <input type=\"date\" id=\"se-from\" value=\"{from}\" style=\"padding:5px 7px;border:1px solid var(--line);border-radius:7px;font-family:inherit;font-size:14px\"> to <input type=\"date\" id=\"se-to\" value=\"{to}\" style=\"padding:5px 7px;border:1px solid var(--line);border-radius:7px;font-family:inherit;font-size:14px\">";
        }
        if (step.Label == "Wean the kids")
            return $"<input type=\"number\" id=\"se-wean\" min=\"0\" value=\"{season.WeanDays}\" style=\"width:62px;padding:5px 7px;border:1px solid var(--line);border-radius:7px;text-align:right;font-family:inherit;font-size:14px\"> days <b>after birth</b> — kids move to <b>Weaned</b> by themselves on this day";
        return step.RuleHtml;
    }
}
