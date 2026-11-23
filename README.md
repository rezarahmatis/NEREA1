# NEREA

**Premium Native Windows Music Player & Living Aquatic Worlds**

*Let the music create the world.*

NEREA is not simply a music player. It is a premium audiovisual experience where
your music becomes the heartbeat of a living aquatic ecosystem. Import your own
music, press play, and enter a cinematic underwater world filled with naturally
behaving fish, procedural water, light shafts, caustics, particles and
vegetation — rendered in real time with Direct3D 12.

NEREA is a combination of:

- a premium music player
- a digital aquarium
- an interactive art installation
- a relaxing ambient application
- a cinematic realtime simulation

The music does not play *inside* NEREA. **The music creates the world.**

---

## Highlights

### The world

- A genuine realtime Direct3D 12 engine with HLSL shaders — no sprites, no
  faked backgrounds: HDR water volume, god rays, procedural caustics,
  depth-graded fog, sand floor with soft fish shadows, swaying vegetation,
  drifting motes, rising bubbles, translucent jellyfish, ACES tone mapping,
  bloom, vignette and film grain.
- Procedural fish behavior: wandering, schooling (alignment / cohesion /
  separation), avoidance, depth preference, curiosity toward the viewer,
  fear responses, resting cycles — species behave differently by design.
- Music-reactive ecosystem: bass, midrange, treble, level and beat energy are
  extracted live from the audio stream and drive water movement, light
  intensity, schooling activity, particle shimmer and fish activity —
  always organically, never as a flashy visualizer.
- **38 distinct worlds** (Moonlit Pond, Japanese Koi Garden, Kelp Forest,
  Coral Reef, Bioluminescent Ocean, Midnight Abyss, Arctic Underwater,
  Volcanic Underwater, Atlantis…), each with its own lighting, water,
  vegetation, particles, camera framing and suggested population.
- **68 species** across freshwater, tropical reef, large ocean, deep sea and
  aquatic life — koi, clownfish, manta rays, whale sharks, anglerfish,
  jellyfish, octopus and more. Species are pure data: adding one never
  requires touching the renderer.
- Mouse interaction: ripples follow the cursor, clicks scatter fish, dragging
  disturbs the water.
- Cinematic mode (F11-level immersion, UI fades, cursor hides) and Ambient
  mode (low power, stable frame pacing, ecosystem continues indefinitely).

### The player

- Full local music playback: MP3, WAV, FLAC, OGG, M4A, AAC, WMA
  (Media Foundation + NVorbis), WASAPI shared-mode output.
- Music library with Songs / Albums / Artists / Playlists / Favorites /
  Recently Played / Folders / Up Next, search, sorting, context menus and
  drag-and-drop import. Library persists in SQLite.
- Playlists: create, rename, duplicate, delete, add/remove tracks.
- Transport: play/pause, previous/next, seek, volume, mute, shuffle,
  repeat (off / all / one), live queue.
- Windows integration: System Media Transport Controls (lock screen, media
  overlay), hardware media keys, minimize to tray with tray menu, optional
  launch with Windows, proper application icon and metadata.
- Offline first: no account, no cloud, no network calls. Works fully offline.
- Graceful failure handling: an unreadable track is skipped; a GPU that fails
  D3D12 initialization falls back to a reduced static experience while music
  keeps playing.

### The identity

A quiet serif wordmark and an abstract droplet-in-ripples symbol, present only
in the upper-left corner of the living world. The center of the screen belongs
to the world itself — never to titles, names or chrome.

---

## Building

### Requirements

- Windows 10 1809+ (19041+ recommended) / Windows 11
- [Visual Studio 2022 17.8+](https://visualstudio.microsoft.com/) with the
  **.NET desktop development** workload, or the .NET **8.0 SDK** alone
- Windows App SDK 1.7 (restored automatically via NuGet)

### Command line

```bat
git clone https://github.com/<you>/NEREA.git
cd NEREA
dotnet build NEREA.sln -c Release -p:Platform=x64
dotnet publish src/NEREA.App/NEREA.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64
```

The app publishes **self-contained x64** — no runtime installation is needed;
run `NEREA.exe` from the publish folder.

### Visual Studio

1. Open `NEREA.sln`.
2. Select `x64` / `Release`.
3. Set `NEREA.App` as the startup project and press **F5**.

### CI / packaging

GitHub Actions (`.github/workflows/build-windows.yml`) builds, publishes,
zips (`NEREA-win-x64.zip`) and packages an Inno Setup installer
(`NEREA-Setup.exe`) on every push, and attaches both to GitHub Releases on
`v*` tags. No signing keys are involved.

---

## Keyboard & interaction

| Input | Action |
| --- | --- |
| `Space` | Play / pause |
| `N` / `P` | Next / previous track |
| `M` | Mute |
| `←` / `→` | Seek ±5 s |
| `↑` / `↓` | Volume |
| `F11` | Fullscreen |
| `C` | Cinematic mode |
| `A` | Ambient mode |
| `Ctrl+O` | Add music |
| `Ctrl+F` | Open library |
| `Esc` | Close panels / exit fullscreen |
| Move mouse | Ripples; fish subtly react |
| Click | Strong ripple, fish scatter |
| Drag | Continuous water disturbance |
| Double-click the world | Toggle cinematic mode |
| Mouse wheel | Volume |

Controls fade after ~3.5 s of stillness and reappear when the cursor moves
near the bottom of the window.

---

## Repository layout

```
NEREA/
├── .github/workflows/build-windows.yml   CI: build → publish → zip → installer
├── branding/                             Logo source (SVG, PNG renders)
├── installer/NEREA.iss                   Inno Setup installer script
├── docs/architecture.md                  Engine & app architecture notes
└── src/
    ├── NEREA.Core/       Models, species & world catalogs, SQLite library,
    │                     settings, playlists, aquarium presets, services
    ├── NEREA.Audio/      WASAPI playback (NAudio), decoding, live FFT
    │                     spectrum analysis, playback controller
    ├── NEREA.Engine/     Direct3D 12 renderer, HLSL shaders, procedural
    │                     meshes, fish AI simulation, particles, camera
    └── NEREA.App/        WinUI 3 shell, immersive UI, library/aquarium/
                          settings panels, view models, SMTC, tray
```

## Extending

- **Add a species:** append one data entry in
  `src/NEREA.Core/Content/SpeciesCatalog.cs` (colors, proportions, behavior).
  The renderer reshapes a shared procedural mesh from that data.
- **Add a world:** append one entry in
  `src/NEREA.Core/Content/WorldCatalog.cs` (colors, lighting, fog,
  vegetation, camera, default population). No engine changes.
- See `docs/architecture.md` for how the layers fit together.

## Data locations

All user data is local, under `%LOCALAPPDATA%\NEREA\`:

- `library.db` — the music library (SQLite)
- `settings.json` — preferences and last session
- `aquariums.json` — saved custom aquariums
- `artwork/` — album artwork cache

Uninstalling NEREA keeps this data so a reinstall restores your library and
worlds.

## License

MIT — see [LICENSE](LICENSE).
