namespace TotalCall.Operations;

public sealed record OperationLogEntry(string Level, string Message)
{
    public static OperationLogEntry Info(string message) => new("info", message);

    public static OperationLogEntry Warn(string message) => new("warn", message);

    public static OperationLogEntry Error(string message) => new("error", message);

    public static OperationLogEntry Done(string message) => new("done", message);
}
