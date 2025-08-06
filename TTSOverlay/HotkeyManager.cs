using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TTSOverlay
{
    public class HotkeyManager
    {
        public event Action<int>? HotkeyPressed;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private HwndSource? _source;
        private Window _window;

        public HotkeyManager(Window window)
        {
            _window = window;
        }

        public void RegisterHotkeys()
        {
            var helper = new WindowInteropHelper(_window);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source.AddHook(HwndHook);

            Register(helper.Handle, 9000, 0x0002 | 0x0004, 0x49); // Ctrl+Shift+I
            Register(helper.Handle, 9001, 0, 0x70); // F1
            Register(helper.Handle, 9002, 0, 0x71); // F2
            Register(helper.Handle, 9003, 0, 0x72); // F3
            Register(helper.Handle, 9004, 0, 0x73); // F4
            Register(helper.Handle, 9005, 0, 0x74); // F5
            Register(helper.Handle, 9006, 0, 0x75); // F6
        }

        public void UnregisterHotkeys()
        {
            if (_source != null)
                _source.RemoveHook(HwndHook);

            var helper = new WindowInteropHelper(_window);
            for (int id = 9000; id <= 9006; id++)
                UnregisterHotKey(helper.Handle, id);
        }

        private void Register(IntPtr handle, int id, uint modifiers, uint key)
        {
            RegisterHotKey(handle, id, modifiers, key);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                HotkeyPressed?.Invoke(id);
                handled = true;
            }

            return IntPtr.Zero;
        }
    }
}
