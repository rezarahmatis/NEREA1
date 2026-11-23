using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using NEREA.Core.Models;
using NEREA.Engine.Interop;
using NEREA.Engine.Rendering;
using NEREA.Engine.Worlds;
using Vortice.Direct3D;

namespace NEREA.Engine;

public sealed class EngineConfig
{
    public string WorldId { get; set; } = "moonlit-pond";
    public List<SpeciesCount> Species { get; set; } = new();
    public QualitySettings Quality { get; set; } = QualityPresets.For(QualityLevel.Balanced);
    public bool ReducedMotion { get; set; }
    public bool Reflections { get; set; } = true;
    public bool Shadows { get; set; } = true;
    public bool AdaptiveQuality { get; set; } = true;
    public float FishPopulationScale { get; set; } = 1f;
    public float ParticleDensity { get; set; } = 1f;
    public float MouseSensitivity { get; set; } = 1f;
    public float RippleStrength { get; set; } = 1f;
    public float FishReaction { get; set; } = 1f;
    public int FpsLimit { get; set; } = 0;   // 0 = match display
    public float AtmosphereScale { get; set; } = 1f;
    public Func<MusicEnergy>? EnergyProvider { get; set; }
}

/// <summary>
/// The NEREA realtime engine facade. Owns the render thread, the D3D12
/// device lifecycle, the aquatic world runtime and pointer interaction.
/// The UI never touches D3D12 directly; it talks to this facade only.
/// </summary>
public sealed class NereaEngine : IDisposable
{
    private readonly BlockingCollection<Action> _commands = new(new ConcurrentQueue<Action>());
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _paused;
    private volatile bool _disposed;

    private GraphicsDevice? _device;
    private SwapChainTargets? _targets;
    private PipelineFactory? _pipelines;
    private FrameRenderer? _renderer;
    private readonly WorldRuntime _world = new();

    private EngineConfig _config = new();
    private readonly object _configSync = new();
    private int _pendingWidth, _pendingHeight;
    private volatile bool _resizePending;
    private float _renderScaleOverride = 1f;
    private float _adaptiveScale = 1f;
    private float _frameEma = 16f;
    private Vector2 _mouse = new(0.5f, 0.5f);
    private int _deviceLostRetries;
    private bool _firstFrameRaised;

    // Kept for device-lost re-initialization.
    private ISwapChainPanelNative? _panelNative;
    private Action<Action>? _marshalToUi;

    public event EventHandler? FirstFramePresented;
    public event EventHandler<string>? Degraded;
    public event EventHandler<string>? WorldAnnounced;
    public event EventHandler<object>? SwapChainCreated;
    public event EventHandler<int>? PopulationChanged;

