using NEREA.Core.Models;

namespace NEREA.Core.Content;

/// <summary>
/// The NEREA world catalog. Each world is pure data: lighting, water, floor,
/// vegetation, particles, camera framing and a suggested population.
/// </summary>
public static class WorldCatalog
{
    private sealed class B
    {
        public string Id = "", Name = "", Category = "Ocean";
        public float Depth = 12f, BoundsRadius = 26f;
        public string WaterShallow = "#2E7F8F", WaterDeep = "#03222E", FloorColor = "#C2B28A";
        public string LightColor = "#BFE7E2"; public float LightIntensity = 1f, LightYaw = 35f, LightPitch = 58f;
        public float FogDensity = 0.05f, GodRays = 0.5f, Caustics = 0.6f, CausticsScale = 0.15f;
        public float CameraRadius = 17f, CameraHeightFrac = 0.42f, CameraFov = 55f;
        public VegType Vegetation = VegType.None; public float VegDensity = 0.3f;
        public string VegColorA = "#3E6B4A", VegColorB = "#7BA35F";
        public float VegHeightLo = 0.8f, VegHeightHi = 2.4f;
        public float Motes = 0.5f, Bubbles = 0.3f;
        public float Shimmer = 0.5f, Bloom = 0.35f, Grain = 0.45f, Vignette = 0.45f;
        public float MusicResponse = 1f, ShadowStrength = 0.5f;
        public List<(string, int)> Species = new();

        public WorldDefinition To() => new()
        {
            Id = Id, Name = Name, Category = Category,
            Depth = Depth, BoundsRadius = BoundsRadius,
            WaterShallow = WaterShallow, WaterDeep = WaterDeep, FloorColor = FloorColor,
            LightColor = LightColor, LightIntensity = LightIntensity, LightYaw = LightYaw, LightPitch = LightPitch,
            FogDensity = FogDensity, GodRays = GodRays, Caustics = Caustics, CausticsScale = CausticsScale,
            CameraRadius = CameraRadius, CameraHeightFrac = CameraHeightFrac, CameraFov = CameraFov,
            Vegetation = Vegetation, VegDensity = VegDensity, VegColorA = VegColorA, VegColorB = VegColorB,
            VegHeightLo = VegHeightLo, VegHeightHi = VegHeightHi,
            Motes = Motes, Bubbles = Bubbles,
            Shimmer = Shimmer, Bloom = Bloom, Grain = Grain, Vignette = Vignette,
            MusicResponse = MusicResponse, ShadowStrength = ShadowStrength,
            DefaultSpecies = Species.AsReadOnly()
        };
    }

    private static WorldDefinition W(string id, string name, string category, Action<B> cfg)
    {
        var b = new B { Id = id, Name = name, Category = category };
        cfg(b);
        return b.To();
    }

    public static IReadOnlyList<WorldDefinition> All { get; } = Build().AsReadOnly();

    public static WorldDefinition? ById(string id) => _byId.TryGetValue(id, out var w) ? w : null;

    public static WorldDefinition Default => _byId["moonlit-pond"];

    private static readonly Dictionary<string, WorldDefinition> _byId =
        Build().ToDictionary(w => w.Id, StringComparer.Ordinal);

