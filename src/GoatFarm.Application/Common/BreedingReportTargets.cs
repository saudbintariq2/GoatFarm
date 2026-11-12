namespace GoatFarm.Application.Common;

/// <summary>V243 <c>TGT_DEF</c> — breeding report scorecard targets stored on season settings.</summary>
public class BreedingReportTargets
{
    public decimal Preg { get; set; } = 85;
    public decimal Kpd { get; set; } = 1.5m;
    public decimal Surv { get; set; } = 90;
    public decimal Kgd { get; set; } = 18;
    public decimal Interval { get; set; } = 365;
    public decimal Age { get; set; } = 18;
    public decimal Spc { get; set; } = 1.3m;

    public decimal? Get(string key) => key switch
    {
        "preg" => Preg,
        "kpd" => Kpd,
        "surv" => Surv,
        "kgd" => Kgd,
        "interval" => Interval,
        "age" => Age,
        "spc" => Spc,
        _ => null
    };

    public bool Set(string key, decimal value)
    {
        if (value <= 0) return false;
        switch (key)
        {
            case "preg": Preg = value; return true;
            case "kpd": Kpd = value; return true;
            case "surv": Surv = value; return true;
            case "kgd": Kgd = value; return true;
            case "interval": Interval = value; return true;
            case "age": Age = value; return true;
            case "spc": Spc = value; return true;
            default: return false;
        }
    }
}