    public bool IsRunning => _running;
    public int FishCount { get; private set; }
    public string CurrentWorldName => _world.World.Name;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>Starts the engine. Called from the UI thread.</summary>
    public void Start(ISwapChainPanelNative panelNative, Action<Action> marshalToUi, EngineConfig config, int width, int height)
    {
        _panelNative = panelNative;
        _marshalToUi = marshalToUi;
        _config = config;
        _pendingWidth = Math.Max(64, width);
        _pendingHeight = Math.Max(64, height);
        _running = true;

        _thread = new Thread(RenderLoop)
        {
            Name = "NEREA.Render",
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        _thread.Start();

        _commands.Add(() =>
        {
            InitializeGraphics();
            ApplyWorldInternal();
        });
    }

    private void InitializeGraphics()
    {
        try
        {
            _device = GraphicsDevice.Create(allowDebugLayer: true);
            _targets = new SwapChainTargets(_device);
            var swapChain = _targets.CreateSwapChain(_device.Queue, _pendingWidth, _pendingHeight);
            SwapChainCreated?.Invoke(this, swapChain);

            _pipelines = new PipelineFactory(_device);
            _renderer = new FrameRenderer(_device, _pipelines);
            _renderer.SetTargets(_targets);
        }
        catch (Exception ex)
        {
            Degraded?.Invoke(this, ex.Message);
            _running = false;
            return;
        }

        // Bind the composition swap chain to the panel on the UI thread.
        var swapChainObject = _targets.SwapChain!;
        _marshalToUi?.Invoke(() =>
        {
            try
            {
                _panelNative?.SetSwapChain(swapChainObject);
            }
            catch (Exception ex)
            {
                Degraded?.Invoke(this, ex.Message);
            }
        });
    }

    private void ApplyWorldInternal()
    {
        lock (_configSync)
        {
            var cfg = _config;
            var world = Core.Content.WorldCatalog.ById(cfg.WorldId) ?? Core.Content.WorldCatalog.Default;
            _world.AtmosphereScale = cfg.AtmosphereScale;
            _world.ApplyWorld(world, cfg.Species, cfg.Quality, cfg.FishPopulationScale, cfg.ParticleDensity);
            _world.Ripples.SetStrength(cfg.RippleStrength);
            FishCount = _world.Fish.AgentCount;
        }
        PopulationChanged?.Invoke(this, FishCount);
    }

    // ── Public commands (thread safe) ────────────────────────────────────────

    public void SetWorld(string worldId, List<SpeciesCount> species) =>
        _commands.Add(() =>
        {
            bool worldChanged;
            lock (_configSync)
            {
                worldChanged = _config.WorldId != worldId;
                _config.WorldId = worldId;
                _config.Species = species;
            }
            ApplyWorldInternal();
            if (worldChanged)
                WorldAnnounced?.Invoke(this, _world.World.Name);
        });

    public void SetSpecies(List<SpeciesCount> species) =>
        _commands.Add(() =>
        {
            lock (_configSync) _config.Species = species;
            ApplyWorldInternal();
        });

    public void UpdateQuality(QualitySettings quality, bool reflections, bool shadows, bool adaptive,
        float fishPopulation, float particleDensity, int fpsLimit) =>
        _commands.Add(() =>
        {
            lock (_configSync)
            {
                _config.Quality = quality;
                _config.Reflections = reflections;
                _config.Shadows = shadows;
                _config.AdaptiveQuality = adaptive;
                _config.FishPopulationScale = fishPopulation;
                _config.ParticleDensity = particleDensity;
                _config.FpsLimit = fpsLimit;
            }
            ApplyWorldInternal();
            _resizePending = true;
        });

    public void UpdateInteraction(float mouseSensitivity, float rippleStrength, float fishReaction) =>
        _commands.Add(() =>
        {
            lock (_configSync)
            {
                _config.MouseSensitivity = mouseSensitivity;
                _config.RippleStrength = rippleStrength;
                _config.FishReaction = fishReaction;
            }
            _world.Ripples.SetStrength(rippleStrength);
        });

    public void SetAtmosphere(float scale) =>
        _commands.Add(() => { _world.AtmosphereScale = scale; });

    public void SetReducedMotion(bool reduced) =>
        _commands.Add(() => { lock (_configSync) _config.ReducedMotion = reduced; });

    public void SetPaused(bool paused) => _paused = paused;

    public void SetAmbientMode(bool ambient)
    {
        _renderScaleOverride = ambient ? 0.85f : 1f;
        _resizePending = true;
    }

    public void Resize(int width, int height)
    {
        _pendingWidth = Math.Max(64, width);
        _pendingHeight = Math.Max(64, height);
        _resizePending = true;
    }

    public void OnPointerMove(float x, float y, float seconds)
    {
        _mouse = new Vector2(x, y);
        var world = PointerToFloor(x, y);
        lock (_configSync) _world.Ripples.OnPointerMove(world, seconds, _config.MouseSensitivity);
    }

    public void OnPointerPress(float x, float y)
    {
        _mouse = new Vector2(x, y);
        var world = PointerToFloor(x, y);
        _world.Ripples.OnPointerPress(world);
        _world.Fish.ApplyImpulse(new Vector3(world.X, -_world.World.Depth * 0.4f, world.Y), 7f, 1.0f);
    }

    public void OnPointerDrag(float x, float y, float seconds)
    {
        _mouse = new Vector2(x, y);
        var world = PointerToFloor(x, y);
        lock (_configSync) _world.Ripples.OnPointerDrag(world, seconds);
    }

    public void OnPointerRelease() { /* reserved */ }

    /// <summary>Project a normalized pointer through the camera onto a mid-water plane.</summary>
    private Vector2 PointerToFloor(float nx, float ny)
    {
        float ndcX = nx * 2f - 1f;
        float ndcY = 1f - ny * 2f;
        var cam = _world.Camera;
        var forward = Vector3.Normalize(cam.LookAt - cam.Position);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        var up = Vector3.Normalize(Vector3.Cross(forward, right));
        float tanH = MathF.Tan(cam.FovRadians * 0.5f);
        float tanW = tanH * MathF.Max(0.2f, cam.Aspect);
        var ray = Vector3.Normalize(forward + right * ndcX * tanW + up * ndcY * tanH);

        float planeY = -_world.World.Depth * 0.5f;
        float denom = ray.Y;
        if (MathF.Abs(denom) < 1e-4f) return new Vector2(0, 0);
        float t = (planeY - cam.Position.Y) / denom;
        if (t < 0) t = 0;
        var hit = cam.Position + ray * t;
        return new Vector2(hit.X, hit.Z);
    }

    // ── Render loop ──────────────────────────────────────────────────────────

    private unsafe void RenderLoop()
    {
        var frameSw = Stopwatch.StartNew();
        double simAccum = 0;
        double lastTime = 0;

        while (_running && !_disposed)
        {
            try
            {
                while (_commands.TryTake(out var cmd)) cmd();

                if (_device is null || _targets is null || _pipelines is null || _renderer is null)
                {
                    Thread.Sleep(16);
                    continue;
                }

                if (_resizePending)
                {
                    _resizePending = false;
                    float scale;
                    lock (_configSync)
                        scale = Math.Min(_config.Quality.RenderScale, _renderScaleOverride) * _adaptiveScale;
                    _targets.Resize(_pendingWidth, _pendingHeight, Math.Clamp(scale, 0.5f, 1.6f));
                    _renderer.SetTargets(_targets);
                }

                if (_paused)
                {
                    Thread.Sleep(120);
                    lastTime = _clock.Elapsed.TotalSeconds;
                    frameSw.Restart();
                    continue;
                }

                var sw = Stopwatch.StartNew();
                float time = (float)_clock.Elapsed.TotalSeconds;
                float dt = Math.Clamp(time - (float)lastTime, 0.001f, 0.1f);
                lastTime = time;

                MusicEnergy energy = _config.EnergyProvider?.Invoke() ?? MusicEnergy.Silence;

                double simStep = 1.0 / Math.Max(20, _config.Quality.SimHz);
                simAccum += dt;
                int simLoops = 0;
                while (simAccum >= simStep && simLoops < 3)
                {
                    simAccum -= simStep;
                    simLoops++;
                    _world.Fish.SetCamera(_world.Camera.Position);
                    _world.Fish.Update((float)simStep, energy, _config.FishReaction);
                    _world.Particles.Update((float)simStep, energy, energy.Treble);
                    _world.Ripples.Update((float)simStep);
                }

                // Beat-driven ambient ripple (subtle, floor-shading only).
                if (energy.Beat > 0.92f && energy.Level > 0.3f)
                {
                    float ang = time * 0.7f;
                    _world.Ripples.OnBeat(new Vector2(MathF.Cos(ang) * 8f, MathF.Sin(ang) * 8f));
                }

                // ── Fill GPU data ──
                int frame = _device.CpuFrameIndex;
                var constants = new PerFrameConstants();
                lock (_configSync)
                {
                    _world.FillConstants(ref constants, time, energy, _config.Quality,
                        _config.Reflections, _config.Shadows, _mouse, _config.ReducedMotion,
                        _pendingWidth / (float)Math.Max(1, _pendingHeight));
                }

                byte* uploadBase = _renderer.Instances.UploadPointer(frame);
                var fishSpan = new Span<FishInstance>((FishInstance*)(uploadBase + InstanceLayout.Fish), EngineLimits.MaxFish);
                var jellySpan = new Span<JellyInstance>((JellyInstance*)(uploadBase + InstanceLayout.Jelly), EngineLimits.MaxJelly);
                var particleSpan = new Span<ParticleInstance>((ParticleInstance*)(uploadBase + InstanceLayout.Particles), EngineLimits.MaxParticles);
                var vegSpan = new Span<VegInstance>((VegInstance*)(uploadBase + InstanceLayout.Vegetation), EngineLimits.MaxVegetation);
                var shadowSpan = new Span<ShadowFish>((ShadowFish*)(uploadBase + InstanceLayout.Shadows), EngineLimits.MaxShadows);
                var rippleSpan = new Span<ParticleInstance>((ParticleInstance*)(uploadBase + InstanceLayout.Ripples), EngineLimits.MaxRipples);

                int fishCount = _world.Fish.FillInstances(fishSpan, jellySpan, out int jellyCount);
                int particleCount = _world.Particles.FillInstances(particleSpan, energy.Treble);
                _world.CopyVegetation(vegSpan);
                int shadowCount = _world.Fish.FillShadows(shadowSpan);
                int rippleCount = _world.Ripples.FillInstances(rippleSpan);
                _world.Ripples.Fill(ref constants);

                var counts = new FrameDrawCounts
                {
                    Fish = fishCount,
                    Jelly = jellyCount,
                    Particles = particleCount,
                    Motes = _world.Particles.MoteCount,
                    Vegetation = _world.VegetationCount,
                    Ripples = rippleCount,
                    Shadows = shadowCount,
                    Bloom = _config.Quality.Bloom,
                    BloomStrength = _config.Quality.BloomStrength * _world.World.Bloom,
                    Vsync = _config.Quality.TargetFps == 0 || _config.Quality.TargetFps >= 60
                };

                _device.BeginFrame();
                _renderer.RenderFrame(frame, constants, counts);
                _device.EndFrameAndExecute();

                sw.Stop();
                AdaptiveCheck((float)sw.Elapsed.TotalMilliseconds);

                if (!_firstFrameRaised)
                {
                    _firstFrameRaised = true;
                    FirstFramePresented?.Invoke(this, EventArgs.Empty);
                }

                FramePacing(frameSw, _config.Quality.TargetFps);
            }
            catch (Exception ex)
            {
                if (IsDeviceRemoved(ex))
                {
                    _deviceLostRetries++;
                    if (_deviceLostRetries <= 2)
                    {
                        try
                        {
                            TeardownGraphics();
                            InitializeGraphics();
                            continue;
                        }
                        catch
                        {
                            Degraded?.Invoke(this, ex.Message);
                            _running = false;
                        }
                    }
                    else
                    {
                        Degraded?.Invoke(this, ex.Message);
                        _running = false;
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }
    }

    private void FramePacing(Stopwatch frameSw, int targetFps)
    {
        if (targetFps <= 0 || targetFps >= 120) return;
        double targetMs = 1000.0 / targetFps;
        double elapsed = frameSw.Elapsed.TotalMilliseconds;
        if (elapsed < targetMs)
            Thread.Sleep((int)(targetMs - elapsed));
        frameSw.Restart();
    }

    private void AdaptiveCheck(float frameMs)
    {
        _frameEma = _frameEma * 0.92f + frameMs * 0.08f;
        lock (_configSync)
        {
            if (!_config.AdaptiveQuality) return;
        }
        if (_frameEma > 26f && _adaptiveScale > 0.7f)
        {
            _adaptiveScale -= 0.06f;
            _resizePending = true;
            _frameEma = 16f;
        }
        else if (_frameEma < 11f && _adaptiveScale < 1f)
        {
            _adaptiveScale += 0.04f;
            if (_adaptiveScale > 1f) _adaptiveScale = 1f;
            _resizePending = true;
        }
    }

    private static bool IsDeviceRemoved(Exception ex) =>
        (ex.Message.Contains("device", StringComparison.OrdinalIgnoreCase) &&
         ex.Message.Contains("removed", StringComparison.OrdinalIgnoreCase)) ||
        ex.Message.Contains("DXGI_ERROR", StringComparison.OrdinalIgnoreCase);

    private void TeardownGraphics()
    {
        _renderer?.Dispose();
        _pipelines?.Dispose();
        _targets?.Dispose();
        _device?.Dispose();
        _renderer = null;
        _pipelines = null;
        _targets = null;
        _device = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        try { _commands.CompleteAdding(); } catch { }
        try { _thread?.Join(800); } catch { }
        TeardownGraphics();
    }
}
