using System.Numerics;
using NEREA.Core.Models;

namespace NEREA.Engine.Simulation;

/// <summary>
/// Cinematic camera: a slow multi-frequency drift around the world center
/// with gentle mouse parallax. Reduced motion freezes the drift.
/// </summary>
public sealed class CameraRig
{
    public Vector3 Position { get; private set; }
    public Vector3 LookAt { get; private set; }
    public float FovRadians { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }

    private float _baseAngle = 0.9f;

    public void Update(float time, WorldDefinition world, Vector2 mouse, bool reducedMotion, float aspect)
    {
        FovRadians = world.CameraFov * MathF.PI / 180f;

        float drift = reducedMotion ? 0f : MathF.Sin(time * 0.021f) * 0.55f + MathF.Sin(time * 0.008f) * 0.45f;
        float angle = _baseAngle + drift;
        float radius = world.CameraRadius * (1f + (reducedMotion ? 0f : MathF.Sin(time * 0.011f) * 0.08f));
        float height = -world.Depth * world.CameraHeightFrac + (reducedMotion ? 0f : MathF.Sin(time * 0.03f) * 1.1f);

        var target = new Vector3(
            reducedMotion ? 0f : MathF.Sin(time * 0.017f) * 2.2f,
            -world.Depth * 0.5f + (reducedMotion ? 0f : MathF.Sin(time * 0.012f) * 1.4f),
            reducedMotion ? 0f : MathF.Cos(time * 0.013f) * 2.2f);

        // Mouse parallax: look slightly toward the cursor.
        var parallax = reducedMotion ? Vector3.Zero : new Vector3(
            (mouse.X - 0.5f) * 4.5f, (mouse.Y - 0.5f) * -2.8f, 0f);

        Position = new Vector3(MathF.Cos(angle) * radius, height, MathF.Sin(angle) * radius);
        LookAt = target + parallax;

        var view = Matrix4x4.CreateLookAt(Position, LookAt, Vector3.UnitY);
        var forward = Vector3.Normalize(LookAt - Position);
        Right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        Up = Vector3.Normalize(Vector3.Cross(forward, Right));

        Aspect = aspect;
    }

    public float Aspect { get; private set; } = 16f / 9f;

    public Matrix4x4 ViewProjection()
    {
        var view = Matrix4x4.CreateLookAt(Position, LookAt, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(FovRadians, MathF.Max(0.2f, Aspect), 0.1f, 220f);
        return view * proj;
    }
}
