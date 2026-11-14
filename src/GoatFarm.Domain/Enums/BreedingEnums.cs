namespace GoatFarm.Domain.Enums;

/// <summary>Health is tracked independently of lifecycle <see cref="GoatStatus"/>.</summary>
public enum GoatHealthState
{
    Healthy = 0,
    Sick = 1,
    UnderTreatment = 2,
    Recovering = 3,
    Quarantine = 4,
    Injured = 5,
    WeakOffFeed = 6
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
    Easy = 0,
    SlightHelp = 1,
    HardPull = 2,
    Vet = 3,
    Caesarean = 4
}

/// <summary>Dam health right after kidding (Sandeel v16).</summary>
public enum DamAfterKiddingHealth
{
    Good = 0,
    Watch = 1,
    Sick = 2
}

/// <summary>Keep / watch / cull decision after kidding.</summary>
public enum DamKeepDecision
{
    Keep = 0,
    Watch = 1,
    Cull = 2
}

/// <summary>V243 kid milk feeding on the Kidding tab.</summary>
public enum KidMilkType
{
    Mother = 0,
    Powder = 1,
    Mixed = 2,
    Foster = 3
}

/// <summary>V243 <c>kidHealth</c> — separate from lifecycle health state.</summary>
public enum KidHealthTag
{
    Good = 0,
    Weak = 1,
    Sick = 2
}
