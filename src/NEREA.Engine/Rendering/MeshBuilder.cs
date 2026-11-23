using System.Numerics;

namespace NEREA.Engine.Rendering;

public sealed record MeshData(float[] Vertices, uint[]? Indices, int VertexStrideFloats)
{
    public int VertexStrideBytes => VertexStrideFloats * 4;
    public int VertexCount => Vertices.Length / VertexStrideFloats;
    public int IndexCount => Indices?.Length ?? VertexCount;
}

/// <summary>
/// Procedural geometry. One generic fish/kelp/jelly mesh is deformed per
/// instance by the vertex shader using species data, so new species need no
/// new assets.
/// </summary>
public static class MeshBuilder
{
    private sealed class Builder
    {
        public readonly List<float> Verts = new();
        public readonly List<uint> Indices = new();

        public int AddVertex(float px, float py, float pz, float u, float v, float nx, float ny, float nz, float part)
        {
            Verts.AddRange(stackalloc[] { px, py, pz, u, v, nx, ny, nz, part });
            return (Verts.Count / 9) - 1;
        }

        public void Quad(int a, int b, int c, int d)
        {
            Indices.AddRange(new uint[] { (uint)a, (uint)b, (uint)c, (uint)a, (uint)c, (uint)d });
        }
    }

    private static float RadiusProfile(float u) => 0.12f + 0.88f * MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(u, 0.85f)), 1.0f);

    // ── Fish ─────────────────────────────────────────────────────────────────
    // Local space: x forward (nose +0.5), y up, z lateral. Unit length 1.
    public static MeshData BuildFish()
    {
        var b = new Builder();
        const int Segments = 20;
        const int Radial = 10;

        // Body rings.
        for (int s = 0; s < Segments; s++)
        {
            float u = s / (float)(Segments - 1);
            float x = 0.5f - u;
            float r = RadiusProfile(u);
            float dr = (RadiusProfile(u + 0.02f) - RadiusProfile(MathF.Max(0, u - 0.02f))) / 0.04f;
            for (int rr = 0; rr < Radial; rr++)
            {
                float vAng = rr / (float)Radial;
                float theta = vAng * MathF.PI * 2f;
                float cy = MathF.Cos(theta), sz = MathF.Sin(theta);
                float y = cy * r * 0.72f;
                float z = sz * r * 0.62f;
                var n = Vector3.Normalize(new Vector3(-dr * 0.45f, cy * 0.72f, sz * 0.62f));
                b.AddVertex(x, y, z, u, vAng, n.X, n.Y, n.Z, 0f);
            }
        }

        // Ring indices.
        for (int s = 0; s < Segments - 1; s++)
        {
            for (int rr = 0; rr < Radial; rr++)
            {
                int a = s * Radial + rr;
                int bb = (s + 1) * Radial + rr;
                int c = (s + 1) * Radial + (rr + 1) % Radial;
                int d = s * Radial + (rr + 1) % Radial;
                b.Quad(a, bb, c, d);
            }
        }

        // Nose cap.
        int nose = b.AddVertex(0.53f, 0, 0, 0, 0.5f, 1, 0, 0, 0);
        for (int rr = 0; rr < Radial; rr++)
        {
            int a = 0 * Radial + rr;
            int d = 0 * Radial + (rr + 1) % Radial;
            b.Indices.AddRange(new uint[] { (uint)nose, (uint)d, (uint)a });
        }

        // Tail base cap.
        int tailCap = b.AddVertex(-0.5f, 0, 0, 1, 0.5f, -1, 0, 0, 0);
        int baseOff = (Segments - 1) * Radial;
        for (int rr = 0; rr < Radial; rr++)
        {
            int a = baseOff + rr;
            int d = baseOff + (rr + 1) % Radial;
            b.Indices.AddRange(new uint[] { (uint)tailCap, (uint)a, (uint)d });
        }

        // Dorsal fin: base along the top, tip above; uv.y stores tip offset (mesh units).
        const float dorsalFull = 0.45f;
        int dorsalRows = 7;
        int prevBase = -1, prevTip = -1;
        for (int i = 0; i < dorsalRows; i++)
        {
            float t = i / (float)(dorsalRows - 1);
            float u = 0.21f + t * 0.53f;
            float x = 0.5f - u;
            float baseY = RadiusProfile(u) * 0.72f;
            float height = (MathF.Sin(MathF.PI * t) * 0.85f + 0.15f) * dorsalFull;
            int baseV = b.AddVertex(x, baseY, 0, u, 0f, 0, 1, 0, 1f);
            int tipV = b.AddVertex(x, baseY + height, 0, u, height, 0, 1, 0, 1f);
            if (prevBase >= 0)
            {
                b.Quad(prevBase, baseV, tipV, prevTip);
            }
            prevBase = baseV;
            prevTip = tipV;
        }

        // Pectoral fins: small quads on both flanks.
        foreach (float side in new[] { 1f, -1f })
        {
            float n = side;
            int a = b.AddVertex(0.18f, 0.02f, side * 0.50f, 0.24f, 0.5f, 0, 0, n, 2f);
            int bb = b.AddVertex(0.02f, -0.10f, side * 0.72f, 0.30f, 0.5f, 0, 0, n, 2f);
            int c = b.AddVertex(-0.06f, -0.02f, side * 0.62f, 0.34f, 0.5f, 0, 0, n, 2f);
            int d2 = b.AddVertex(0.06f, -0.16f, side * 0.58f, 0.28f, 0.5f, 0, 0, n, 2f);
            b.Quad(a, bb, c, d2);
        }

        // Tail (caudal) fin: vertical plane at x = -0.5, fork shaped in VS.
        int tailRows = 9;
        prevBase = -1; prevTip = -1;
        for (int i = 0; i < tailRows; i++)
        {
            float t = i / (float)(tailRows - 1);
            float y = (t * 2f - 1f) * 0.9f;
            int root = b.AddVertex(-0.48f, y, 0, 1.03f, 0.5f, 0, 0, 1, 3f);
            int edge = b.AddVertex(-0.5f, y, 0, 1.05f, 0.5f, 0, 0, 1, 3f);
            if (prevBase >= 0)
            {
                b.Quad(prevBase, root, edge, prevTip);
            }
            prevBase = root;
            prevTip = edge;
        }

        return new MeshData(b.Verts.ToArray(), b.Indices.ToArray(), 9);
    }

    // ── Vegetation (crossed ribbon) ──────────────────────────────────────────
    public static MeshData BuildVegetation()
    {
        var b = new Builder();
        const int Rows = 7;
        foreach (float rot in new[] { 0f, MathF.PI / 2f })
        {
            float c = MathF.Cos(rot), s = MathF.Sin(rot);
            int start = b.Verts.Count / 9;
            for (int i = 0; i < Rows; i++)
            {
                float y = i / (float)(Rows - 1);
                float nx = s, nz = c;
                int a = b.AddVertex(-0.5f * c, y, -0.5f * s, y, 0, nx, 0, nz, 0);
                int bb = b.AddVertex(0.5f * c, y, 0.5f * s, y, 1, nx, 0, nz, 0);
            }
            for (int i = 0; i < Rows - 1; i++)
            {
                int a0 = start + i * 2;
                int a1 = start + (i + 1) * 2;
                b.Quad(a0, a1, a1 + 1, a0 + 1);
            }
        }
        return new MeshData(b.Verts.ToArray(), b.Indices.ToArray(), 9);
    }

    // ── Jellyfish (bell + tentacle ribbons) ──────────────────────────────────
    public static MeshData BuildJelly()
    {
        var b = new Builder();
        const int Lats = 7;
        const int Lons = 12;

        // Bell hemisphere.
        for (int l = 0; l <= Lats; l++)
        {
            float phi = (l / (float)Lats) * (MathF.PI / 2f) * 0.96f;
            float cy = MathF.Cos(phi);
            float sr = MathF.Sin(phi);
            for (int m = 0; m < Lons; m++)
            {
                float lon = m / (float)Lons;
                float theta = lon * MathF.PI * 2f;
                float x = sr * MathF.Cos(theta);
                float z = sr * MathF.Sin(theta);
                var n = Vector3.Normalize(new Vector3(x, cy * 0.9f, z));
                b.AddVertex(x, cy, z, phi / (MathF.PI / 2f), lon, n.X, n.Y, n.Z, 0f);
            }
        }
        for (int l = 0; l < Lats; l++)
        {
            for (int m = 0; m < Lons; m++)
            {
                int a = l * Lons + m;
                int bb = (l + 1) * Lons + m;
                int c = (l + 1) * Lons + (m + 1) % Lons;
                int d = l * Lons + (m + 1) % Lons;
                b.Quad(a, bb, c, d);
            }
        }

        // Tentacles: 8 ribbons hanging from the rim.
        const int Tentacles = 8;
        const int Segs = 5;
        for (int t = 0; t < Tentacles; t++)
        {
            float ang = t / (float)Tentacles * MathF.PI * 2f;
            float cx = MathF.Cos(ang) * 0.92f;
            float cz = MathF.Sin(ang) * 0.92f;
            // perpendicular direction for ribbon width
            float px = -MathF.Sin(ang), pz = MathF.Cos(ang);
            int start = b.Verts.Count / 9;
            for (int i = 0; i <= Segs; i++)
            {
                float u = i / (float)Segs;
                float y = -u * 1.35f;
                float w = 0.05f * (1f - u * 0.6f);
                var n = Vector3.Normalize(new Vector3(px, 0.25f, pz));
                int a = b.AddVertex(cx + px * w, y, cz + pz * w, u, t / (float)Tentacles, n.X, n.Y, n.Z, 1f);
                int bb = b.AddVertex(cx - px * w, y, cz - pz * w, u, t / (float)Tentacles, n.X, n.Y, n.Z, 1f);
            }
            for (int i = 0; i < Segs; i++)
            {
                int a0 = start + i * 2;
                int a1 = start + (i + 1) * 2;
                b.Quad(a0, a1, a1 + 1, a0 + 1);
            }
        }

        return new MeshData(b.Verts.ToArray(), b.Indices.ToArray(), 9);
    }

    // ── Simple quads (non-indexed) ───────────────────────────────────────────
    public static MeshData BuildCornerQuad()
    {
        float[] verts =
        {
            -1, -1,  1, -1,  1, 1,
            -1, -1,  1, 1,  -1, 1
        };
        return new MeshData(verts, null, 2);
    }

    public static MeshData BuildFloorQuad(float extent)
    {
        float e = extent;
        float[] verts =
        {
            -e, 0, -e,   e, 0, -e,   e, 0, e,
            -e, 0, -e,   e, 0,  e,  -e, 0, e
        };
        return new MeshData(verts, null, 3);
    }
}
