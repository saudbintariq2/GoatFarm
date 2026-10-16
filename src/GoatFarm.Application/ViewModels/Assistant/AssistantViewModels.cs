using System.ComponentModel.DataAnnotations;

namespace GoatFarm.Application.ViewModels.Assistant;

public enum AssistantBlockKind
{
    Text,
    Summary,
    Table,
    Chart,
    Navigate,
    Clarify,
    Error
}

public sealed class AssistantChatRequest
{
    [StringLength(64)]
    public string? ConversationId { get; set; }

    [Required]
    [StringLength(AssistantLimits.MaxMessageLength, MinimumLength = 1)]
    public string Message { get; set; } = "";

    [StringLength(64)]
    public string? TimeZone { get; set; }
}

public sealed class AssistantTurnResponse
{
    public string ConversationId { get; set; } = "";
    public IReadOnlyList<AssistantBlock> Blocks { get; set; } = [];
}

public sealed class AssistantBlock
{
    public AssistantBlockKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public AssistantTable? Table { get; set; }
    public AssistantKpiList? Summary { get; set; }
    public AssistantChartData? Chart { get; set; }
    public AssistantNavigation? Navigation { get; set; }
    public AssistantClarification? Clarification { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class AssistantTable
{
    public IReadOnlyList<string> Columns { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];
}

public sealed class AssistantKpiList
{
    public IReadOnlyList<AssistantKpi> Items { get; set; } = [];
}

public sealed class AssistantKpi
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class AssistantChartData
{
    public string ChartType { get; set; } = "bar";
    public IReadOnlyList<AssistantChartPoint> Points { get; set; } = [];
}

public sealed class AssistantChartPoint
{
    public string Label { get; set; } = "";
    public decimal Value { get; set; }
}

public sealed class AssistantNavigation
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class AssistantClarification
{
    public IReadOnlyList<string> Questions { get; set; } = [];
}

public static class AssistantErrorCodes
{
    public const string NotConfigured = "not_configured";
    public const string Timeout = "timeout";
    public const string PermissionDenied = "permission_denied";
    public const string InvalidArguments = "invalid_arguments";
    public const string Unsupported = "unsupported";
    public const string MalformedModel = "malformed_model";
    public const string ToolLimit = "tool_limit";
    public const string ProviderError = "provider_error";
    public const string Validation = "validation";
}

public static class AssistantLimits
{
    public const int MaxMessageLength = 2000;
    public const int MaxConversationIdLength = 64;
    public const int MaxToolExecutionsPerTurn = 4;
    public const int MaxTableRows = 50;
    public const int MaxMilkRows = 50;
    public const int AbsoluteMaxMilkRows = 100;
    public const int MaxDateRangeDays = 366;
    public const int MaxCachedTurns = 8;
    public const int ConversationTtlMinutes = 30;
    public const int MaxTextChars = 4000;
    public const int MaxToolResultChars = 4000;
}
