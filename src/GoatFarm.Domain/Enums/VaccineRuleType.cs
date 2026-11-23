namespace GoatFarm.Domain.Enums;

public enum VaccineRuleType
{
    Age = 0,
    Repeat = 1,
  /// <summary>One dose per pregnancy, due N days before expected kidding (V2).</summary>
    BeforeKidding = 2
}
