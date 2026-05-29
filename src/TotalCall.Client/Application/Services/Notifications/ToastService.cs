using System.Collections.Concurrent;

namespace TotalCall.Client.Application.Services.Notifications;

public sealed class ToastService
{
    private readonly ConcurrentDictionary<Guid, ToastMessage> messages = new();

    public event Action? OnChanged;

    public IReadOnlyList<ToastMessage> Messages =>
        messages.Values
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .ToArray();

    public ToastMessage ShowSuccess(string text) =>
        Show(new ToastMessage { Id = Guid.NewGuid(), Kind = ToastKind.Success, Text = text });

    public ToastMessage ShowError(string text) =>
        Show(new ToastMessage
        {
            Id = Guid.NewGuid(),
            Kind = ToastKind.Error,
            Text = text,
            AutoHideAfter = TimeSpan.FromSeconds(6)
        });

    public ToastMessage ShowInfo(string text) =>
        Show(new ToastMessage { Id = Guid.NewGuid(), Kind = ToastKind.Info, Text = text });

    public void Dismiss(Guid id)
    {
        if (messages.TryRemove(id, out _))
        {
            OnChanged?.Invoke();
        }
    }

    private ToastMessage Show(ToastMessage message)
    {
        messages[message.Id] = message;
        OnChanged?.Invoke();
        return message;
    }
}
