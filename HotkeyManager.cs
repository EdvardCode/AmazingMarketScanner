using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace com.edvardcode.AmazingMarketScanner;

public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _actions = [];
    private readonly List<int> _ids = [];

    public HotkeyManager(IntPtr hwnd) => _hwnd = hwnd;

    public void Register(int id, uint modifiers, Key key, Action action)
    {
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk))
            throw new InvalidOperationException($"Хоткей {key} занят другой программой");
        _ids.Add(id);
        _actions[id] = action;
    }

    public bool Process(int msg, IntPtr wParam)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var a))
        {
            a();
            return true;
        }
        return false;
    }

    public void Dispose() { foreach (var id in _ids) UnregisterHotKey(_hwnd, id); }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}