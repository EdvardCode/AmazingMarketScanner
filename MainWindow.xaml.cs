using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace com.edvardcode.AmazingMarketScanner;

public partial class MainWindow : Window
{
    private const int PavilionCount = 40;
    private const string TargetsFile = "targets.json";

    private readonly ObservableCollection<PavilionState> _pavilions = [];
    private readonly ObservableCollection<string> _targets = [];
    private readonly ObservableCollection<string> _logs = [];
    private readonly List<Offer> _offers = [];
    private readonly OcrService _ocr = new();
    private HotkeyManager? _hotkeys;
    private IntPtr _myHwnd;
    private int _currentIndex = 1;

    public MainWindow()
    {
        InitializeComponent();
        for (int i = 1; i <= PavilionCount; i++) _pavilions.Add(new PavilionState(i));
        _pavilions[0].IsCurrent = true;

        PavilionGrid.ItemsSource = _pavilions;
        TargetsList.ItemsSource = _targets;
        LogList.ItemsSource = _logs;
        LoadTargets();

        Log("Готово. Цели загружены, начните обход павильонов.");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _myHwnd = new WindowInteropHelper(this).Handle;
        _hotkeys = new HotkeyManager(_myHwnd);
        (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(Hook);

        (uint mods, string label)[] combos =
        [
            (HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT,   "Ctrl+Alt"),
            (HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT, "Ctrl+Shift"),
            (HotkeyManager.MOD_ALT     | HotkeyManager.MOD_SHIFT, "Alt+Shift"),
        ];

        var scan = RegisterSafe(1, Key.F8, combos, () => Dispatcher.Invoke(ScanPavilion));
        var closed = RegisterSafe(2, Key.F7, combos, () => Dispatcher.Invoke(MarkClosed));
        var results = RegisterSafe(3, Key.F9, combos, () => Dispatcher.Invoke(ShowResults));
        var reset = RegisterSafe(4, Key.F10, combos, () => Dispatcher.Invoke(ResetAll));

        ChipScan.Text = scan is null ? "скан: нет свободной клавиши!" : $"{scan} — скан";
        ChipClosed.Text = closed is null ? "закрыт: нет свободной клавиши!" : $"{closed} — закрыт ✕";
        ChipResults.Text = results is null ? "итоги: нет свободной клавиши!" : $"{results} — итоги";
        ChipReset.Text = reset is null ? "сброс: нет свободной клавиши!" : $"{reset} — сброс";

        Log($"Хоткеи активны: {ChipScan.Text} | {ChipClosed.Text} | {ChipResults.Text} | {ChipReset.Text}");
    }

    private string? RegisterSafe(int id, Key key, (uint mods, string label)[] combos, Action action)
    {
        foreach (var (mods, label) in combos)
        {
            try
            {
                _hotkeys!.Register(id, mods, key, action);
                return $"{label}+{key}";
            }
            catch { /* занята — пробуем следующую */ }
        }
        return null;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (_hotkeys?.Process(msg, wp) == true) handled = true;
        return IntPtr.Zero;
    }

    private async void ScanPavilion()
    {
        if (_currentIndex > PavilionCount) { Log("Все павильоны обработаны."); return; }
        var fg = GetForegroundWindow();
        if (fg == _myHwnd) { Log("Сначала вернитесь в окно игры."); return; }

        int idx = _currentIndex;
        Log($"Павильон №{idx}: захват и распознавание...");
        try
        {
            var (offers, preview, pillsCount, debugLines) = await Task.Run(() => ScanWindow(fg, idx));
            foreach (var o in offers) _offers.Add(o with { Pavilion = idx });

            Log($"   зелёных блоков цены: {pillsCount}, распознано: {offers.Count}");
            foreach (var line in debugLines) Log("   [debug] " + line);

            _pavilions[idx - 1].Status = PavilionStatus.Scanned;
            _pavilions[idx - 1].Detail = offers.Count.ToString();
            PreviewImage.Source = preview;
            StatusText.Text = $"№{idx}: распознано предложений — {offers.Count}";
            foreach (var o in offers) Log($"   {o.ItemName} — {o.Price:N0} ₽");
            Advance();
        }
        catch (Exception ex) { Log($"Ошибка сканирования: {ex.Message}"); }
    }

    private (List<Offer> Offers, BitmapImage Preview, int Pills, List<string> Debug)
        ScanWindow(IntPtr hwnd, int idx)
    {
        using var shot = ScreenCapture.CaptureWindow(hwnd);
        var px = UiDetector.Lock(shot);
        var offers = new List<Offer>();
        var debug = new List<string>();

        Directory.CreateDirectory("scans/debug");
        var pills = UiDetector.FindPricePills(px);

        int i = 0;
        foreach (var pill in pills)
        {
            using (var pc = Crop(shot, pill))
            {
                pc.Save($"scans/debug/{idx:D2}_price_{i}.png");

                var rawPrice = _ocr.RecognizePriceGreen(pc);
                var digits = ParsePriceDigits(rawPrice);
                debug.Add($"цена OCR: '{rawPrice.Trim()}' -> {digits}");

                var nameRect = UiDetector.FindNameArea(px, pill);
                string name = "???";
                if (nameRect is not null)
                {
                    using var nc = Crop(shot, nameRect.Value);
                    nc.Save($"scans/debug/{idx:D2}_name_{i}.png");
                    name = _ocr.Recognize(nc).Trim().Replace("\n", " ").Replace("\r", " ");
                }
                debug.Add($"имя OCR: '{name}'");

                if (digits.Length >= 3)
                    offers.Add(new Offer(name, long.Parse(digits), 0));
            }
            i++;
        }
        return (offers, ToBitmapImage(shot), pills.Count, debug);
    }

    private static string ParsePriceDigits(string raw)
    {
        var sb = new StringBuilder();
        foreach (var ch in raw)
        {
            if (char.IsDigit(ch)) { sb.Append(ch); continue; }
            var mapped = char.ToUpperInvariant(ch) switch
            {
                'O' or 'Q' or 'D' => '0',
                'I' or 'L' or '|' => '1',
                'Z' => '2',
                'S' => '5',
                'B' => '8',
                _ => '\0'
            };
            if (mapped != '\0') sb.Append(mapped);
        }
        return sb.ToString();
    }

    private void MarkClosed()
    {
        if (_currentIndex > PavilionCount) { Log("Очередь пуста."); return; }
        int idx = _currentIndex;
        _pavilions[idx - 1].Status = PavilionStatus.Closed;
        StatusText.Text = $"№{idx} помечен закрытым";
        Log($"Павильон №{idx}: закрыт ✕, идём дальше.");
        Advance();
    }

    private void Advance()
    {
        _pavilions[_currentIndex - 1].IsCurrent = false;
        _currentIndex++;
        UpdateProgress();

        if (_currentIndex <= PavilionCount)
        {
            _pavilions[_currentIndex - 1].IsCurrent = true;
            CurrentNumberText.Text = _currentIndex.ToString();
        }
        else
        {
            CurrentNumberText.Text = "—";
            Log("Все 40 павильонов обработаны. Считаем итоги...");
            ShowResults();
        }
    }

    private void UpdateProgress()
    {
        Progress.Value = _currentIndex - 1;
        ProgressText.Text = $"{_currentIndex - 1} / {PavilionCount}";
    }

    private void ShowResults()
    {
        var rows = MarketAnalyzer.Analyze(_offers, _targets);
        ResultsGrid.ItemsSource = rows;
        Log($"Итоги: товаров с предложениями — {rows.Count}.");
        foreach (var r in rows.Take(5))
            Log($"   {r.Item}: лучше всего {r.BestPrice:N0} ₽ в павильоне №{r.BestPavilion} (выгода +{r.GainVsAvg:N0} к средней)");
    }

    private void ResetAll()
    {
        _offers.Clear();
        foreach (var p in _pavilions) { p.Status = PavilionStatus.Pending; p.IsCurrent = false; p.Detail = ""; }
        _currentIndex = 1;
        _pavilions[0].IsCurrent = true;
        CurrentNumberText.Text = "1";
        PreviewImage.Source = null;
        ResultsGrid.ItemsSource = null;
        UpdateProgress();
        Log("Сброс. Начните обход заново.");
    }

    private void AddTarget_Click(object sender, RoutedEventArgs e) => AddTarget();
    private void TargetInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddTarget(); }

