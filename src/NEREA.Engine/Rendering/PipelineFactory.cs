using Vortice.Direct3D12;
using Vortice.DXGI;

namespace NEREA.Engine.Rendering;

/// <summary>All graphics pipeline state objects, built once at startup.</summary>
public sealed class PipelineFactory : IDisposable
{
    public ID3D12RootSignature SceneRootSignature { get; private set; } = null!;
    public ID3D12RootSignature PostRootSignature { get; private set; } = null!;

    public ID3D12PipelineState Environment { get; private set; } = null!;
    public ID3D12PipelineState Floor { get; private set; } = null!;
    public ID3D12PipelineState Fish { get; private set; } = null!;
    public ID3D12PipelineState Vegetation { get; private set; } = null!;
    public ID3D12PipelineState ParticlesMotes { get; private set; } = null!;
    public ID3D12PipelineState ParticlesBubbles { get; private set; } = null!;
    public ID3D12PipelineState Ripples { get; private set; } = null!;
    public ID3D12PipelineState Jelly { get; private set; } = null!;
    public ID3D12PipelineState BloomExtract { get; private set; } = null!;
    public ID3D12PipelineState Blur { get; private set; } = null!;
    public ID3D12PipelineState Composite { get; private set; } = null!;

    private readonly GraphicsDevice _device;

    public PipelineFactory(GraphicsDevice device)
    {
        _device = device;
        Build();
    }

    private void Build()
    {
        var device = _device.Device;

        // ── Scene root signature: per-frame CB + root-structured SRVs (no heap). ──
        var sceneParams = new RootParameter[]
        {
            new(RootParameterType.ConstantBufferView, new RootDescriptor(0, 0), ShaderVisibility.All),      // b0 PerFrame
            new(RootParameterType.ShaderResourceView, new RootDescriptor(0, 0), ShaderVisibility.All),      // t0 fish
            new(RootParameterType.ShaderResourceView, new RootDescriptor(1, 0), ShaderVisibility.All),      // t1 particles
            new(RootParameterType.ShaderResourceView, new RootDescriptor(2, 0), ShaderVisibility.All),      // t2 vegetation
            new(RootParameterType.ShaderResourceView, new RootDescriptor(3, 0), ShaderVisibility.All),      // t3 shadows
            new(RootParameterType.ShaderResourceView, new RootDescriptor(4, 0), ShaderVisibility.All)       // t4 jelly
        };
        var sceneDesc = new RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout, sceneParams, Array.Empty<StaticSamplerDescription>());
        SceneRootSignature = device.CreateRootSignature(sceneDesc, RootSignatureVersion.Version10);

        // ── Post root signature: heap SRVs (t0/t1), static linear sampler, PerFrame CB, root constants. ──
        var postParams = new RootParameter[]
        {
            new(RootParameterType.ConstantBufferView, new RootDescriptor(0, 0), ShaderVisibility.All),      // b0 PerFrame
            new(new RootConstants(1, 0, 4), ShaderVisibility.All),                                          // b1 inline constants
            new(new RootDescriptorTable(new[]
            {
                new DescriptorRange(DescriptorRangeType.ShaderResourceView, 2, 0, 0, 0)                                    // t0 scene, t1 bloom
            }), ShaderVisibility.All)
        };
        var linearSampler = new StaticSamplerDescription(
            0, Filter.MinMagMipLinear, TextureAddressMode.Clamp, TextureAddressMode.Clamp, TextureAddressMode.Clamp,
            0f, 1, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, 0f, float.MaxValue,
            ShaderVisibility.All, 0);
        var postDesc = new RootSignatureDescription(RootSignatureFlags.None, postParams, new[] { linearSampler });
        PostRootSignature = device.CreateRootSignature(postDesc, RootSignatureVersion.Version10);

        var dsvFormat = Format.D32_Float;

        // ── Scene pipelines ──────────────────────────────────────────────────────
        var vsEnv = ShaderLibrary.Compile("Environment.hlsl", "VSEnvironment", "vs_5_0");
        var psEnv = ShaderLibrary.Compile("Environment.hlsl", "PSEnvironment", "ps_5_0");
        Environment = CreatePipeline(SceneRootSignature, vsEnv, psEnv, null, null,
            depthEnabled: false, dsvFormat: dsvFormat);

        var vsFloor = ShaderLibrary.Compile("Floor.hlsl", "VSFloor", "vs_5_0");
        var psFloor = ShaderLibrary.Compile("Floor.hlsl", "PSFloor", "ps_5_0");
        Floor = CreatePipeline(SceneRootSignature, vsFloor, psFloor, FloorInput(), null,
            depthEnabled: true, dsvFormat: dsvFormat);

        var vsFish = ShaderLibrary.Compile("Fish.hlsl", "VSFish", "vs_5_0");
        var psFish = ShaderLibrary.Compile("Fish.hlsl", "PSFish", "ps_5_0");
        Fish = CreatePipeline(SceneRootSignature, vsFish, psFish, FishInput(), null,
            depthEnabled: true, dsvFormat: dsvFormat);

        var vsVeg = ShaderLibrary.Compile("Vegetation.hlsl", "VSVegetation", "vs_5_0");
        var psVeg = ShaderLibrary.Compile("Vegetation.hlsl", "PSVegetation", "ps_5_0");
        Vegetation = CreatePipeline(SceneRootSignature, vsVeg, psVeg, VegInput(), null,
            depthEnabled: true, dsvFormat: dsvFormat);

