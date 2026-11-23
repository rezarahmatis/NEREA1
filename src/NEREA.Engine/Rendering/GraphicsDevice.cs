using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;

namespace NEREA.Engine.Rendering;

/// <summary>
/// D3D12 device, direct queue, per-frame command allocators and fence sync.
/// Owns the frame lifecycle used by every other rendering subsystem.
/// </summary>
public sealed class GraphicsDevice : IDisposable
{
    public const int FrameCount = EngineLimits.FrameCount;

    public ID3D12Device Device { get; private set; } = null!;
    public IDXGIFactory4 Factory { get; private set; } = null!;
    public ID3D12CommandQueue Queue { get; private set; } = null!;
    public ID3D12CommandAllocator[] Allocators { get; private set; } = null!;
    public ID3D12GraphicsCommandList CommandList { get; private set; } = null!;
    public ID3D12Fence Fence { get; private set; } = null!;

    private ulong _fenceValue;
    private readonly ulong[] _frameFenceValues = new ulong[FrameCount];
    private readonly ManualResetEvent _fenceEvent = new(false);
    public int CpuFrameIndex { get; private set; }
    public bool DebugLayerEnabled { get; private set; }

    public static GraphicsDevice Create(bool allowDebugLayer)
    {
        var g = new GraphicsDevice();
        g.Initialize(allowDebugLayer);
        return g;
    }

    private void Initialize(bool allowDebugLayer)
    {
#if DEBUG
        if (allowDebugLayer)
        {
            try
            {
                if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out ID3D12Debug? debug).Success && debug is not null)
                {
                    debug.EnableDebugLayer();
                    DebugLayerEnabled = true;
                }
            }
            catch
            {
                // Debug layer unavailable: continue without validation.
            }
        }
#endif

        Factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(DebugLayerEnabled);

        var adapter = PickAdapter(Factory, out bool warp);
        Device = D3D12.D3D12CreateDevice<ID3D12Device>(adapter, FeatureLevel.Level_11_0);
        IsWarp = warp;
        adapter?.Dispose();

        Queue = Device.CreateCommandQueue(CommandListType.Direct, 0, CommandQueueFlags.None, 0);

        Allocators = new ID3D12CommandAllocator[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            Allocators[i] = Device.CreateCommandAllocator(CommandListType.Direct);

        CommandList = Device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, Allocators[0], null);
        CommandList.Close();

        Fence = Device.CreateFence(0, FenceFlags.None);
    }

    public bool IsWarp { get; private set; }

    private static IDXGIAdapter1? PickAdapter(IDXGIFactory4 factory, out bool warp)
    {
        IDXGIAdapter1? best = null;
        long bestMemory = -1;
        for (uint i = 0; factory.EnumAdapters1(i, out IDXGIAdapter1? adapter).Success; i++)
        {
            var desc = adapter.Description1;
            if ((desc.Flags & AdapterFlags.Software) != 0)
            {
                adapter.Dispose();
                warp = false;
                continue;
            }
            if (desc.DedicatedVideoMemory > bestMemory)
            {
                best?.Dispose();
                best = adapter;
                bestMemory = desc.DedicatedVideoMemory;
            }
            else
            {
                adapter.Dispose();
            }
        }
        if (best is null)
        {
            // No hardware adapter: fall back to WARP so the app still runs.
            factory.EnumWarpAdapter(out IDXGIAdapter1? warpAdapter);
            warp = true;
            return warpAdapter;
        }
        warp = false;
        return best;
    }

    public void BeginFrame()
    {
        WaitFrame(CpuFrameIndex);
        Allocators[CpuFrameIndex].Reset();
        CommandList.Reset(Allocators[CpuFrameIndex], null);
    }

    public void EndFrameAndExecute()
    {
        CommandList.Close();
        Queue.ExecuteCommandList(CommandList);

        _fenceValue++;
        Queue.Signal(Fence, _fenceValue);
        _frameFenceValues[CpuFrameIndex] = _fenceValue;

        CpuFrameIndex = (CpuFrameIndex + 1) % FrameCount;
    }

    public void WaitFrame(int frame)
    {
        if (Fence.CompletedValue < _frameFenceValues[frame])
        {
            Fence.SetEventOnCompletion(_frameFenceValues[frame], _fenceEvent);
            _fenceEvent.WaitOne();
            _fenceEvent.Reset();
        }
    }

    public void Flush()
    {
        _fenceValue++;
        Queue.Signal(Fence, _fenceValue);
        Fence.SetEventOnCompletion(_fenceValue, _fenceEvent);
        _fenceEvent.WaitOne();
        _fenceEvent.Reset();
    }

    public void Dispose()
    {
        CommandList?.Dispose();
        if (Allocators is not null)
            foreach (var a in Allocators) a.Dispose();
        Queue?.Dispose();
        Fence?.Dispose();
        Device?.Dispose();
        Factory?.Dispose();
        _fenceEvent.Dispose();
    }
}
