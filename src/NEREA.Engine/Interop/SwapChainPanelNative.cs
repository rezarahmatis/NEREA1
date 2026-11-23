using System.Runtime.InteropServices;

namespace NEREA.Engine.Interop;

/// <summary>
/// Native binding of a composition swap chain to a WinUI SwapChainPanel.
/// Calls ISwapChainPanelNative::SetSwapChain directly through the IInspectable
/// vtable (slots 0-2 = IUnknown, 3-5 = IInspectable, 6 = SetSwapChain), so the
/// engine does not depend on any WinUI / WinRT projection assembly.
/// </summary>
public static class SwapChainPanelNative
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetSwapChainDelegate(IntPtr thisPtr, IntPtr swapChain);

    /// <summary>
    /// Binds <paramref name="swapChain"/> (an IDXGISwapChain pointer) to the panel.
    /// <paramref name="panelAbi"/> is the IUnknown pointer of the SwapChainPanel object.
    /// </summary>
    public static void SetSwapChain(IntPtr panelAbi, IntPtr swapChain)
    {
        if (panelAbi == IntPtr.Zero || swapChain == IntPtr.Zero) return;

        var vtbl = Marshal.ReadIntPtr(panelAbi);
        var setSwapChain = Marshal.GetDelegateForFunctionPointer<SetSwapChainDelegate>(
            Marshal.ReadIntPtr(vtbl, 6 * IntPtr.Size));
        var hr = setSwapChain(panelAbi, swapChain);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }
}
