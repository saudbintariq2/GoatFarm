namespace GoatFarm.Application.Common;

/// <summary>Thrown before any data is touched when a backup file fails validation.</summary>
public class BackupValidationException : Exception
{
    public BackupValidationException(IReadOnlyList<string> errors)
        : base("The backup file is not valid: " + string.Join("; ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
