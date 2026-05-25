namespace TotalCall.Client.Application.Services.Notifications;

public sealed record ToastMessage
{
    public required Guid Id { get; init; }
    public required ToastKind Kind { get; init; }
    public required string Text { get; init; }
    public TimeSpan AutoHideAfter { get; init; } = TimeSpan.FromSeconds(4);
}
