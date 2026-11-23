using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Mathematics;
using Vortice.DXGI;

namespace NEREA.Engine.Rendering;

/// <summary>Counts of instances to draw for one frame.</summary>
public struct FrameDrawCounts
{
    public int Fish;
    public int Jelly;
    public int Particles;      // motes + bubbles
    public int Motes;
    public int Vegetation;
    public int Ripples;
    public int Shadows;
    public bool Bloom;
    public float BloomStrength;
    public bool Vsync;
}

/// <summary>
/// Records one complete frame: water volume, floor with caustics, vegetation,
/// fish, jellyfish, particles, ripples, then bloom and final composite.
/// Resource transitions are tracked explicitly - steady state, no allocs.
/// </summary>
public sealed unsafe class FrameRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly PipelineFactory _pipelines;
    private SwapChainTargets? _targets;

    private StaticMeshBuffer _fishMesh = null!;
    private StaticMeshBuffer _vegMesh = null!;
    private StaticMeshBuffer _jellyMesh = null!;
    private StaticMeshBuffer _cornerQuad = null!;
    private StaticMeshBuffer _floorQuad = null!;

    private InstanceBuffer _instances = null!;

    private static readonly Color4 ClearColor = new(0f, 0f, 0f, 1f);

    public FrameRenderer(GraphicsDevice device, PipelineFactory pipelines)
    {
        _device = device;
        _pipelines = pipelines;

        _instances = new InstanceBuffer(device, InstanceLayout.TotalBytes);

        _fishMesh = new StaticMeshBuffer(device, MeshBuilder.BuildFish());
        _vegMesh = new StaticMeshBuffer(device, MeshBuilder.BuildVegetation());
        _jellyMesh = new StaticMeshBuffer(device, MeshBuilder.BuildJelly());
        _cornerQuad = new StaticMeshBuffer(device, MeshBuilder.BuildCornerQuad());
        _floorQuad = new StaticMeshBuffer(device, MeshBuilder.BuildFloorQuad(34f));
    }

    public InstanceBuffer Instances => _instances;

    public void SetTargets(SwapChainTargets targets)
    {
        _targets = targets;
        _backIsRenderTarget = false;
        _sceneIsRenderTarget = true;   // freshly built targets are in RenderTarget state
        _bloomAIsRenderTarget = true;
        _bloomBIsRenderTarget = true;
        _instancesAreReadable = false;
    }

    private bool _backIsRenderTarget;
    private bool _sceneIsRenderTarget;
    private bool _bloomAIsRenderTarget;
    private bool _bloomBIsRenderTarget;
    private bool _instancesAreReadable;

    private void Transition(ID3D12Resource res, ResourceStates from, ResourceStates to) =>
        _device.CommandList.ResourceBarrierTransition(res, from, to, 0, ResourceBarrierFlags.None);

    /// <summary>Records the instance-data upload and draws the full frame.</summary>
    public void RenderFrame(int frame, in PerFrameConstants constants, in FrameDrawCounts counts)
    {
        var targets = _targets!;
        var list = _device.CommandList;

        // ── Upload instance data ──
        {
            if (_instancesAreReadable)
            {
                list.ResourceBarrierTransition(_instances.Resource, ResourceStates.NonPixelShaderResource | ResourceStates.PixelShaderResource, ResourceStates.CopyDest, 0, ResourceBarrierFlags.None);
            }
            else
            {
                Transition(_instances.Resource, ResourceStates.Common, ResourceStates.CopyDest);
                _instancesAreReadable = true;
            }
            list.CopyBufferRegion(_instances.Resource, 0, _instances.GetUploadResource(frame), 0, (ulong)InstanceLayout.TotalBytes);
            list.ResourceBarrierTransition(_instances.Resource, ResourceStates.CopyDest, ResourceStates.NonPixelShaderResource | ResourceStates.PixelShaderResource, 0, ResourceBarrierFlags.None);
        }

        ulong cbVa = _instances.Resource.GPUVirtualAddress; // constants start at offset 0
        ulong fishVa = _instances.Resource.GPUVirtualAddress + (ulong)InstanceLayout.Fish;
        ulong particleVa = _instances.Resource.GPUVirtualAddress + (ulong)InstanceLayout.Particles;
        ulong vegVa = _instances.Resource.GPUVirtualAddress + (ulong)InstanceLayout.Vegetation;
        ulong jellyVa = _instances.Resource.GPUVirtualAddress + (ulong)InstanceLayout.Jelly;
        ulong shadowVa = _instances.Resource.GPUVirtualAddress + (ulong)InstanceLayout.Shadows;

        var vp = new Viewport(0, 0, targets.SceneWidth, targets.SceneHeight);
        var scissor = new RectI(0, 0, targets.SceneWidth, targets.SceneHeight);
        var dsv = targets.DepthDsv;

        // ── Scene pass ──
        if (!_sceneIsRenderTarget)
        {
            list.ResourceBarrierTransition(targets.SceneTarget, ResourceStates.PixelShaderResource, ResourceStates.RenderTarget, 0, ResourceBarrierFlags.None);
            _sceneIsRenderTarget = true;
        }
        list.OMSetRenderTargets(targets.SceneRtv, dsv);
        list.ClearRenderTargetView(targets.SceneRtv, ClearColor);
        list.ClearDepthStencilView(dsv, ClearFlags.Depth, 1f, 0);
        list.RSSetViewport(vp);
        list.RSSetScissorRect(scissor);
        list.SetGraphicsRootSignature(_pipelines.SceneRootSignature);
        list.SetGraphicsRootConstantBufferView(0, cbVa);
        list.SetGraphicsRootShaderResourceView(1, fishVa);
        list.SetGraphicsRootShaderResourceView(2, particleVa);
        list.SetGraphicsRootShaderResourceView(3, vegVa);
        list.SetGraphicsRootShaderResourceView(4, shadowVa);
        list.SetGraphicsRootShaderResourceView(5, jellyVa);

        // Environment (water volume).
        list.SetPipelineState(_pipelines.Environment);
        list.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        list.DrawInstanced(3, 1, 0, 0);

        // Floor with caustics.
        list.SetPipelineState(_pipelines.Floor);
        list.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        list.IASetVertexBuffers(0, new VertexBufferView(_floorQuad.VertexAddress, (uint)(_floorQuad.VertexCount * _floorQuad.VertexStrideBytes), (uint)_floorQuad.VertexStrideBytes));
        list.DrawInstanced(6, 1, 0, 0);

        // Vegetation.
        if (counts.Vegetation > 0)
        {
            list.SetPipelineState(_pipelines.Vegetation);
            list.IASetVertexBuffers(0, new VertexBufferView(_vegMesh.VertexAddress, (uint)(_vegMesh.VertexCount * _vegMesh.VertexStrideBytes), (uint)_vegMesh.VertexStrideBytes));
            list.IASetIndexBuffer(_vegMesh.IndexAddress, (uint)(_vegMesh.IndexCount * 4), true);
            list.DrawIndexedInstanced((uint)_vegMesh.IndexCount, (uint)counts.Vegetation, 0, 0, 0);
        }

        // Fish.
        if (counts.Fish > 0)
        {
            list.SetPipelineState(_pipelines.Fish);
            list.IASetVertexBuffers(0, new VertexBufferView(_fishMesh.VertexAddress, (uint)(_fishMesh.VertexCount * _fishMesh.VertexStrideBytes), (uint)_fishMesh.VertexStrideBytes));
            list.IASetIndexBuffer(_fishMesh.IndexAddress, (uint)(_fishMesh.IndexCount * 4), true);
            list.DrawIndexedInstanced((uint)_fishMesh.IndexCount, (uint)counts.Fish, 0, 0, 0);
        }

        // Jellyfish (additive, translucent).
        if (counts.Jelly > 0)
        {
            list.SetPipelineState(_pipelines.Jelly);
            list.IASetVertexBuffers(0, new VertexBufferView(_jellyMesh.VertexAddress, (uint)(_jellyMesh.VertexCount * _jellyMesh.VertexStrideBytes), (uint)_jellyMesh.VertexStrideBytes));
            list.IASetIndexBuffer(_jellyMesh.IndexAddress, (uint)(_jellyMesh.IndexCount * 4), true);
            list.DrawIndexedInstanced((uint)_jellyMesh.IndexCount, (uint)counts.Jelly, 0, 0, 0);
        }

        // Particles: motes (additive) then bubbles (alpha).
        if (counts.Particles > 0)
        {
            list.IASetVertexBuffers(0, new VertexBufferView(_cornerQuad.VertexAddress, (uint)(_cornerQuad.VertexCount * _cornerQuad.VertexStrideBytes), (uint)_cornerQuad.VertexStrideBytes));
            list.IASetIndexBuffer((IndexBufferView?)null);

            list.SetPipelineState(_pipelines.ParticlesMotes);
            list.DrawInstanced(6, (uint)counts.Motes, 0, 0);
            if (counts.Particles > counts.Motes)
            {
                list.SetPipelineState(_pipelines.ParticlesBubbles);
                list.DrawInstanced(6, (uint)(counts.Particles - counts.Motes), 0, (uint)counts.Motes);
            }
        }

        // Interaction ripples on the floor.
        if (counts.Ripples > 0)
        {
            list.SetPipelineState(_pipelines.Ripples);
            list.DrawInstanced(6, (uint)counts.Ripples, 0, 0);
        }

        // Scene target becomes readable.
        list.ResourceBarrierTransition(targets.SceneTarget, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource, 0, ResourceBarrierFlags.None);
        _sceneIsRenderTarget = false;

        // ── Bloom chain ──
        var postVp = new Viewport(0, 0, targets.SceneWidth / 2, targets.SceneHeight / 2);
        var postScissor = new RectI(0, 0, targets.SceneWidth / 2, targets.SceneHeight / 2);

        bool bloomEnabled = counts.Bloom;
        if (bloomEnabled)
        {
            list.RSSetViewport(postVp);
            list.RSSetScissorRect(postScissor);
            list.SetGraphicsRootSignature(_pipelines.PostRootSignature);
            list.SetGraphicsRootConstantBufferView(0, cbVa);

            // Extract: scene → BloomA.
            if (!_bloomAIsRenderTarget)
            {
                list.ResourceBarrierTransition(targets.BloomA, ResourceStates.PixelShaderResource, ResourceStates.RenderTarget, 0, ResourceBarrierFlags.None);
                _bloomAIsRenderTarget = true;
            }
            list.SetPipelineState(_pipelines.BloomExtract);
            list.SetGraphicsRootDescriptorTable(2, targets.SceneSrvGpu);
            SetPostParams(list, 1f / targets.SceneWidth, 1f / targets.SceneHeight, 1.05f, 0f);
            list.DrawInstanced(3, 1, 0, 0);

            // Blur H: BloomA → BloomB.
            list.ResourceBarrierTransition(targets.BloomA, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource, 0, ResourceBarrierFlags.None);
            _bloomAIsRenderTarget = false;
            if (!_bloomBIsRenderTarget)
            {
                list.ResourceBarrierTransition(targets.BloomB, ResourceStates.PixelShaderResource, ResourceStates.RenderTarget, 0, ResourceBarrierFlags.None);
                _bloomBIsRenderTarget = true;
            }
            list.SetPipelineState(_pipelines.Blur);
            list.SetGraphicsRootDescriptorTable(2, targets.BloomASrvGpu);
            SetPostParams(list, 1f / targets.SceneWidth, 1f / targets.SceneHeight, 1f, 0f);
            list.DrawInstanced(3, 1, 0, 0);

            // Blur V: BloomB → BloomA.
            list.ResourceBarrierTransition(targets.BloomB, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource, 0, ResourceBarrierFlags.None);
            _bloomBIsRenderTarget = false;
            list.ResourceBarrierTransition(targets.BloomA, ResourceStates.PixelShaderResource, ResourceStates.RenderTarget, 0, ResourceBarrierFlags.None);
            _bloomAIsRenderTarget = true;
            list.SetGraphicsRootDescriptorTable(2, targets.BloomBSrvGpu);
            SetPostParams(list, 1f / targets.SceneWidth, 1f / targets.SceneHeight, 0f, 1f);
            list.DrawInstanced(3, 1, 0, 0);

            list.ResourceBarrierTransition(targets.BloomA, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource, 0, ResourceBarrierFlags.None);
            _bloomAIsRenderTarget = false;
        }

        // ── Composite to the back buffer ──
        int back = (int)targets.SwapChain!.CurrentBackBufferIndex;
        if (!_backIsRenderTarget)
        {
            Transition(targets.BackBuffers[back], ResourceStates.Common, ResourceStates.RenderTarget);
            _backIsRenderTarget = true;
        }
        var finalVp = new Viewport(0, 0, targets.Width, targets.Height);
        var finalScissor = new RectI(0, 0, targets.Width, targets.Height);
        list.RSSetViewport(finalVp);
        list.RSSetScissorRect(finalScissor);
        list.SetGraphicsRootSignature(_pipelines.PostRootSignature);
        list.SetGraphicsRootConstantBufferView(0, cbVa);
        list.SetPipelineState(_pipelines.Composite);
        // The descriptor table starts at the scene SRV: t0 = scene, t1 = BloomA.
        // With bloom disabled the strength is zero, so stale BloomA content is ignored.
        list.SetGraphicsRootDescriptorTable(2, targets.SceneSrvGpu);
        SetPostParams(list, 1f / targets.SceneWidth, 1f / targets.SceneHeight,
            bloomEnabled ? counts.BloomStrength : 0f, 1.0f);
        list.DrawInstanced(3, 1, 0, 0);

        Transition(targets.BackBuffers[back], ResourceStates.RenderTarget, ResourceStates.Present);
        _backIsRenderTarget = false;

        targets.SwapChain.Present(bloomEnabled && counts.Vsync ? 1u : 0u, PresentFlags.None);
    }

    private void SetPostParams(ID3D12GraphicsCommandList list, float texelW, float texelH, float a, float b)
    {
        float* values = stackalloc float[4] { texelW, texelH, a, b };
        list.SetGraphicsRoot32BitConstants(1, 4, values, 0);
    }

    public void Dispose()
    {
        _fishMesh?.Dispose();
        _vegMesh?.Dispose();
        _jellyMesh?.Dispose();
        _cornerQuad?.Dispose();
        _floorQuad?.Dispose();
        _instances?.Dispose();
    }
}
