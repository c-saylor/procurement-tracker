namespace ProcureTrack.Web.Services;

/// <summary>Outcome of a workflow operation. Expected rule violations are returned here instead of throwing exceptions.</summary>
public sealed record WorkflowResult(bool Succeeded, string? Error)
{
    public static WorkflowResult Success() => new(true, null);

    public static WorkflowResult Failure(string error) => new(false, error);
}