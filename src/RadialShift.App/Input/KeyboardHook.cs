using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using RadialShift.App.Infra;
using static RadialShift.App.Infra.NativeMethods;

namespace RadialShift.App.Input;

/// <summary>
/// Hook global de teclado (WH_KEYBOARD_LL). Instalar na thread de UI.
/// REGRAS: o callback nunca armazena nem registra qual tecla foi pressionada; só traduz o evento
/// para "Shift esquerdo/direito/outra tecla" e repassa ao <see cref="TriggerDetector"/>.
/// Nada de COM, I/O ou log aqui. Sempre chama CallNextHookEx.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly TriggerDetector _detector;
    private readonly Action _onTap;
    private readonly HookProc _proc; // referência mantida para o GC não coletar o delegate
    private IntPtr _handle;
    private bool _leftDown;
    private bool _rightDown;

    /// <summary>Contador de eventos recebidos, usado pelo watchdog. Não indica quais teclas.</summary>
    public long EventCount { get; private set; }

    /// <param name="onTap">Chamado (dentro do callback) quando há um toque válido. Deve só despachar.</param>
    public KeyboardHook(TriggerDetector detector, Action onTap)
    {
        _detector = detector;
        _onTap = onTap;
        _proc = Callback;
    }

    public void Install()
    {
        if (_handle != IntPtr.Zero) return;
        _handle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Uninstall()
    {
        if (_handle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
        _leftDown = false;
        _rightDown = false;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            EventCount++;
            var msg = (int)wParam;
            var vk = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).vkCode;
            var key = vk switch
            {
                VK_LSHIFT => ShiftKey.Left,
                VK_RSHIFT => ShiftKey.Right,
                _ => ShiftKey.None,
            };
            var now = Environment.TickCount64;

            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                var isRepeat = false;
                if (key == ShiftKey.Left) { isRepeat = _leftDown; _leftDown = true; }
                else if (key == ShiftKey.Right) { isRepeat = _rightDown; _rightDown = true; }

                var otherModifiers = key != ShiftKey.None && !isRepeat &&
                    (IsKeyHeld(VK_CONTROL) || IsKeyHeld(VK_MENU) || IsKeyHeld(VK_LWIN) || IsKeyHeld(VK_RWIN));

                _detector.KeyDown(key, isRepeat, otherModifiers, now);
            }
            else if (msg is WM_KEYUP or WM_SYSKEYUP)
            {
                if (key == ShiftKey.Left) _leftDown = false;
                else if (key == ShiftKey.Right) _rightDown = false;

                if (_detector.KeyUp(key, now)) _onTap();
            }
        }

        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
