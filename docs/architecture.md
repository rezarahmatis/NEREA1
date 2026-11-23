# NEREA architecture

NEREA separates the **UI layer**, the **realtime rendering engine**, the
**audio engine** and the **simulation** so that none of them can block,
crash or degrade the others.

```
┌────────────────────────────── WinUI 3 (NEREA.App) ─────────────────────────────┐
│  MainWindow · panels (Library / Aquarium / Settings) · view models (MVVM)      │
│  SMTC · tray · media keys · drag & drop · splash · idle chrome fading          │
└──────────────┬────────────────────────────────────────────┬────────────────────┘
               │ commands (thread-safe)                     │ energy snapshot
┌──────────────▼──────────────────┐   ┌─────────────────────▼──────────────────┐
│  NEREA.Engine (render thread)   │   │  NEREA.Audio (WASAPI, its own thread)  │
│  D3D12 device / swapchain       │   │  NAudio pipeline → 48 kHz float stereo │
│  HLSL passes:                   │   │  → SampleTap → FFT (2048) → bands      │
│   water volume · god rays       │◄──┤  bass / mid / treble / beat / level    │
│   caustic floor + shadows       │   └────────────────────────────────────────┘
│   vegetation · fish · jelly     │
│   particles · ripples           │
│   bloom → ACES composite        │
│                                 │
│  Simulation (same thread, 30-60 │
│  Hz steps): boids + species     │
│  behaviors, spatial hash grid,  │
│  particles, ripples, camera     │
└──────────────▲──────────────────┘
               │ data-driven
┌──────────────┴────────────────── NEREA.Core ───────────────────────────────────┐
│ SpeciesCatalog (68) · WorldCatalog (38) · QualitySettings                      │
│ SQLite library · JSON settings · aquarium presets · playlists                  │
└────────────────────────────────────────────────────────────────────────────────┘
```

## Engine (NEREA.Engine)

- `NereaEngine` — facade owning the render thread. UI pushes commands through
  a `BlockingCollection<Action>`; the engine pulls energy from a
  `Func<MusicEnergy>` provider each frame. No locks on the hot path except a
  short config lock.
- `GraphicsDevice` — D3D12 device (FL 11_0), direct queue, 2-frame
  allocator/fence rotation, WARP fallback if no hardware adapter exists.
- `SwapChainTargets` — `CreateSwapChainForComposition` bound to the WinUI
  `SwapChainPanel` via `ISwapChainPanelNative`; HDR scene target
  (`R16G16B16A16_Float`), `D32` depth, half-res bloom chain.
- `PipelineFactory` — two root signatures (scene: root CBV + root structured
  SRVs, zero descriptor heaps; post: heap SRVs + static linear sampler +
  inline constants) and 11 PSOs.
- `ShaderLibrary` — embedded HLSL compiled at startup with D3DCompile
  (SM 5.0, `d3dcompiler_47` ships with Windows); includes resolved manually.
- `FrameRenderer` — pass orchestration with explicit resource-state tracking:
  env → floor → vegetation → fish → jelly → motes/bubbles → ripples →
  bright-extract → blur H/V → composite (ACES, vignette, grain, dither).
- `InstanceBuffer` — one persistent default-heap buffer with a 2-frame upload
  ring, mapped once, written via raw pointers, copied per frame. Zero
  per-frame allocations anywhere in the loop.
- `MeshBuilder` — procedural geometry: one generic fish (body + dorsal +
  pectorals + forked tail) reshaped in the vertex shader per species; crossed
  kelp/grass ribbons; jelly bell + tentacle ribbons; billboard quads.

### Shaders (HLSL, SM 5.0)

| File | Passes |
| --- | --- |
| `Common.hlsli` | per-frame constants, instance structs, noise, caustics, fog, ACES |
| `Environment.hlsl` | fullscreen water volume: gradient, layered god rays, sun glow, shimmer |
| `Floor.hlsl` | sandbed: value-noise grain, dune shading, caustics, 48 soft shadows, ripple bands |
| `Fish.hlsl` | species reshaping (head bluntness, dorsal, fork, rays), undulation, banking, patterns, eyes, glow |
| `Vegetation.hlsl` | sway driven by time + midrange music energy |
| `Particles.hlsl` | motes (additive, treble-twinkle) and bubbles (rim + highlight) |
| `Ripples.hlsl` | interaction rings on the floor |
| `Jelly.hlsl` | pulsing translucent bells with trailing ribbons (additive) |
| `Post.hlsl` | bloom extract, 9-tap separable blur, composite |

The per-frame cbuffer (`PerFrameConstants`) mirrors `Common.hlsli` byte for
byte — layout is the contract.

## Simulation

`FishSimulation` keeps agents in a flat struct array and queries neighbors via
a linked-list spatial hash (`cell 3.2 m`, no allocations). Forces: species
weights for alignment/cohesion/separation, smooth wander, depth-band steering,
soft cylindrical boundary, fear impulses from pointer ripples, curiosity
toward the camera during calm music, resting cycles, and a turn-rate limiter
for believable inertia. Tail phase advances with true speed, so motion and
animation are coupled. Jelly-form agents route to the translucent jelly
pipeline; large fish project soft shadows onto the floor.

## Audio (NEREA.Audio)

`AudioPlayerService` builds a chain per track — `WaveStream` (Media
Foundation or NVorbis) → `WdlResamplingSampleProvider` (device mix rate) →
channel adapter → tap → `VolumeSampleProvider` → `WasapiOut` (shared mode).
Seeks operate on the stream itself and survive device switches. The tap feeds
`SpectrumAnalyzer`: a 8192-sample ring, 2048-point Hann-windowed FFT at ~25 Hz,
band energies with attack/release smoothing, bass-flux beat detection and peak
tracking for loudness normalization. Playback is fully independent of
rendering: pause, world changes, panels and minimization never interrupt it.

## App (NEREA.App)

MVVM with `CommunityToolkit.Mvvm`; manual DI via
`Microsoft.Extensions.DependencyInjection`. The window extends content into
the title bar; a 46 px drag strip keeps native window dragging. comctl32
subclassing (`SetWindowSubclass`) provides `WM_APPCOMMAND` media keys, a
960×600 minimum size, cursor hiding for cinematic/ambient idle and the shell
tray icon. SMTC attaches through `ISystemMediaTransportControlsInterop`
(activated via `RoGetActivationFactory`) and degrades silently if unavailable.
Settings, aquarium presets and the last session persist as JSON/SQLite under
`%LOCALAPPDATA%\NEREA`.

## Quality & power

Five presets (Battery → Ultra) scale render resolution, fish budget, motes,
vegetation, caustics octaves, bloom, shadows, simulation rate and FPS cap.
Adaptive quality watches the frame-time EMA and shrinks/grows the render
scale within 0.7–1.0×. When minimized, rendering pauses (music continues);
Ambient mode drops to a lighter frame budget for long-running sessions.
