namespace TotalCall.Client.Application.Windows;

public abstract class FloatingWindowDescriptor
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    public string Kind { get; }

    public string Title { get; protected set; }

    public string? Subtitle { get; protected set; }

    public WindowPosition Position { get; set; }

    public WindowSize? Size { get; set; }

    public int ZIndex { get; set; }

    public bool IsMinimized { get; set; }

    protected FloatingWindowDescriptor(string kind, string title, string? subtitle, WindowPosition position)
    {
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        Position = position;
    }
}
