namespace GoatFarm.Application.Common;

public enum DoubleEntrySeverity
{
    Warn,
    Block
}

public class DoubleEntryValidationException : Exception
{
    public DoubleEntryValidationException(string message, DoubleEntrySeverity severity) : base(message)
    {
        Severity = severity;
    }

    public DoubleEntrySeverity Severity { get; }
}
