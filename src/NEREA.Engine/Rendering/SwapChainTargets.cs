using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace NEREA.Engine.Rendering;

/// <summary>
/// Composition swap chain bound to a SwapChainPanel plus all render targets:
/// HDR scene target, depth buffer and the half-resolution bloom chain.
/// </summary>
public sealed class SwapChainTargets : IDisposable
{
    public IDXGISwapChain4? SwapChain { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int SceneWidth => _sceneW;
    public int SceneHeight => _sceneH;

    public ID3D12Resource[] BackBuffers { get; } = new ID3D12Resource[EngineLimits.FrameCount];
    public ID3D12Resource SceneTarget { get; private set; } = null!;
    public ID3D12Resource DepthBuffer { get; private set; } = null!;
    public ID3D12Resource BloomA { get; private set; } = null!;
    public ID3D12Resource BloomB { get; private set; } = null!;

    public ID3D12DescriptorHeap RtvHeap { get; private set; } = null!;
    public ID3D12DescriptorHeap DsvHeap { get; private set; } = null!;
    public ID3D12DescriptorHeap SrvHeap { get; private set; } = null!;

    public CpuDescriptorHandle BackBufferRtv(int i) => Offset(RtvCpu, i, _rtvInc);
    public CpuDescriptorHandle SceneRtv => Offset(RtvCpu, 2, _rtvInc);
    public CpuDescriptorHandle BloomARtv => Offset(RtvCpu, 3, _rtvInc);
    public CpuDescriptorHandle BloomBRtv => Offset(RtvCpu, 4, _rtvInc);
    public CpuDescriptorHandle DepthDsv => DsvCpu;
    public CpuDescriptorHandle SceneSrv => Offset(SrvCpu, 0, _srvInc);
    public GpuDescriptorHandle SceneSrvGpu => OffsetGpu(SrvGpu, 0, _srvInc);
    public GpuDescriptorHandle BloomASrvGpu => OffsetGpu(SrvGpu, 1, _srvInc);
    public GpuDescriptorHandle BloomBSrvGpu => OffsetGpu(SrvGpu, 2, _srvInc);

    private CpuDescriptorHandle RtvCpu => RtvHeap.GetCPUDescriptorHandleForHeapStart();
    private CpuDescriptorHandle DsvCpu => DsvHeap.GetCPUDescriptorHandleForHeapStart();
    private CpuDescriptorHandle SrvCpu => SrvHeap.GetCPUDescriptorHandleForHeapStart();
    private GpuDescriptorHandle SrvGpu => SrvHeap.GetGPUDescriptorHandleForHeapStart();

    private int _rtvInc, _srvInc;
    private int _sceneW, _sceneH;
    private float _renderScale = 1f;

    private readonly GraphicsDevice _device;

    public SwapChainTargets(GraphicsDevice device)
    {
        _device = device;
        _rtvInc = (int)device.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        _srvInc = (int)device.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
    }

    public IDXGISwapChain4 CreateSwapChain(ID3D12CommandQueue queue, int width, int height)
    {
        Width = Math.Max(8, width);
        Height = Math.Max(8, height);

        var desc = new SwapChainDescription1
        {
            Width = (uint)Width,
            Height = (uint)Height,
            Format = Format.R8G8B8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = EngineLimits.FrameCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore
        };

        using IDXGISwapChain1 sc1 = _device.Factory.CreateSwapChainForComposition(queue, desc, null);
        SwapChain = sc1.QueryInterface<IDXGISwapChain4>();

        for (uint i = 0; i < EngineLimits.FrameCount; i++)
        {
            BackBuffers[i]?.Dispose();
            BackBuffers[i] = SwapChain.GetBuffer<ID3D12Resource>(i);
            _device.Device.CreateRenderTargetView(BackBuffers[i], null, BackBufferRtv((int)i));
        }

        if (RtvHeap is null)
        {
            RtvHeap = _device.Device.CreateDescriptorHeap(new DescriptorHeapDescription(
                DescriptorHeapType.RenderTargetView, 8, DescriptorHeapFlags.None, 0));
            DsvHeap = _device.Device.CreateDescriptorHeap(new DescriptorHeapDescription(
                DescriptorHeapType.DepthStencilView, 1, DescriptorHeapFlags.None, 0));
            SrvHeap = _device.Device.CreateDescriptorHeap(new DescriptorHeapDescription(
                DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 4, DescriptorHeapFlags.None, 0));
        }

        RebuildTargets(_renderScale);
        return SwapChain;
    }

    public void Resize(int width, int height, float renderScale)
    {
        width = Math.Max(8, width);
        height = Math.Max(8, height);
        _renderScale = renderScale;
        if (SwapChain is null) return;
        if (width == Width && height == Height && _sceneW == (int)(width * renderScale)) return;

        Width = width;
        Height = height;

        _device.Flush();
        for (int i = 0; i < EngineLimits.FrameCount; i++)
        {
            BackBuffers[i]?.Dispose();
            BackBuffers[i] = null!;
        }
        SwapChain.ResizeBuffers((uint)EngineLimits.FrameCount, (uint)Width, (uint)Height, Format.R8G8B8A8_UNorm, SwapChainFlags.None);
        for (uint i = 0; i < EngineLimits.FrameCount; i++)
        {
            BackBuffers[i] = SwapChain.GetBuffer<ID3D12Resource>(i);
            _device.Device.CreateRenderTargetView(BackBuffers[i], null, BackBufferRtv((int)i));
        }
        RebuildTargets(_renderScale);
    }

    private void RebuildTargets(float scale)
    {
        _device.Flush();
        SceneTarget?.Dispose();
        DepthBuffer?.Dispose();
        BloomA?.Dispose();
        BloomB?.Dispose();

        _sceneW = Math.Max(16, (int)(Width * scale));
        _sceneH = Math.Max(16, (int)(Height * scale));
        int bloomW = Math.Max(8, _sceneW / 2);
        int bloomH = Math.Max(8, _sceneH / 2);

        SceneTarget = CreateRt(Format.R16G16B16A16_Float, _sceneW, _sceneH);
        _device.Device.CreateRenderTargetView(SceneTarget, null, SceneRtv);
        _device.Device.CreateShaderResourceView(SceneTarget, null, SceneSrv);

        DepthBuffer = CreateRt(Format.R32_Typeless, _sceneW, _sceneH);
        var dsvDesc = new DepthStencilViewDescription
        {
            Format = Format.D32_Float,
            ViewDimension = DepthStencilViewDimension.Texture2D
        };
        _device.Device.CreateDepthStencilView(DepthBuffer, dsvDesc, DepthDsv);

        BloomA = CreateRt(Format.R16G16B16A16_Float, bloomW, bloomH);
        _device.Device.CreateRenderTargetView(BloomA, null, Offset(RtvCpu, 3, _rtvInc));
        _device.Device.CreateShaderResourceView(BloomA, null, Offset(SrvCpu, 1, _srvInc));

        BloomB = CreateRt(Format.R16G16B16A16_Float, bloomW, bloomH);
        _device.Device.CreateRenderTargetView(BloomB, null, Offset(RtvCpu, 4, _rtvInc));
        _device.Device.CreateShaderResourceView(BloomB, null, Offset(SrvCpu, 2, _srvInc));
    }

    private ID3D12Resource CreateRt(Format format, int w, int h)
    {
        var desc = ResourceDescription.Texture2D(format, (uint)w, (uint)h, 1, 1, 1, 0,
            ResourceFlags.None, TextureLayout.Unknown, 0);
        return _device.Device.CreateCommittedResource(
            HeapType.Default, desc, ResourceStates.RenderTarget, null);
    }

    private static CpuDescriptorHandle Offset(CpuDescriptorHandle baseHandle, int slot, int inc)
    {
        var h = baseHandle;
        h.Ptr += (nuint)(slot * inc);
        return h;
    }

    private static GpuDescriptorHandle OffsetGpu(GpuDescriptorHandle baseHandle, int slot, int inc)
    {
        var h = baseHandle;
        h.Ptr += (nuint)(slot * inc);
        return h;
    }

    public void Dispose()
    {
        for (int i = 0; i < EngineLimits.FrameCount; i++) BackBuffers[i]?.Dispose();
        SceneTarget?.Dispose();
        DepthBuffer?.Dispose();
        BloomA?.Dispose();
        BloomB?.Dispose();
        RtvHeap?.Dispose();
        DsvHeap?.Dispose();
        SrvHeap?.Dispose();
        SwapChain?.Dispose();
    }
}
