using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class MotherScoreHelper
{
    public static readonly string[] DamIssueOptions =
    [
        "retained placenta", "mastitis", "milk fever", "low milk", "prolapse", "weak"
    ];

    public static double? ScoreForKidding(KiddingRecord k) =>
        Score(k.Mothering, k.MilkProduction, k.Ease, k.DamHealth);

    public static double? Score(int? mothering, int? milk, KiddingEase ease, DamAfterKiddingHealth health)
    {
        if (mothering is null or < 1 or > 5 || milk is null or < 1 or > 5)
            return null;
        var easePts = EasePoints(ease);
        var healthPts = health switch
        {
            DamAfterKiddingHealth.Good => 5,
            DamAfterKiddingHealth.Watch => 3,
            DamAfterKiddingHealth.Sick => 1,
            _ => 3
        };
        return (mothering.Value + milk.Value + easePts + healthPts) / 4.0;
    }

    public static double? DoeAverage(IEnumerable<KiddingRecord> kiddings)
    {
        var scores = kiddings.Select(ScoreForKidding).Where(s => s.HasValue).Select(s => s!.Value).ToList();
        return scores.Count == 0 ? null : scores.Average();
    }

    public static string StarsHtml(double? score)
    {
        if (score is null) return "<span class=\"muted\">—</span>";
        var full = (int)Math.Round(score.Value);
        full = Math.Clamp(full, 0, 5);
        var stars = new string('★', full) + new string('☆', 5 - full);
        return $"<span class=\"breed-ms\" title=\"{score.Value:0.1}\">{stars}</span> <b>{score.Value:0.1}</b>";
    }

    public static int EasePoints(KiddingEase ease) => ease switch
    {
        KiddingEase.Easy => 5,
        KiddingEase.SlightHelp => 4,
        KiddingEase.HardPull => 2,
        KiddingEase.Vet => 1,
        KiddingEase.Caesarean => 1,
        _ => 3
    };

    public static bool TryParseEase(string? value, out KiddingEase ease)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ease = KiddingEase.Easy;
            return false;
        }

        var v = value.Trim();
        if (v.Equals("Normal", StringComparison.OrdinalIgnoreCase))
        {
            ease = KiddingEase.Easy;
            return true;
        }

        if (v.Equals("Assisted", StringComparison.OrdinalIgnoreCase))
        {
            ease = KiddingEase.SlightHelp;
            return true;
        }

        if (v.Equals("Difficult", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("Difficult", StringComparison.OrdinalIgnoreCase))
        {
            ease = KiddingEase.HardPull;
            return true;
        }

        return Enum.TryParse(v, true, out ease);
    }
}