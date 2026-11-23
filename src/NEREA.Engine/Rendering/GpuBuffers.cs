using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace NEREA.Engine.Rendering;

/// <summary>
/// A persistent default-heap buffer with a per-frame upload ring. Instances
/// and constants are written straight into mapped upload memory and copied to
/// the GPU once per frame - no per-frame allocations, no per-frame map/unmap.
/// </summary>
public sealed unsafe class InstanceBuffer : IDisposable
{
    public ID3D12Resource Resource { get; private set; } = null!;
    public int CapacityBytes { get; private set; }

    private readonly GraphicsDevice _device;
    private ID3D12Resource[] _upload = null!;
    private byte*[] _mapped = new byte*[EngineLimits.FrameCount];

    public InstanceBuffer(GraphicsDevice device, int capacityBytes)
    {
        _device = device;
        CapacityBytes = capacityBytes;

        Resource = device.Device.CreateCommittedResource(
            HeapType.Default, ResourceDescription.Buffer((ulong)capacityBytes, ResourceFlags.None, 0),
            ResourceStates.Common, null);

        _upload = new ID3D12Resource[EngineLimits.FrameCount];
        for (int i = 0; i < EngineLimits.FrameCount; i++)
        {
            _upload[i] = device.Device.CreateCommittedResource(
                HeapType.Upload, ResourceDescription.Buffer((ulong)capacityBytes, ResourceFlags.None, 0),
                ResourceStates.GenericRead, null);
            _mapped[i] = _upload[i].Map<byte>(0);
        }
    }

    /// <summary>Base pointer of this frame's mapped upload slot (persistent mapping).</summary>
    public byte* UploadPointer(int frame) => _mapped[frame];

    public ID3D12Resource GetUploadResource(int frame) => _upload[frame];

    public void Dispose()
    {
        if (_upload is not null)
        {
            for (int i = 0; i < _upload.Length; i++)
            {
                if (_upload[i] is not null)
                {
                    try { _upload[i].Unmap(0, null); } catch { /* already released */ }
                    _upload[i].Dispose();
                }
            }
        }
        Resource?.Dispose();
    }
}

/// <summary>
/// Small static vertex/index buffer. Geometry is a few kilobytes and never
/// changes, so it lives directly in mapped upload memory - valid for IA reads
/// and removes the staging-copy dance entirely.
/// </summary>
public sealed unsafe class StaticMeshBuffer : IDisposable
{
    public ID3D12Resource VertexBuffer { get; private set; } = null!;
    public ID3D12Resource? IndexBuffer { get; private set; }
    public int VertexStrideBytes { get; }
    public int VertexCount { get; }
    public int IndexCount { get; }
    public ulong VertexAddress => VertexBuffer.GPUVirtualAddress;
    public ulong IndexAddress => IndexBuffer?.GPUVirtualAddress ?? 0;

    public StaticMeshBuffer(GraphicsDevice device, MeshData mesh)
    {
        VertexStrideBytes = mesh.VertexStrideBytes;
        VertexCount = mesh.VertexCount;
        IndexCount = mesh.IndexCount;

        int vbBytes = mesh.Vertices.Length * 4;
        VertexBuffer = device.Device.CreateCommittedResource(
            HeapType.Upload, ResourceDescription.Buffer((ulong)Math.Max(16, vbBytes), ResourceFlags.None, 0),
            ResourceStates.GenericRead, null);
        byte* p = VertexBuffer.Map<byte>(0);
        fixed (float* src = mesh.Vertices)
            Buffer.MemoryCopy(src, p, vbBytes, vbBytes);
        VertexBuffer.Unmap(0, null);

        if (mesh.Indices is { Length: > 0 })
        {
            int ibBytes = mesh.Indices.Length * 4;
            IndexBuffer = device.Device.CreateCommittedResource(
                HeapType.Upload, ResourceDescription.Buffer((ulong)Math.Max(16, ibBytes), ResourceFlags.None, 0),
                ResourceStates.GenericRead, null);
            byte* ip = IndexBuffer.Map<byte>(0);
            fixed (uint* isrc = mesh.Indices)
                Buffer.MemoryCopy(isrc, ip, ibBytes, ibBytes);
            IndexBuffer.Unmap(0, null);
        }
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
    }
}