    private void AddTarget()
    {
        var t = TargetInput.Text.Trim();
        if (t.Length == 0 || _targets.Contains(t, StringComparer.OrdinalIgnoreCase)) return;
        _targets.Add(t);
        TargetInput.Text = "";
        SaveTargets();
        Log($"Цель добавлена: {t}");
    }

    private void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string t })
        {
            _targets.Remove(t);
            SaveTargets();
        }
    }

    private void SaveTargets() => File.WriteAllText(TargetsFile, JsonSerializer.Serialize(_targets.ToList()));

    private void LoadTargets()
    {
        if (!File.Exists(TargetsFile)) return;
        try { foreach (var t in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(TargetsFile)) ?? []) _targets.Add(t); }
        catch { }
    }

    private void Log(string msg)
    {
        _logs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
        if (_logs.Count > 500) _logs.RemoveAt(0);
        LogList.ScrollIntoView(_logs[^1]);
    }

    private static Bitmap Crop(Bitmap src, Rectangle r)
    {
        r.Intersect(new Rectangle(0, 0, src.Width, src.Height));
        return src.Clone(r, src.PixelFormat);
    }

    private static BitmapImage ToBitmapImage(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    protected override void OnClosed(EventArgs e) { _hotkeys?.Dispose(); _ocr.Dispose(); base.OnClosed(e); }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}