    private static List<WorldDefinition> Build() => new()
    {
        // ───────────────────────────── Freshwater ─────────────────────────────
        W("japanese-koi-garden", "Japanese Koi Garden", "Freshwater", b =>
        {
            b.Depth = 7f; b.WaterShallow = "#3E8E7E"; b.WaterDeep = "#0E3A32"; b.FloorColor = "#D8C9A0";
            b.LightColor = "#F2E8C8"; b.LightIntensity = 1.1f; b.LightYaw = 40f; b.LightPitch = 62f;
            b.FogDensity = 0.055f; b.GodRays = 0.65f; b.Caustics = 0.85f; b.CausticsScale = 0.13f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.35f; b.VegColorA = "#4E7A4A"; b.VegColorB = "#8AA85E";
            b.VegHeightLo = 0.5f; b.VegHeightHi = 1.6f;
            b.Motes = 0.45f; b.Bubbles = 0.2f; b.Shimmer = 0.6f; b.Bloom = 0.35f;
            b.Species = new() { ("koi", 10), ("goldfish", 4), ("common-carp", 2) };
        }),
        W("shallow-garden-pond", "Shallow Garden Pond", "Freshwater", b =>
        {
            b.Depth = 4.5f; b.WaterShallow = "#4E9E6E"; b.WaterDeep = "#144A34"; b.FloorColor = "#CFC088";
            b.LightIntensity = 1.15f; b.GodRays = 0.7f; b.Caustics = 0.9f; b.CausticsScale = 0.18f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.5f; b.VegHeightLo = 0.6f; b.VegHeightHi = 1.8f;
            b.Motes = 0.5f; b.Shimmer = 0.7f;
            b.Species = new() { ("goldfish", 6), ("guppy", 8), ("platy", 4) };
        }),
        W("mountain-lake", "Mountain Lake", "Freshwater", b =>
        {
            b.Depth = 14f; b.WaterShallow = "#3E7E96"; b.WaterDeep = "#0A2438"; b.FloorColor = "#8A9498";
            b.LightColor = "#CFE4E8"; b.LightPitch = 52f; b.FogDensity = 0.042f; b.GodRays = 0.5f; b.Caustics = 0.55f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.18f; b.VegColorA = "#4A6450"; b.VegColorB = "#6E8A6A";
            b.Motes = 0.3f; b.Bubbles = 0.1f;
            b.Species = new() { ("trout", 4), ("perch", 3), ("minnow", 10) };
        }),
        W("alpine-lake", "Alpine Lake", "Freshwater", b =>
        {
            b.Depth = 16f; b.WaterShallow = "#5E9EB8"; b.WaterDeep = "#0C2C44"; b.FloorColor = "#A8B0B4";
            b.LightColor = "#DFF2F8"; b.LightPitch = 55f; b.FogDensity = 0.034f; b.GodRays = 0.55f; b.Caustics = 0.7f;
            b.Vegetation = VegType.None; b.Motes = 0.2f; b.Bubbles = 0.08f; b.Vignette = 0.5f;
            b.Species = new() { ("trout", 5), ("minnow", 12) };
        }),
        W("amazon-river", "Amazon River", "Freshwater", b =>
        {
            b.Depth = 9f; b.WaterShallow = "#7A8E4E"; b.WaterDeep = "#2A3418"; b.FloorColor = "#8A744E";
            b.LightColor = "#E8D8A8"; b.LightIntensity = 0.9f; b.LightYaw = 20f; b.LightPitch = 60f;
            b.FogDensity = 0.085f; b.GodRays = 0.4f; b.Caustics = 0.3f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.7f; b.VegColorA = "#3E5E38"; b.VegColorB = "#6E8E4E";
            b.VegHeightLo = 0.6f; b.VegHeightHi = 2.2f; b.Motes = 0.7f;
            b.Species = new() { ("neon-tetra", 12), ("angelfish", 3), ("discus", 2), ("oscar", 2) };
        }),
        W("tropical-river", "Tropical River", "Freshwater", b =>
        {
            b.Depth = 8f; b.WaterShallow = "#5E9E6E"; b.WaterDeep = "#1A4030"; b.FloorColor = "#B8A878";
            b.LightColor = "#F2E4B8"; b.FogDensity = 0.06f; b.GodRays = 0.55f; b.Caustics = 0.6f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.55f; b.Motes = 0.55f;
            b.Species = new() { ("rainbowfish", 8), ("gourami", 3), ("loach", 2) };
        }),
        W("mangrove-lagoon", "Mangrove Lagoon", "Freshwater", b =>
        {
            b.Depth = 6f; b.WaterShallow = "#4E8E6A"; b.WaterDeep = "#123A2A"; b.FloorColor = "#6E5E44";
            b.LightColor = "#E8DFB8"; b.LightIntensity = 0.95f; b.FogDensity = 0.068f; b.GodRays = 0.45f; b.Caustics = 0.4f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.45f; b.VegColorA = "#3E5E3A"; b.VegColorB = "#5E7E4E";
            b.Motes = 0.6f; b.Bubbles = 0.25f;
            b.Species = new() { ("molly", 6), ("guppy", 8), ("shrimp", 8), ("crab", 2) };
        }),
        W("wetland-marsh", "Wetland Marsh", "Freshwater", b =>
        {
            b.Depth = 5f; b.WaterShallow = "#6E8E4E"; b.WaterDeep = "#223A20"; b.FloorColor = "#7E6E4E";
            b.LightColor = "#E8DFA8"; b.LightIntensity = 0.9f; b.FogDensity = 0.09f; b.GodRays = 0.35f; b.Caustics = 0.3f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.8f; b.VegColorA = "#4A5E32"; b.VegColorB = "#7E944E";
            b.Motes = 0.8f;
            b.Species = new() { ("perch", 4), ("minnow", 14), ("pike", 1) };
        }),
        W("misty-forest-lake", "Misty Forest Lake", "Freshwater", b =>
        {
            b.Depth = 11f; b.WaterShallow = "#5E8E88"; b.WaterDeep = "#1A3A40"; b.FloorColor = "#8A9284";
            b.LightColor = "#CFE0D8"; b.LightIntensity = 0.85f; b.LightPitch = 48f;
            b.FogDensity = 0.075f; b.GodRays = 0.6f; b.Caustics = 0.4f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.3f; b.VegColorA = "#4E6450"; b.VegColorB = "#7A8E72";
            b.Motes = 0.65f; b.Vignette = 0.55f;
            b.Species = new() { ("bass", 3), ("perch", 4), ("trout", 2) };
        }),
        W("sunset-lake", "Sunset Lake", "Freshwater", b =>
        {
            b.Depth = 8f; b.WaterShallow = "#6E8E9E"; b.WaterDeep = "#2A3444"; b.FloorColor = "#B8A488";
            b.LightColor = "#F2B87E"; b.LightIntensity = 1.2f; b.LightYaw = 70f; b.LightPitch = 24f;
            b.FogDensity = 0.055f; b.GodRays = 0.85f; b.Caustics = 0.6f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.35f; b.Motes = 0.5f; b.Bloom = 0.5f;
            b.Species = new() { ("koi", 6), ("goldfish", 5) };
        }),
        W("moonlit-pond", "Moonlit Pond", "Freshwater", b =>
        {
            b.Depth = 7f; b.WaterShallow = "#2E5E7E"; b.WaterDeep = "#0A1A2E"; b.FloorColor = "#5E6478";
            b.LightColor = "#CFE4F2"; b.LightIntensity = 0.75f; b.LightYaw = 15f; b.LightPitch = 66f;
            b.FogDensity = 0.06f; b.GodRays = 0.75f; b.Caustics = 0.8f; b.CausticsScale = 0.12f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.28f; b.VegColorA = "#2E4A3E"; b.VegColorB = "#4E6E5C";
            b.Motes = 0.4f; b.Bubbles = 0.15f; b.Shimmer = 0.65f; b.Bloom = 0.4f; b.Vignette = 0.55f;
            b.Species = new() { ("koi", 8), ("goldfish", 5), ("common-carp", 2) };
        }),
        W("lotus-garden", "Lotus Garden", "Freshwater", b =>
        {
            b.Depth = 5f; b.WaterShallow = "#3E9E8E"; b.WaterDeep = "#0E4438"; b.FloorColor = "#D8CDA8";
            b.LightColor = "#F2DFD0"; b.LightIntensity = 1.05f; b.GodRays = 0.6f; b.Caustics = 0.8f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.4f; b.VegColorA = "#4E7E5A"; b.VegColorB = "#8AAE76";
            b.Motes = 0.45f; b.Shimmer = 0.6f;
            b.Species = new() { ("goldfish", 6), ("guppy", 6) };
        }),
        W("ancient-temple-pond", "Ancient Temple Pond", "Freshwater", b =>
        {
            b.Depth = 9f; b.WaterShallow = "#3E8E7A"; b.WaterDeep = "#0E3630"; b.FloorColor = "#A89878";
            b.LightColor = "#E8D9A8"; b.LightIntensity = 0.95f; b.LightYaw = 55f; b.LightPitch = 56f;
            b.FogDensity = 0.065f; b.GodRays = 0.7f; b.Caustics = 0.7f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.3f; b.VegColorA = "#3E5E42"; b.VegColorB = "#6E8A5E";
            b.Motes = 0.5f; b.Vignette = 0.5f;
            b.Species = new() { ("koi", 7), ("common-carp", 3), ("catfish", 1) };
        }),
        W("hidden-jungle-pond", "Hidden Jungle Pond", "Freshwater", b =>
        {
            b.Depth = 6f; b.WaterShallow = "#3E7E4E"; b.WaterDeep = "#0E2E1C"; b.FloorColor = "#6E6A48";
            b.LightColor = "#D8E8B0"; b.LightIntensity = 0.9f; b.LightYaw = 10f; b.LightPitch = 70f;
            b.FogDensity = 0.07f; b.GodRays = 0.55f; b.Caustics = 0.45f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.75f; b.VegColorA = "#3E5E34"; b.VegColorB = "#6E8E44";
            b.VegHeightLo = 0.8f; b.VegHeightHi = 2.6f; b.Motes = 0.7f;
            b.Species = new() { ("angelfish", 3), ("neon-tetra", 12), ("betta", 1), ("crab", 2) };
        }),
        W("deep-freshwater-lake", "Deep Freshwater Lake", "Freshwater", b =>
        {
            b.Depth = 20f; b.WaterShallow = "#3E6E8E"; b.WaterDeep = "#081E30"; b.FloorColor = "#5E6E78";
            b.LightColor = "#BFE0E8"; b.LightIntensity = 0.85f; b.LightPitch = 60f;
            b.FogDensity = 0.045f; b.GodRays = 0.6f; b.Caustics = 0.5f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.2f; b.VegHeightLo = 0.6f; b.VegHeightHi = 1.6f;
            b.Motes = 0.35f; b.Vignette = 0.55f;
            b.Species = new() { ("pike", 2), ("bass", 3), ("perch", 5) };
        }),

        // ───────────────────────────── Ocean ─────────────────────────────
        W("tropical-lagoon", "Tropical Lagoon", "Ocean", b =>
        {
            b.Depth = 8f; b.WaterShallow = "#3EC8C8"; b.WaterDeep = "#0A5E68"; b.FloorColor = "#F0E8C8";
            b.LightColor = "#F2F4E0"; b.LightIntensity = 1.2f; b.LightPitch = 64f;
            b.FogDensity = 0.038f; b.GodRays = 0.7f; b.Caustics = 1.0f; b.CausticsScale = 0.2f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.35f; b.VegColorA = "#3E7E6E"; b.VegColorB = "#6EAE8E";
            b.Motes = 0.4f; b.Bubbles = 0.25f; b.Shimmer = 0.75f;
            b.Species = new() { ("damselfish", 8), ("butterflyfish", 3), ("wrasse", 2) };
        }),
        W("coral-reef", "Coral Reef", "Ocean", b =>
        {
            b.Depth = 9f; b.WaterShallow = "#2E9EC8"; b.WaterDeep = "#064258"; b.FloorColor = "#C8B898";
            b.LightColor = "#F0F0DC"; b.LightIntensity = 1.15f; b.LightPitch = 62f;
            b.FogDensity = 0.044f; b.GodRays = 0.6f; b.Caustics = 0.85f; b.CausticsScale = 0.17f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.7f; b.VegColorA = "#C86A5E"; b.VegColorB = "#E8A85E";
            b.VegHeightLo = 0.3f; b.VegHeightHi = 1.1f;
            b.Motes = 0.5f; b.Bubbles = 0.4f; b.Shimmer = 0.7f;
            b.Species = new() { ("clownfish", 5), ("blue-tang", 4), ("damselfish", 3), ("butterflyfish", 2), ("pufferfish", 1) };
        }),
        W("open-tropical-ocean", "Open Tropical Ocean", "Ocean", b =>
        {
            b.Depth = 22f; b.WaterShallow = "#1E7EC0"; b.WaterDeep = "#04264A"; b.FloorColor = "#3E6E8E";
            b.LightColor = "#E8F2F8"; b.LightIntensity = 1.1f; b.LightPitch = 66f;
            b.FogDensity = 0.032f; b.GodRays = 0.65f; b.Caustics = 0.45f;
            b.Motes = 0.3f; b.Bubbles = 0.2f; b.Vignette = 0.5f;
            b.Species = new() { ("tuna", 4), ("mackerel", 12), ("whale-shark", 1) };
        }),
        W("seagrass-meadow", "Seagrass Meadow", "Ocean", b =>
        {
            b.Depth = 7f; b.WaterShallow = "#3EAE9E"; b.WaterDeep = "#0A4E48"; b.FloorColor = "#B8B888";
            b.LightColor = "#F0F2DC"; b.LightIntensity = 1.15f; b.LightPitch = 63f;
            b.FogDensity = 0.042f; b.GodRays = 0.6f; b.Caustics = 0.8f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.95f; b.VegColorA = "#3E7E52"; b.VegColorB = "#6EAE6E";
            b.VegHeightLo = 0.5f; b.VegHeightHi = 1.5f; b.Motes = 0.45f;
            b.Species = new() { ("damselfish", 6), ("goby", 3), ("stingray", 1), ("seahorse", 3) };
        }),
        W("caribbean-reef", "Caribbean Reef", "Ocean", b =>
        {
            b.Depth = 10f; b.WaterShallow = "#2EAEC8"; b.WaterDeep = "#064E64"; b.FloorColor = "#E0D8B4";
            b.LightColor = "#F2F2DC"; b.LightIntensity = 1.18f; b.LightPitch = 64f;
            b.FogDensity = 0.04f; b.GodRays = 0.62f; b.Caustics = 0.9f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.6f; b.VegColorA = "#C8A85E"; b.VegColorB = "#E8C88A";
            b.VegHeightLo = 0.25f; b.VegHeightHi = 0.9f;
            b.Motes = 0.45f; b.Bubbles = 0.3f;
            b.Species = new() { ("surgeonfish", 4), ("parrotfish", 2), ("damselfish", 5), ("cardinalfish", 4), ("reef-shark", 1) };
        }),
        W("pacific-reef", "Pacific Reef", "Ocean", b =>
        {
            b.Depth = 11f; b.WaterShallow = "#2E9EBE"; b.WaterDeep = "#053E58"; b.FloorColor = "#C8B090";
            b.LightColor = "#EEEEDA"; b.LightIntensity = 1.1f; b.LightPitch = 60f;
            b.FogDensity = 0.045f; b.GodRays = 0.55f; b.Caustics = 0.85f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.65f; b.VegColorA = "#B85E7E"; b.VegColorB = "#E8B05E";
            b.VegHeightLo = 0.3f; b.VegHeightHi = 1.2f;
            b.Motes = 0.5f; b.Bubbles = 0.35f;
            b.Species = new() { ("yellow-tang", 4), ("clownfish", 4), ("mandarin-fish", 2), ("boxfish", 1) };
        }),
        W("deep-blue-ocean", "Deep Blue Ocean", "Ocean", b =>
        {
            b.Depth = 30f; b.WaterShallow = "#1E5EC0"; b.WaterDeep = "#021634"; b.FloorColor = "#2A4E68";
            b.LightColor = "#DCEAF4"; b.LightIntensity = 1.0f; b.LightPitch = 68f;
            b.FogDensity = 0.035f; b.GodRays = 0.55f; b.Caustics = 0.3f;
            b.Motes = 0.25f; b.Bubbles = 0.12f; b.Vignette = 0.55f;
            b.Species = new() { ("tuna", 3), ("barracuda", 5) };
        }),
        W("ocean-drop-off", "Ocean Drop-Off", "Ocean", b =>
        {
            b.Depth = 34f; b.WaterShallow = "#1E7EC0"; b.WaterDeep = "#02102A"; b.FloorColor = "#24445E";
            b.LightColor = "#D8EAF4"; b.LightIntensity = 1.05f; b.LightPitch = 70f;
            b.FogDensity = 0.05f; b.GodRays = 0.7f; b.Caustics = 0.25f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.25f; b.VegColorA = "#8A6E5E"; b.VegColorB = "#B8987E";
            b.VegHeightLo = 0.4f; b.VegHeightHi = 1.4f;
            b.Motes = 0.3f; b.Vignette = 0.6f;
            b.Species = new() { ("eagle-ray", 1), ("tuna", 3), ("barracuda", 4), ("hammerhead", 1) };
        }),
        W("kelp-forest", "Kelp Forest", "Ocean", b =>
        {
            b.Depth = 14f; b.WaterShallow = "#3E9E8E"; b.WaterDeep = "#0A3830"; b.FloorColor = "#8A7E5E";
            b.LightColor = "#E8F0C8"; b.LightIntensity = 1.15f; b.LightYaw = 25f; b.LightPitch = 60f;
            b.FogDensity = 0.055f; b.GodRays = 0.85f; b.Caustics = 0.5f;
            b.Vegetation = VegType.Kelp; b.VegDensity = 0.95f; b.VegColorA = "#3E5E34"; b.VegColorB = "#7E9E44";
            b.VegHeightLo = 4f; b.VegHeightHi = 9f; b.Motes = 0.55f; b.Bubbles = 0.3f;
            b.Species = new() { ("mackerel", 12), ("wrasse", 3), ("eagle-ray", 1) };
        }),
        W("underwater-cave", "Underwater Cave", "Ocean", b =>
        {
            b.Depth = 12f; b.WaterShallow = "#1E5E6E"; b.WaterDeep = "#041A24"; b.FloorColor = "#4A4E52";
            b.LightColor = "#8EC4D0"; b.LightIntensity = 0.6f; b.LightYaw = 75f; b.LightPitch = 22f;
            b.FogDensity = 0.09f; b.GodRays = 0.8f; b.Caustics = 0.35f;
            b.Vegetation = VegType.None; b.Motes = 0.6f; b.Bubbles = 0.5f; b.Vignette = 0.65f;
            b.Species = new() { ("anglerfish", 2), ("goby", 3), ("octopus", 1) };
        }),
        W("sunken-reef", "Sunken Reef", "Ocean", b =>
        {
            b.Depth = 13f; b.WaterShallow = "#2E8E9E"; b.WaterDeep = "#083848"; b.FloorColor = "#8A8878";
            b.LightColor = "#D8E8DC"; b.LightIntensity = 1.0f; b.LightPitch = 58f;
            b.FogDensity = 0.052f; b.GodRays = 0.6f; b.Caustics = 0.6f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.5f; b.VegColorA = "#6E7E5E"; b.VegColorB = "#9EA87E";
            b.VegHeightLo = 0.4f; b.VegHeightHi = 1.3f; b.Motes = 0.5f;
            b.Species = new() { ("damselfish", 6), ("butterflyfish", 3), ("triggerfish", 2), ("lionfish", 1) };
        }),
        W("midnight-ocean", "Midnight Ocean", "Ocean", b =>
        {
            b.Depth = 40f; b.WaterShallow = "#0E3E7E"; b.WaterDeep = "#010A1C"; b.FloorColor = "#16304A";
            b.LightColor = "#A8C8E4"; b.LightIntensity = 0.55f; b.LightYaw = 8f; b.LightPitch = 72f;
            b.FogDensity = 0.04f; b.GodRays = 0.85f; b.Caustics = 0.35f;
            b.Motes = 0.3f; b.Shimmer = 0.5f; b.Bloom = 0.45f; b.Vignette = 0.6f;
            b.Species = new() { ("lanternfish", 8), ("viperfish", 1) };
        }),

        // ───────────────────────────── Exotic ─────────────────────────────
        W("bioluminescent-ocean", "Bioluminescent Ocean", "Exotic", b =>
        {
            b.Depth = 24f; b.WaterShallow = "#0E4E7E"; b.WaterDeep = "#020E22"; b.FloorColor = "#123044";
            b.LightColor = "#7EC8E4"; b.LightIntensity = 0.45f; b.LightPitch = 70f;
            b.FogDensity = 0.042f; b.GodRays = 0.7f; b.Caustics = 0.3f;
            b.Motes = 0.9f; b.Shimmer = 0.6f; b.Bloom = 0.6f; b.Vignette = 0.6f; b.MusicResponse = 1.2f;
            b.Species = new() { ("lanternfish", 5), ("bioluminescent-fish", 3), ("jellyfish", 2), ("deep-sea-squid", 1) };
        }),
        W("midnight-abyss", "Midnight Abyss", "Exotic", b =>
        {
            b.Depth = 44f; b.WaterShallow = "#0A2A5E"; b.WaterDeep = "#010614"; b.FloorColor = "#0E1E30";
            b.LightColor = "#6E9EC8"; b.LightIntensity = 0.35f; b.LightPitch = 74f;
            b.FogDensity = 0.05f; b.GodRays = 0.75f; b.Caustics = 0.15f;
            b.Motes = 0.35f; b.Bloom = 0.55f; b.Vignette = 0.7f; b.MusicResponse = 0.9f;
            b.Species = new() { ("anglerfish", 2), ("viperfish", 2), ("gulper-eel", 1) };
        }),
        W("arctic-underwater", "Arctic Underwater", "Exotic", b =>
        {
            b.Depth = 18f; b.WaterShallow = "#8EC8E0"; b.WaterDeep = "#123E5E"; b.FloorColor = "#DCE4EA";
            b.LightColor = "#F2F8FC"; b.LightIntensity = 1.05f; b.LightPitch = 66f;
            b.FogDensity = 0.038f; b.GodRays = 0.6f; b.Caustics = 0.7f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.15f; b.VegColorA = "#4E6E5E"; b.VegColorB = "#7E9E8A";
            b.Motes = 0.5f; b.Bubbles = 0.15f; b.Shimmer = 0.55f;
            b.Species = new() { ("trout", 4), ("minnow", 8) };
        }),
        W("ice-cave", "Ice Cave", "Exotic", b =>
        {
            b.Depth = 12f; b.WaterShallow = "#6E9EC8"; b.WaterDeep = "#0E3450"; b.FloorColor = "#C8D8E4";
            b.LightColor = "#D8F0F8"; b.LightIntensity = 0.9f; b.LightYaw = 55f; b.LightPitch = 34f;
            b.FogDensity = 0.055f; b.GodRays = 0.85f; b.Caustics = 0.8f; b.CausticsScale = 0.22f;
            b.Vegetation = VegType.None; b.Motes = 0.6f; b.Bloom = 0.5f; b.Vignette = 0.5f;
            b.Species = new() { ("trout", 2), ("minnow", 10) };
        }),
        W("volcanic-underwater", "Volcanic Underwater", "Exotic", b =>
        {
            b.Depth = 15f; b.WaterShallow = "#4E3E4E"; b.WaterDeep = "#140A14"; b.FloorColor = "#3A2A2A";
            b.LightColor = "#E8845C"; b.LightIntensity = 0.75f; b.LightYaw = 60f; b.LightPitch = 30f;
            b.FogDensity = 0.075f; b.GodRays = 0.4f; b.Caustics = 0.35f;
            b.Vegetation = VegType.None; b.Motes = 0.7f; b.Bubbles = 1.2f; b.Bloom = 0.6f; b.Vignette = 0.65f; b.MusicResponse = 1.1f;
            b.Species = new() { ("viperfish", 1), ("anglerfish", 1), ("gulper-eel", 1) };
        }),
        W("deep-sea-trench", "Deep Sea Trench", "Exotic", b =>
        {
            b.Depth = 48f; b.WaterShallow = "#0A2A54"; b.WaterDeep = "#01040E"; b.FloorColor = "#0A1622";
            b.LightColor = "#5E88B0"; b.LightIntensity = 0.3f; b.LightPitch = 76f;
            b.FogDensity = 0.055f; b.GodRays = 0.7f; b.Caustics = 0.1f;
            b.Motes = 0.3f; b.Bloom = 0.5f; b.Vignette = 0.72f; b.MusicResponse = 0.85f;
            b.Species = new() { ("anglerfish", 2), ("gulper-eel", 2), ("oarfish", 1) };
        }),
        W("neon-reef", "Neon Reef", "Exotic", b =>
        {
            b.Depth = 10f; b.WaterShallow = "#2E5E9E"; b.WaterDeep = "#0A1A44"; b.FloorColor = "#2A3E5E";
            b.LightColor = "#8E9EF2"; b.LightIntensity = 0.9f; b.LightPitch = 58f;
            b.FogDensity = 0.048f; b.GodRays = 0.6f; b.Caustics = 0.7f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.6f; b.VegColorA = "#C84E9E"; b.VegColorB = "#4EE8C8";
            b.VegHeightLo = 0.35f; b.VegHeightHi = 1.2f;
            b.Motes = 0.7f; b.Bubbles = 0.3f; b.Bloom = 0.6f; b.MusicResponse = 1.25f;
            b.Species = new() { ("anthias", 8), ("cardinalfish", 4), ("mandarin-fish", 2) };
        }),
        W("fantasy-crystal-lake", "Fantasy Crystal Lake", "Exotic", b =>
        {
            b.Depth = 11f; b.WaterShallow = "#5E8EC8"; b.WaterDeep = "#142E58"; b.FloorColor = "#A8B8D8";
            b.LightColor = "#C8D4F8"; b.LightIntensity = 1.0f; b.LightYaw = 45f; b.LightPitch = 50f;
            b.FogDensity = 0.045f; b.GodRays = 0.75f; b.Caustics = 0.9f; b.CausticsScale = 0.22f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.3f; b.VegColorA = "#4E6E9E"; b.VegColorB = "#8EB8D8";
            b.Motes = 0.6f; b.Bubbles = 0.25f; b.Bloom = 0.55f; b.MusicResponse = 1.2f;
            b.Species = new() { ("betta", 3), ("guppy", 10), ("rainbowfish", 6) };
        }),
        W("ancient-underwater-ruins", "Ancient Underwater Ruins", "Exotic", b =>
        {
            b.Depth = 14f; b.WaterShallow = "#3E8E9E"; b.WaterDeep = "#0E3A48"; b.FloorColor = "#9E9884";
            b.LightColor = "#D8E4D8"; b.LightIntensity = 0.95f; b.LightYaw = 50f; b.LightPitch = 55f;
            b.FogDensity = 0.055f; b.GodRays = 0.7f; b.Caustics = 0.65f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.45f; b.VegColorA = "#3E5E44"; b.VegColorB = "#6E8E5E";
            b.Motes = 0.55f; b.Vignette = 0.55f;
            b.Species = new() { ("damselfish", 6), ("butterflyfish", 2), ("cuttlefish", 1) };
        }),
        W("atlantis", "Atlantis", "Exotic", b =>
        {
            b.Depth = 13f; b.WaterShallow = "#3E9EBE"; b.WaterDeep = "#0A4058"; b.FloorColor = "#B8A878";
            b.LightColor = "#F2E2B8"; b.LightIntensity = 1.15f; b.LightYaw = 35f; b.LightPitch = 52f;
            b.FogDensity = 0.048f; b.GodRays = 0.8f; b.Caustics = 0.9f;
            b.Vegetation = VegType.Reef; b.VegDensity = 0.5f; b.VegColorA = "#8E7E5E"; b.VegColorB = "#E8C88A";
            b.VegHeightLo = 0.5f; b.VegHeightHi = 1.6f; b.Motes = 0.55f; b.Bubbles = 0.45f; b.Bloom = 0.5f; b.MusicResponse = 1.15f;
            b.Species = new() { ("parrotfish", 3), ("surgeonfish", 4), ("sea-turtle", 1) };
        }),
        W("dreamlike-ocean", "Dreamlike Ocean", "Exotic", b =>
        {
            b.Depth = 16f; b.WaterShallow = "#8E9ED8"; b.WaterDeep = "#2E3A6E"; b.FloorColor = "#B8B0D8";
            b.LightColor = "#F2D8E8"; b.LightIntensity = 1.0f; b.LightYaw = 20f; b.LightPitch = 48f;
            b.FogDensity = 0.05f; b.GodRays = 0.75f; b.Caustics = 0.5f;
            b.Vegetation = VegType.Grass; b.VegDensity = 0.3f; b.VegColorA = "#8E6E9E"; b.VegColorB = "#C8A8D8";
            b.Motes = 0.7f; b.Shimmer = 0.7f; b.Bloom = 0.6f; b.Vignette = 0.5f; b.MusicResponse = 1.25f;
            b.Species = new() { ("jellyfish", 3), ("manta-ray", 1), ("bioluminescent-fish", 2) };
        }),
    };
}