        var vsParticles = ShaderLibrary.Compile("Particles.hlsl", "VSParticles", "vs_5_0");
        var psMotes = ShaderLibrary.Compile("Particles.hlsl", "PSParticles", "ps_5_0");
        ParticlesMotes = CreatePipeline(SceneRootSignature, vsParticles, psMotes, CornerInput(),
            BlendDescription.Additive, depthEnabled: true, depthWrite: false, dsvFormat: dsvFormat);
        ParticlesBubbles = CreatePipeline(SceneRootSignature, vsParticles, psMotes, CornerInput(),
            BlendDescription.AlphaBlend, depthEnabled: true, depthWrite: false, dsvFormat: dsvFormat);

        var vsRipples = ShaderLibrary.Compile("Ripples.hlsl", "VSRipples", "vs_5_0");
        var psRipples = ShaderLibrary.Compile("Ripples.hlsl", "PSRipples", "ps_5_0");
        Ripples = CreatePipeline(SceneRootSignature, vsRipples, psRipples, CornerInput(),
            BlendDescription.Additive, depthEnabled: true, depthWrite: false, dsvFormat: dsvFormat);

        var vsJelly = ShaderLibrary.Compile("Jelly.hlsl", "VSJelly", "vs_5_0");
        var psJelly = ShaderLibrary.Compile("Jelly.hlsl", "PSJelly", "ps_5_0");
        Jelly = CreatePipeline(SceneRootSignature, vsJelly, psJelly, JellyInput(),
            BlendDescription.Additive, depthEnabled: true, depthWrite: false, dsvFormat: dsvFormat);

        // ── Post pipelines ───────────────────────────────────────────────────────
        var vsPost = ShaderLibrary.Compile("FullscreenTri.hlsl", "VSFullscreen", "vs_5_0");
        var psExtract = ShaderLibrary.Compile("Post.hlsl", "PSBloomExtract", "ps_5_0");
        var psBlur = ShaderLibrary.Compile("Post.hlsl", "PSBlur", "ps_5_0");
        var psComposite = ShaderLibrary.Compile("Post.hlsl", "PSComposite", "ps_5_0");

        BloomExtract = CreatePipeline(PostRootSignature, vsPost, psExtract, null, null,
            depthEnabled: false, dsvFormat: Format.Unknown);
        Blur = CreatePipeline(PostRootSignature, vsPost, psBlur, null, null,
            depthEnabled: false, dsvFormat: Format.Unknown);
        Composite = CreatePipeline(PostRootSignature, vsPost, psComposite, null, null,
            depthEnabled: false, dsvFormat: Format.Unknown);
    }

    private ID3D12PipelineState CreatePipeline(
        ID3D12RootSignature rootSignature,
        ReadOnlyMemory<byte> vs, ReadOnlyMemory<byte> ps,
        InputElementDescription[]? inputElements,
        BlendDescription? blend,
        bool depthEnabled,
        bool depthWrite = true,
        Format dsvFormat = Format.Unknown)
    {
        var desc = new GraphicsPipelineStateDescription
        {
            RootSignature = rootSignature,
            VertexShader = vs,
            PixelShader = ps,
            BlendState = blend ?? BlendDescription.Opaque,
            SampleMask = uint.MaxValue,
            RasterizerState = new RasterizerDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                FrontCounterClockwise = false,
                DepthBias = 0,
                DepthBiasClamp = 0f,
                SlopeScaledDepthBias = 0f,
                DepthClipEnable = true,
                MultisampleEnable = false,
                AntialiasedLineEnable = false,
                ForcedSampleCount = 0,
                ConservativeRaster = ConservativeRasterizationMode.Off
            },
            DepthStencilState = depthEnabled
                ? new DepthStencilDescription
                {
                    DepthEnable = true,
                    DepthWriteMask = depthWrite ? DepthWriteMask.All : DepthWriteMask.Zero,
                    DepthFunc = ComparisonFunction.LessEqual
                }
                : new DepthStencilDescription
                {
                    DepthEnable = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthFunc = ComparisonFunction.Always
                },
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            InputLayout = inputElements is null
                ? new InputLayoutDescription()
                : new InputLayoutDescription(inputElements),
            RenderTargetFormats = new[] { Format.R16G16B16A16_Float },
            DepthStencilFormat = dsvFormat,
            SampleDescription = new SampleDescription(1, 0),
            Flags = PipelineStateFlags.None
        };
        return _device.Device.CreateGraphicsPipelineState(desc);
    }

    private static InputElementDescription[] FloorInput() => new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0)
    };

    private static InputElementDescription[] FishInput() => new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
        new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 20, 0),
        new InputElementDescription("TEXCOORD", 1, Format.R32_Float, 32, 0)
    };

    private static InputElementDescription[] VegInput() => new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
    };

    private static InputElementDescription[] CornerInput() => new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0)
    };

    private static InputElementDescription[] JellyInput() => new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
        new InputElementDescription("TEXCOORD", 1, Format.R32_Float, 20, 0)
    };

    public void Dispose()
    {
        SceneRootSignature?.Dispose();
        PostRootSignature?.Dispose();
        Environment?.Dispose();
        Floor?.Dispose();
        Fish?.Dispose();
        Vegetation?.Dispose();
        ParticlesMotes?.Dispose();
        ParticlesBubbles?.Dispose();
        Ripples?.Dispose();
        Jelly?.Dispose();
        BloomExtract?.Dispose();
        Blur?.Dispose();
        Composite?.Dispose();
    }
}
