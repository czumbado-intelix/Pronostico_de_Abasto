using System.Runtime.InteropServices;

namespace PronosticosAbasto.Services;

/// <summary>
/// Acceso seguro al portapapeles de Windows mediante Win32 API.
/// </summary>
/// <remarks>
/// WinUI 3 (<c>Windows.ApplicationModel.DataTransfer.Clipboard</c>) sufre de un fallo
/// nativo documentado en Windows App SDK (Issue #6754) que genera
/// <c>STATUS_STOWED_EXCEPTION</c> (0xc000027b / 0x8000ffff) dentro de
/// <c>Microsoft.UI.Xaml.dll</c> al competir por el portapapeles con otras aplicaciones
/// (Excel, SAP, RDP) o ante cambios de foco. Este helper utiliza la API estandar de Win32
/// con reintentos y manejo de errores que no pueden tirar el proceso.
/// </remarks>
public static class SafeClipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 25;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    /// <summary>
    /// Intenta obtener el texto unicode del portapapeles de manera segura.
    /// </summary>
    public static bool TryGetText(out string text)
    {
        text = string.Empty;

        if (!TryOpenClipboard())
        {
            return false;
        }

        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            {
                return false;
            }

            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                text = Marshal.PtrToStringUni(pointer) ?? string.Empty;
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// Intenta colocar texto unicode en el portapapeles de manera segura.
    /// </summary>
    public static bool TrySetText(string text)
    {
        if (text is null)
        {
            return false;
        }

        if (!TryOpenClipboard())
        {
            return false;
        }

        var hGlobal = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            var bytesNeeded = (text.Length + 1) * sizeof(char);
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytesNeeded);
            if (hGlobal == IntPtr.Zero)
            {
                return false;
            }

            var target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                hGlobal = IntPtr.Zero;
                return false;
            }

            try
            {
                var chars = text.ToCharArray();
                Marshal.Copy(chars, 0, target, chars.Length);
                Marshal.WriteInt16(target, chars.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                hGlobal = IntPtr.Zero;
                return false;
            }

            // Cuando SetClipboardData tiene exito, el sistema operativo toma posesion
            // de la memoria asignada; no se debe liberar con GlobalFree.
            hGlobal = IntPtr.Zero;
            return true;
        }
        catch
        {
            if (hGlobal != IntPtr.Zero)
            {
                GlobalFree(hGlobal);
            }
            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool TryOpenClipboard()
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            if (i < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        return false;
    }
}
