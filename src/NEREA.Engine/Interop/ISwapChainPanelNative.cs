using System.Runtime.InteropServices;

namespace NEREA.Engine.Interop;

/// <summary>
/// Native interop with SwapChainPanel (WinUI 3). The App layer obtains this
/// interface from the panel via WinRT cast and hands it to the engine.
/// </summary>
[ComImport]
[Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISwapChainPanelNative
{
    void SetSwapChain([MarshalAs(UnmanagedType.Interface)] object swapChain);
}
