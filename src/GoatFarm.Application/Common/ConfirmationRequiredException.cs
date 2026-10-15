namespace GoatFarm.Application.Common;

/// <summary>
/// The request is valid but carries warnings the user must accept; resend with <c>confirmWarnings: true</c>.
/// </summary>
public class ConfirmationRequiredException : Exception
{
    public ConfirmationRequiredException(IReadOnlyList<string> warnings)
        : base(string.Join("\n", warnings))
    {
        Warnings = warnings;
    }

    public IReadOnlyList<string> Warnings { get; }
}
