using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace com.edvardcode.AmazingMarketScanner;

public sealed class PavilionState : INotifyPropertyChanged
{
    private static readonly SolidColorBrush BgPending = Brush("#232323");
    private static readonly SolidColorBrush BgScanned = Brush("#14351F");
    private static readonly SolidColorBrush BgClosed = Brush("#3A1515");
    private static readonly SolidColorBrush FgGreen = Brush("#4ADE80");
    private static readonly SolidColorBrush FgRed = Brush("#F87171");
    private static readonly SolidColorBrush FgDim = Brush("#777777");
    private static readonly SolidColorBrush FgWhite = Brush("#F5F5F5");
    private static readonly SolidColorBrush Accent = Brush("#FACC15");
    private static readonly SolidColorBrush Clear = Brush("#00000000");

    public int Number { get; }
    public PavilionState(int number) { Number = number; }

    private PavilionStatus _status = PavilionStatus.Pending;
    public PavilionStatus Status
    {
        get => _status;
        set
        {
            _status = value; OnChanged(); OnChanged(nameof(TileBrush)); OnChanged(nameof(Glyph));
            OnChanged(nameof(GlyphBrush)); OnChanged(nameof(NumberBrush));
        }
    }

    private string _detail = "";
    public string Detail { get => _detail; set { _detail = value; OnChanged(nameof(Glyph)); } }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; OnChanged(); OnChanged(nameof(BorderBrush)); OnChanged(nameof(NumberBrush)); }
    }

    public Brush TileBrush => Status switch { PavilionStatus.Scanned => BgScanned, PavilionStatus.Closed => BgClosed, _ => BgPending };
    public Brush BorderBrush => IsCurrent ? Accent : Clear;
    public string Glyph => Status switch { PavilionStatus.Scanned => $"✓ {Detail}", PavilionStatus.Closed => "✕", _ => "" };
    public Brush GlyphBrush => Status switch { PavilionStatus.Scanned => FgGreen, PavilionStatus.Closed => FgRed, _ => FgDim };
    public Brush NumberBrush => IsCurrent ? Accent : FgWhite;

    private static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}