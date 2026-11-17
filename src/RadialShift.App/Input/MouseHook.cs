using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static RadialShift.App.Infra.NativeMethods;

namespace RadialShift.App.Input;

/// <summary>
/// Hook global de mouse (WH_MOUSE_LL). Só informa ao detector que houve clique ou rolagem,
/// para que Shift + clique (seleção múltipla) e Shift + roda não disparem.
/// Não guarda posição nem qualquer outro dado. Sempre chama CallNextHookEx.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly TriggerDetector _detector;
    private readonly HookProc _proc;
    private IntPtr _handle;

    public MouseHook(TriggerDetector detector)
    {
        _detector = detector;
        _proc = Callback;
    }

    public void Install()
    {
        if (_handle != IntPtr.Zero) return;
        _handle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Uninstall()
    {
        if (_handle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN
                or WM_XBUTTONDOWN or WM_MOUSEWHEEL or WM_MOUSEHWHEEL)
        {
            _detector.MouseDown();
        }

        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
