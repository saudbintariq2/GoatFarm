namespace GoatFarm.Domain.Enums;

/// <summary>Health is tracked independently of lifecycle <see cref="GoatStatus"/>.</summary>
public enum GoatHealthState
{
    Healthy = 0,
    Sick = 1,
    UnderTreatment = 2,
    Recovering = 3
}

/// <summary>Current breeding cycle stage of a doe, independent of lifecycle <see cref="GoatStatus"/>.</summary>
public enum BreedingStage
{
    None = 0,
    Ready = 1,
    ScanPending = 2,
    ConfirmedPregnant = 3
}

public enum BreedingOutcome
{
    Open = 0,
    ConfirmedPregnant = 1,
    Empty = 2,
    Kidded = 3,
    Aborted = 4,
    Cancelled = 5
}

public enum KiddingEase
{
    Normal = 0,
    Assisted = 1,
    Difficult = 2
}
