using TotalCall.Client.Application.Windows;

namespace TotalCall.Client.Application.Services;

public sealed class WindowManager
{
    private const int CascadeStepPx = 28;
    private const int CascadeBaseX = 96;
    private const int CascadeBaseY = 96;
    private const int CascadeWrap = 6;
    private const int BaseZIndex = 50;

    private readonly List<FloatingWindowDescriptor> windows = [];
    private int nextZIndex = BaseZIndex;
    private int cascadeIndex = 0;

    public IReadOnlyList<FloatingWindowDescriptor> Windows => windows;

    public event Action? StateChanged;

    public event Action<string, AthleteHistoryAction>? AthleteHistoryActionRequested;

    public FloatingWindowDescriptor? ActiveWindow => windows
        .Where(window => !window.IsMinimized)
        .OrderByDescending(window => window.ZIndex)
        .FirstOrDefault();

    public AthleteHistoryWindowDescriptor OpenAthleteHistory(
        string competitionId,
        string athleteId,
        string athleteName,
        string? countryCode,
        string? countryName,
        string? categoryName,
        decimal? nominatedTotalKg,
        string title,
        AthleteHistorySlotContext? slotContext = null)
    {
        var existing = windows
            .OfType<AthleteHistoryWindowDescriptor>()
            .FirstOrDefault(window =>
                string.Equals(window.CompetitionId, competitionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(window.AthleteId, athleteId, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (slotContext is not null)
            {
                existing.SlotContext = slotContext;
            }

            existing.IsMinimized = false;
            BringToFront(existing.Id);
            return existing;
        }

        var descriptor = new AthleteHistoryWindowDescriptor(
            competitionId,
            athleteId,
            athleteName,
            countryCode,
            countryName,
            categoryName,
            nominatedTotalKg,
            slotContext,
            NextCascadePosition(),
            title)
        {
            ZIndex = ++nextZIndex
        };

        windows.Add(descriptor);
        StateChanged?.Invoke();
        return descriptor;
    }

    public void UpdateAthleteHistorySlotContext(string athleteId, AthleteHistorySlotContext? context)
    {
        var changed = false;

        foreach (var window in windows.OfType<AthleteHistoryWindowDescriptor>())
        {
            if (!string.Equals(window.AthleteId, athleteId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Equals(window.SlotContext, context))
            {
                window.SlotContext = context;
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    public void ClearAllAthleteHistorySlotContexts()
    {
        var changed = false;

        foreach (var window in windows.OfType<AthleteHistoryWindowDescriptor>())
        {
            if (window.SlotContext is not null)
            {
                window.SlotContext = null;
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    public void Close(string windowId)
    {
        var window = FindById(windowId);
        if (window is null)
        {
            return;
        }

        windows.Remove(window);
        StateChanged?.Invoke();
    }

    public void CloseAll()
    {
        if (windows.Count == 0)
        {
            return;
        }

        windows.Clear();
        cascadeIndex = 0;
        StateChanged?.Invoke();
    }

    public void Minimize(string windowId, bool minimized)
    {
        var window = FindById(windowId);
        if (window is null || window.IsMinimized == minimized)
        {
            return;
        }

        window.IsMinimized = minimized;

        if (!minimized)
        {
            BringToFront(windowId);
            return;
        }

        StateChanged?.Invoke();
    }

    public void BringToFront(string windowId)
    {
        var window = FindById(windowId);
        if (window is null)
        {
            return;
        }

        var top = windows
            .Where(other => other.Id != windowId)
            .Select(other => other.ZIndex)
            .DefaultIfEmpty(BaseZIndex)
            .Max();

        if (window.ZIndex > top)
        {
            StateChanged?.Invoke();
            return;
        }

        nextZIndex = Math.Max(nextZIndex, top + 1);
        window.ZIndex = ++nextZIndex;
        StateChanged?.Invoke();
    }

    public void SetPosition(string windowId, double x, double y)
    {
        var window = FindById(windowId);
        if (window is null)
        {
            return;
        }

        if (Math.Abs(window.Position.X - x) < 0.5 && Math.Abs(window.Position.Y - y) < 0.5)
        {
            return;
        }

        window.Position = new WindowPosition(x, y);
        StateChanged?.Invoke();
    }

    public void SetSize(string windowId, double width, double height)
    {
        var window = FindById(windowId);
        if (window is null)
        {
            return;
        }

        if (window.Size is { } current
            && Math.Abs(current.Width - width) < 0.5
            && Math.Abs(current.Height - height) < 0.5)
        {
            return;
        }

        window.Size = new WindowSize(width, height);
        StateChanged?.Invoke();
    }

    public void RequestAthleteHistoryAction(string windowId, AthleteHistoryAction action)
    {
        AthleteHistoryActionRequested?.Invoke(windowId, action);
    }

    private FloatingWindowDescriptor? FindById(string windowId)
    {
        return windows.FirstOrDefault(window =>
            string.Equals(window.Id, windowId, StringComparison.Ordinal));
    }

    private WindowPosition NextCascadePosition()
    {
        var step = cascadeIndex % CascadeWrap;
        cascadeIndex++;
        return new WindowPosition(
            CascadeBaseX + (step * CascadeStepPx),
            CascadeBaseY + (step * CascadeStepPx));
    }
}
