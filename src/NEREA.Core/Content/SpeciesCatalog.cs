using NEREA.Core.Models;

namespace NEREA.Core.Content;

/// <summary>
/// The NEREA species library. Every entry is pure data consumed by both the
/// simulation (behavior) and the renderer (visuals), so new species can be
/// added without touching engine code.
/// </summary>
public static class SpeciesCatalog
{
    private sealed class B
    {
        public string Id = "", Name = "", Category = "Freshwater";
        public SpeciesForm Form = SpeciesForm.Fish;
        public BodyPattern Pattern = BodyPattern.None;
        public float SizeM = 0.18f, Speed = 0.55f, TurnRate = 1.6f;
        public float School = 0f, Separation = 0.55f, Fear = 0.5f, Curiosity = 0.15f, Restfulness = 0.2f;
        public float DepthLo = 0.15f, DepthHi = 0.75f;
        public string ColorDeep = "#7A97A0", ColorBright = "#E8F1EE", ColorFin = "#B7C9C2";
        public int StripeCount = 0;
        public float Glow = 0f;
        public float BodyHeight = 1f, BodyWidth = 1f, HeadBlunt = 0.5f, TailFork = 0.5f;
        public float DorsalHeight = 0.16f, TailLength = 0.28f, PectoralSize = 0.5f;

        public SpeciesDefinition To() => new()
        {
            Id = Id, Name = Name, Category = Category, Form = Form, Pattern = Pattern,
            SizeM = SizeM, Speed = Speed, TurnRate = TurnRate,
            School = School, Separation = Separation, Fear = Fear, Curiosity = Curiosity, Restfulness = Restfulness,
            DepthLo = DepthLo, DepthHi = DepthHi,
            ColorDeep = ColorDeep, ColorBright = ColorBright, ColorFin = ColorFin,
            StripeCount = StripeCount, Glow = Glow,
            BodyHeight = BodyHeight, BodyWidth = BodyWidth, HeadBlunt = HeadBlunt, TailFork = TailFork,
            DorsalHeight = DorsalHeight, TailLength = TailLength, PectoralSize = PectoralSize
        };
    }

    private static SpeciesDefinition S(string id, string name, string category, Action<B> cfg)
    {
        var b = new B { Id = id, Name = name, Category = category };
        cfg(b);
        return b.To();
    }

    public static IReadOnlyList<SpeciesDefinition> All { get; } = Build().AsReadOnly();

    public static SpeciesDefinition? ById(string id) => _byId.TryGetValue(id, out var s) ? s : null;

    private static readonly Dictionary<string, SpeciesDefinition> _byId =
        Build().ToDictionary(s => s.Id, StringComparer.Ordinal);

    private static List<SpeciesDefinition> Build() => new()
    {
        // ───────────────────────────── Freshwater ─────────────────────────────
        S("koi", "Koi", "Freshwater", s =>
        {
            s.SizeM = 0.42f; s.Speed = 0.32f; s.TurnRate = 1.0f;
            s.Curiosity = 0.55f; s.Restfulness = 0.5f; s.Fear = 0.3f; s.DepthLo = 0.1f; s.DepthHi = 0.6f;
            s.ColorDeep = "#D8622C"; s.ColorBright = "#F7F1E4"; s.ColorFin = "#EFD9C3";
            s.Pattern = BodyPattern.Koi; s.BodyHeight = 1.05f; s.HeadBlunt = 0.7f; s.TailFork = 0.25f; s.TailLength = 0.3f;
        }),
        S("goldfish", "Goldfish", "Freshwater", s =>
        {
            s.SizeM = 0.16f; s.Speed = 0.3f; s.TurnRate = 1.4f;
            s.Curiosity = 0.45f; s.Restfulness = 0.45f; s.Fear = 0.35f; s.School = 0.25f; s.DepthLo = 0.1f; s.DepthHi = 0.55f;
            s.ColorDeep = "#E8703A"; s.ColorBright = "#F7B24C"; s.ColorFin = "#F2A45E";
            s.Pattern = BodyPattern.TwoTone; s.BodyHeight = 1.15f; s.BodyWidth = 1.1f; s.HeadBlunt = 0.75f; s.TailFork = 0.2f; s.TailLength = 0.34f; s.DorsalHeight = 0.2f;
        }),
        S("common-carp", "Common Carp", "Freshwater", s =>
        {
            s.SizeM = 0.55f; s.Speed = 0.34f; s.TurnRate = 0.9f;
            s.Curiosity = 0.4f; s.Restfulness = 0.5f; s.Fear = 0.4f; s.DepthLo = 0.25f; s.DepthHi = 0.85f;
            s.ColorDeep = "#8A7A52"; s.ColorBright = "#D8CBA0"; s.ColorFin = "#9C8A60";
            s.BodyHeight = 1.05f; s.HeadBlunt = 0.6f;
        }),
        S("grass-carp", "Grass Carp", "Freshwater", s =>
        {
            s.SizeM = 0.6f; s.Speed = 0.4f; s.TurnRate = 0.9f;
            s.DepthLo = 0.2f; s.DepthHi = 0.8f; s.Restfulness = 0.35f;
            s.ColorDeep = "#9AA98A"; s.ColorBright = "#DFE6D2"; s.ColorFin = "#A8B696";
        }),
        S("guppy", "Guppy", "Freshwater", s =>
        {
            s.SizeM = 0.05f; s.Speed = 0.45f; s.TurnRate = 2.4f;
            s.School = 0.6f; s.Fear = 0.8f; s.Curiosity = 0.3f; s.DepthLo = 0.08f; s.DepthHi = 0.5f;
            s.ColorDeep = "#5C7FA3"; s.ColorBright = "#E8B05C"; s.ColorFin = "#E8663D";
            s.TailLength = 0.42f; s.TailFork = 0.15f; s.DorsalHeight = 0.22f; s.BodyHeight = 0.95f;
        }),
        S("molly", "Molly", "Freshwater", s =>
        {
            s.SizeM = 0.07f; s.Speed = 0.4f; s.TurnRate = 2.0f;
            s.School = 0.5f; s.Fear = 0.65f; s.DepthLo = 0.08f; s.DepthHi = 0.55f;
            s.ColorDeep = "#9AA5A8"; s.ColorBright = "#D9E2E2"; s.ColorFin = "#8A98A0";
            s.TailLength = 0.3f;
        }),
        S("platy", "Platy", "Freshwater", s =>
        {
            s.SizeM = 0.06f; s.Speed = 0.42f; s.TurnRate = 2.1f;
            s.School = 0.55f; s.Fear = 0.7f; s.DepthLo = 0.1f; s.DepthHi = 0.5f;
            s.ColorDeep = "#E0703C"; s.ColorBright = "#F4B364"; s.ColorFin = "#E8905A";
            s.BodyHeight = 1.1f;
        }),
        S("betta", "Betta", "Freshwater", s =>
        {
            s.SizeM = 0.08f; s.Speed = 0.3f; s.TurnRate = 1.8f;
            s.School = 0f; s.Fear = 0.45f; s.Restfulness = 0.6f; s.Curiosity = 0.4f; s.DepthLo = 0.05f; s.DepthHi = 0.45f;
            s.ColorDeep = "#7B2D8E"; s.ColorBright = "#C93A66"; s.ColorFin = "#C93A66";
            s.TailLength = 0.55f; s.TailFork = 0.1f; s.DorsalHeight = 0.4f; s.PectoralSize = 0.7f; s.BodyHeight = 1.0f;
        }),
        S("angelfish", "Angelfish", "Freshwater", s =>
        {
            s.SizeM = 0.12f; s.Speed = 0.28f; s.TurnRate = 1.4f;
            s.School = 0.3f; s.Fear = 0.5f; s.Restfulness = 0.5f; s.DepthLo = 0.15f; s.DepthHi = 0.65f;
            s.ColorDeep = "#C9D6D8"; s.ColorBright = "#E8F0EE"; s.ColorFin = "#B4C6C8";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 3; s.BodyHeight = 1.6f; s.BodyWidth = 0.5f;
            s.DorsalHeight = 0.55f; s.TailLength = 0.3f; s.TailFork = 0.2f; s.HeadBlunt = 0.6f;
        }),
        S("discus", "Discus", "Freshwater", s =>
        {
            s.SizeM = 0.18f; s.Speed = 0.24f; s.TurnRate = 1.2f;
            s.School = 0.3f; s.Restfulness = 0.6f; s.Fear = 0.55f; s.DepthLo = 0.15f; s.DepthHi = 0.6f;
            s.ColorDeep = "#D8742F"; s.ColorBright = "#8FC8B4"; s.ColorFin = "#B45E3C";
            s.Pattern = BodyPattern.Stripes; s.StripeCount = 6; s.BodyHeight = 1.65f; s.BodyWidth = 0.45f; s.HeadBlunt = 0.55f;
        }),
        S("neon-tetra", "Neon Tetra", "Freshwater", s =>
        {
            s.SizeM = 0.045f; s.Speed = 0.5f; s.TurnRate = 2.6f;
            s.School = 0.85f; s.Separation = 0.45f; s.Fear = 0.85f; s.DepthLo = 0.12f; s.DepthHi = 0.55f;
            s.ColorDeep = "#3A4258"; s.ColorBright = "#56E5EC"; s.ColorFin = "#D8463A";
            s.Pattern = BodyPattern.Stripes; s.StripeCount = 1; s.BodyHeight = 0.85f;
        }),
        S("cardinal-tetra", "Cardinal Tetra", "Freshwater", s =>
        {
            s.SizeM = 0.045f; s.Speed = 0.5f; s.TurnRate = 2.6f;
            s.School = 0.85f; s.Fear = 0.85f; s.DepthLo = 0.12f; s.DepthHi = 0.6f;
            s.ColorDeep = "#323A50"; s.ColorBright = "#58E8E4"; s.ColorFin = "#C43A44";
            s.Pattern = BodyPattern.Stripes; s.StripeCount = 1; s.BodyHeight = 0.85f;
        }),
        S("zebra-danio", "Zebra Danio", "Freshwater", s =>
        {
            s.SizeM = 0.06f; s.Speed = 0.6f; s.TurnRate = 2.4f;
            s.School = 0.75f; s.Fear = 0.7f; s.DepthLo = 0.05f; s.DepthHi = 0.45f;
            s.ColorDeep = "#C9CFC4"; s.ColorBright = "#E9EEE8"; s.ColorFin = "#8FA0A4";
            s.Pattern = BodyPattern.Stripes; s.StripeCount = 5; s.BodyHeight = 0.9f;
        }),
        S("cherry-barb", "Cherry Barb", "Freshwater", s =>
        {
            s.SizeM = 0.06f; s.Speed = 0.45f; s.TurnRate = 2.2f;
            s.School = 0.6f; s.Fear = 0.75f; s.DepthLo = 0.12f; s.DepthHi = 0.5f;
            s.ColorDeep = "#C94B3C"; s.ColorBright = "#E88A6A"; s.ColorFin = "#D86A52";
            s.StripeCount = 1; s.Pattern = BodyPattern.Stripes;
        }),
        S("tiger-barb", "Tiger Barb", "Freshwater", s =>
        {
            s.SizeM = 0.07f; s.Speed = 0.55f; s.TurnRate = 2.0f;
            s.School = 0.65f; s.Fear = 0.6f; s.DepthLo = 0.15f; s.DepthHi = 0.6f;
            s.ColorDeep = "#E0B23C"; s.ColorBright = "#F4D98A"; s.ColorFin = "#E88A3C";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 4; s.BodyHeight = 1.1f;
        }),
        S("gourami", "Gourami", "Freshwater", s =>
        {
            s.SizeM = 0.14f; s.Speed = 0.26f; s.TurnRate = 1.4f;
            s.Restfulness = 0.6f; s.Curiosity = 0.3f; s.DepthLo = 0.05f; s.DepthHi = 0.45f;
            s.ColorDeep = "#A8B8C8"; s.ColorBright = "#D8E2E8"; s.ColorFin = "#9AACBE";
            s.BodyHeight = 1.2f; s.BodyWidth = 0.85f; s.HeadBlunt = 0.65f; s.DorsalHeight = 0.26f; s.TailLength = 0.24f;
        }),
        S("rainbowfish", "Rainbowfish", "Freshwater", s =>
        {
            s.SizeM = 0.1f; s.Speed = 0.5f; s.TurnRate = 2.0f;
            s.School = 0.6f; s.Fear = 0.6f; s.DepthLo = 0.08f; s.DepthHi = 0.5f;
            s.ColorDeep = "#4C8FBE"; s.ColorBright = "#7ED0C8"; s.ColorFin = "#E8C05A";
            s.BodyHeight = 0.95f; s.TailFork = 0.7f;
        }),
        S("oscar", "Oscar", "Freshwater", s =>
        {
            s.SizeM = 0.3f; s.Speed = 0.3f; s.TurnRate = 1.0f;
            s.Curiosity = 0.5f; s.Restfulness = 0.45f; s.Fear = 0.2f; s.DepthLo = 0.15f; s.DepthHi = 0.7f;
            s.ColorDeep = "#4A4436"; s.ColorBright = "#D8944C"; s.ColorFin = "#5A5442";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 1.25f; s.HeadBlunt = 0.7f;
        }),
        S("pleco", "Pleco", "Freshwater", s =>
        {
            s.SizeM = 0.26f; s.Speed = 0.14f; s.TurnRate = 0.8f;
            s.Restfulness = 0.85f; s.Fear = 0.5f; s.DepthLo = 0.8f; s.DepthHi = 0.97f;
            s.ColorDeep = "#5A5248"; s.ColorBright = "#8A7E6C"; s.ColorFin = "#4A4438";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.85f; s.BodyWidth = 0.9f; s.TailLength = 0.22f;
        }),
        S("catfish", "Catfish", "Freshwater", s =>
        {
            s.SizeM = 0.34f; s.Speed = 0.2f; s.TurnRate = 0.8f;
            s.Restfulness = 0.75f; s.Curiosity = 0.3f; s.DepthLo = 0.75f; s.DepthHi = 0.97f;
            s.ColorDeep = "#6E6254"; s.ColorBright = "#C9BCA4"; s.ColorFin = "#5A5044";
            s.BodyHeight = 0.8f; s.BodyWidth = 0.85f; s.TailLength = 0.3f; s.PectoralSize = 0.7f;
        }),
        S("loach", "Loach", "Freshwater", s =>
        {
            s.SizeM = 0.13f; s.Speed = 0.3f; s.TurnRate = 1.6f;
            s.Restfulness = 0.5f; s.DepthLo = 0.7f; s.DepthHi = 0.95f; s.Fear = 0.6f;
            s.ColorDeep = "#C9A66B"; s.ColorBright = "#E8D2A8"; s.ColorFin = "#B08E58";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 5; s.BodyHeight = 0.8f; s.BodyWidth = 0.75f;
        }),
        S("pike", "Pike", "Freshwater", s =>
        {
            s.SizeM = 0.75f; s.Speed = 0.42f; s.TurnRate = 0.9f;
            s.Restfulness = 0.7f; s.Fear = 0.15f; s.Curiosity = 0.1f; s.DepthLo = 0.3f; s.DepthHi = 0.8f;
            s.ColorDeep = "#5E7050"; s.ColorBright = "#C9CFA8"; s.ColorFin = "#6E7E5C";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.72f; s.BodyWidth = 0.7f; s.HeadBlunt = 0.85f; s.TailLength = 0.22f;
        }),
        S("trout", "Trout", "Freshwater", s =>
        {
            s.SizeM = 0.35f; s.Speed = 0.55f; s.TurnRate = 1.3f;
            s.School = 0.3f; s.DepthLo = 0.2f; s.DepthHi = 0.7f; s.Fear = 0.5f;
            s.ColorDeep = "#8C7B6B"; s.ColorBright = "#E4D9C8"; s.ColorFin = "#C96A6A";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.9f;
        }),
        S("bass", "Bass", "Freshwater", s =>
        {
            s.SizeM = 0.4f; s.Speed = 0.45f; s.TurnRate = 1.2f;
            s.Restfulness = 0.5f; s.Fear = 0.35f; s.Curiosity = 0.2f; s.DepthLo = 0.25f; s.DepthHi = 0.75f;
            s.ColorDeep = "#6E8468"; s.ColorBright = "#DFE6C8"; s.ColorFin = "#7E8E5E";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 3; s.BodyHeight = 1.1f; s.HeadBlunt = 0.65f;
        }),
        S("perch", "Perch", "Freshwater", s =>
        {
            s.SizeM = 0.22f; s.Speed = 0.4f; s.TurnRate = 1.5f;
            s.School = 0.4f; s.DepthLo = 0.2f; s.DepthHi = 0.7f;
            s.ColorDeep = "#5E7E6A"; s.ColorBright = "#D9C88A"; s.ColorFin = "#D8683C";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 5; s.DorsalHeight = 0.28f;
        }),
        S("minnow", "Minnow", "Freshwater", s =>
        {
            s.SizeM = 0.055f; s.Speed = 0.5f; s.TurnRate = 2.4f;
            s.School = 0.85f; s.Separation = 0.4f; s.Fear = 0.85f; s.DepthLo = 0.08f; s.DepthHi = 0.5f;
            s.ColorDeep = "#9AA8A0"; s.ColorBright = "#DDE5DE"; s.ColorFin = "#A8B2A8";
        }),

        // ───────────────────────────── Tropical Reef ─────────────────────────────
        S("clownfish", "Clownfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.09f; s.Speed = 0.32f; s.TurnRate = 2.0f;
            s.School = 0.15f; s.Fear = 0.45f; s.Curiosity = 0.5f; s.Restfulness = 0.4f; s.DepthLo = 0.1f; s.DepthHi = 0.5f;
            s.ColorDeep = "#E8641E"; s.ColorBright = "#F5F0E8"; s.ColorFin = "#E8842C";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 3; s.BodyHeight = 1.05f; s.HeadBlunt = 0.65f; s.TailFork = 0.3f;
        }),
        S("blue-tang", "Blue Tang", "Tropical Reef", s =>
        {
            s.SizeM = 0.2f; s.Speed = 0.4f; s.TurnRate = 1.5f;
            s.School = 0.25f; s.Fear = 0.5f; s.Curiosity = 0.35f; s.DepthLo = 0.1f; s.DepthHi = 0.55f;
            s.ColorDeep = "#2E6FD8"; s.ColorBright = "#4E9AE8"; s.ColorFin = "#F2D02E";
            s.BodyHeight = 1.2f; s.BodyWidth = 0.6f; s.HeadBlunt = 0.6f; s.DorsalHeight = 0.2f;
        }),
        S("yellow-tang", "Yellow Tang", "Tropical Reef", s =>
        {
            s.SizeM = 0.18f; s.Speed = 0.36f; s.TurnRate = 1.6f;
            s.Fear = 0.5f; s.Curiosity = 0.35f; s.DepthLo = 0.1f; s.DepthHi = 0.5f;
            s.ColorDeep = "#F2D02E"; s.ColorBright = "#F8E88A"; s.ColorFin = "#E8C02E";
            s.BodyHeight = 1.35f; s.BodyWidth = 0.5f; s.HeadBlunt = 0.55f; s.DorsalHeight = 0.3f;
        }),
        S("butterflyfish", "Butterflyfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.13f; s.Speed = 0.3f; s.TurnRate = 1.7f;
            s.Fear = 0.6f; s.Curiosity = 0.45f; s.DepthLo = 0.1f; s.DepthHi = 0.5f;
            s.ColorDeep = "#F2D44E"; s.ColorBright = "#F8F0D8"; s.ColorFin = "#E8B84E";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 4; s.BodyHeight = 1.4f; s.BodyWidth = 0.5f; s.HeadBlunt = 0.5f; s.DorsalHeight = 0.28f;
        }),
        S("moorish-idol", "Moorish Idol", "Tropical Reef", s =>
        {
            s.SizeM = 0.16f; s.Speed = 0.3f; s.TurnRate = 1.5f;
            s.School = 0.3f; s.Fear = 0.55f; s.DepthLo = 0.08f; s.DepthHi = 0.45f;
            s.ColorDeep = "#2E2A26"; s.ColorBright = "#F2F0E8"; s.ColorFin = "#3A342E";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 3; s.BodyHeight = 1.5f; s.BodyWidth = 0.45f; s.DorsalHeight = 0.6f; s.TailLength = 0.3f;
        }),
        S("damselfish", "Damselfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.07f; s.Speed = 0.45f; s.TurnRate = 2.4f;
            s.School = 0.75f; s.Fear = 0.75f; s.Curiosity = 0.3f; s.DepthLo = 0.1f; s.DepthHi = 0.55f;
            s.ColorDeep = "#3A78C8"; s.ColorBright = "#6EA8E0"; s.ColorFin = "#2E68B8";
        }),
        S("wrasse", "Wrasse", "Tropical Reef", s =>
        {
            s.SizeM = 0.18f; s.Speed = 0.5f; s.TurnRate = 1.8f;
            s.Fear = 0.6f; s.Curiosity = 0.4f; s.DepthLo = 0.1f; s.DepthHi = 0.6f;
            s.ColorDeep = "#3EA8A0"; s.ColorBright = "#8AD8C8"; s.ColorFin = "#E8845C";
            s.Pattern = BodyPattern.Stripes; s.StripeCount = 2;
        }),
        S("goby", "Goby", "Tropical Reef", s =>
        {
            s.SizeM = 0.07f; s.Speed = 0.25f; s.TurnRate = 1.8f;
            s.Restfulness = 0.75f; s.Fear = 0.7f; s.DepthLo = 0.75f; s.DepthHi = 0.97f;
            s.ColorDeep = "#C9B88A"; s.ColorBright = "#E8DCC0"; s.ColorFin = "#A89878";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.85f;
        }),
        S("blenny", "Blenny", "Tropical Reef", s =>
        {
            s.SizeM = 0.08f; s.Speed = 0.22f; s.TurnRate = 1.8f;
            s.Restfulness = 0.8f; s.Curiosity = 0.4f; s.DepthLo = 0.7f; s.DepthHi = 0.95f;
            s.ColorDeep = "#8A9498"; s.ColorBright = "#C9D2D4"; s.ColorFin = "#7A868A";
            s.BodyHeight = 0.9f; s.HeadBlunt = 0.7f;
        }),
        S("triggerfish", "Triggerfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.28f; s.Speed = 0.3f; s.TurnRate = 1.2f;
            s.Fear = 0.3f; s.Curiosity = 0.45f; s.DepthLo = 0.15f; s.DepthHi = 0.6f;
            s.ColorDeep = "#4A5E68"; s.ColorBright = "#B8C8CC"; s.ColorFin = "#3A4E58";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 1.15f; s.BodyWidth = 0.75f; s.HeadBlunt = 0.6f; s.DorsalHeight = 0.24f; s.TailLength = 0.2f;
        }),
        S("lionfish", "Lionfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.28f; s.Speed = 0.2f; s.TurnRate = 1.2f;
            s.Restfulness = 0.7f; s.Fear = 0.3f; s.DepthLo = 0.2f; s.DepthHi = 0.7f;
            s.ColorDeep = "#C8B8B0"; s.ColorBright = "#EFE4DC"; s.ColorFin = "#B04A38";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 8; s.DorsalHeight = 0.5f; s.PectoralSize = 0.9f; s.BodyWidth = 0.8f;
        }),
        S("pufferfish", "Pufferfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.22f; s.Speed = 0.18f; s.TurnRate = 1.3f;
            s.Restfulness = 0.7f; s.Curiosity = 0.5f; s.Fear = 0.4f; s.DepthLo = 0.1f; s.DepthHi = 0.55f;
            s.ColorDeep = "#D8C89A"; s.ColorBright = "#F2E8C8"; s.ColorFin = "#B8A878";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 1.35f; s.BodyWidth = 1.3f; s.HeadBlunt = 0.8f; s.TailLength = 0.18f; s.DorsalHeight = 0.14f;
        }),
        S("boxfish", "Boxfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.15f; s.Speed = 0.16f; s.TurnRate = 1.4f;
            s.Restfulness = 0.7f; s.Curiosity = 0.4f; s.DepthLo = 0.1f; s.DepthHi = 0.5f;
            s.ColorDeep = "#F2E8A0"; s.ColorBright = "#F8F0C8"; s.ColorFin = "#3A3A3A";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 1.25f; s.BodyWidth = 1.35f; s.HeadBlunt = 0.75f; s.TailLength = 0.16f;
        }),
        S("cardinalfish", "Cardinalfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.08f; s.Speed = 0.28f; s.TurnRate = 2.0f;
            s.School = 0.5f; s.Fear = 0.7f; s.DepthLo = 0.15f; s.DepthHi = 0.6f;
            s.ColorDeep = "#C8787A"; s.ColorBright = "#E8B0A8"; s.ColorFin = "#B8686A";
            s.BodyHeight = 1.1f;
        }),
        S("parrotfish", "Parrotfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.35f; s.Speed = 0.35f; s.TurnRate = 1.2f;
            s.Curiosity = 0.3f; s.DepthLo = 0.1f; s.DepthHi = 0.6f;
            s.ColorDeep = "#2EA8B8"; s.ColorBright = "#F2C84E"; s.ColorFin = "#2E8898";
            s.Pattern = BodyPattern.TwoTone; s.HeadBlunt = 0.8f; s.BodyHeight = 1.05f;
        }),
        S("surgeonfish", "Surgeonfish", "Tropical Reef", s =>
        {
            s.SizeM = 0.22f; s.Speed = 0.42f; s.TurnRate = 1.5f;
            s.School = 0.35f; s.DepthLo = 0.08f; s.DepthHi = 0.5f;
            s.ColorDeep = "#5E78C8"; s.ColorBright = "#8AA8E0"; s.ColorFin = "#F2D02E";
            s.BodyHeight = 1.2f; s.BodyWidth = 0.55f;
        }),
        S("anthias", "Anthias", "Tropical Reef", s =>
        {
            s.SizeM = 0.07f; s.Speed = 0.42f; s.TurnRate = 2.4f;
            s.School = 0.8f; s.Fear = 0.7f; s.DepthLo = 0.05f; s.DepthHi = 0.45f;
            s.ColorDeep = "#F27A9E"; s.ColorBright = "#F8B0C4"; s.ColorFin = "#E86A8E";
        }),
        S("mandarin-fish", "Mandarin Fish", "Tropical Reef", s =>
        {
            s.SizeM = 0.07f; s.Speed = 0.15f; s.TurnRate = 1.6f;
            s.Restfulness = 0.85f; s.Curiosity = 0.3f; s.DepthLo = 0.6f; s.DepthHi = 0.9f;
            s.ColorDeep = "#2E8E7E"; s.ColorBright = "#E8842C"; s.ColorFin = "#E8A02C";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 1.1f; s.BodyWidth = 0.85f; s.HeadBlunt = 0.7f;
        }),

        // ───────────────────────────── Large Ocean ─────────────────────────────
        S("tuna", "Tuna", "Large Ocean", s =>
        {
            s.SizeM = 1.3f; s.Speed = 1.5f; s.TurnRate = 1.1f;
            s.School = 0.5f; s.DepthLo = 0.15f; s.DepthHi = 0.6f; s.Fear = 0.15f;
            s.ColorDeep = "#3A5E7E"; s.ColorBright = "#DFE8EE"; s.ColorFin = "#7E98A8";
            s.BodyHeight = 0.85f; s.BodyWidth = 0.8f; s.TailFork = 0.9f; s.TailLength = 0.22f; s.HeadBlunt = 0.55f;
        }),
        S("mackerel", "Mackerel", "Large Ocean", s =>
        {
            s.SizeM = 0.4f; s.Speed = 1.2f; s.TurnRate = 1.6f;
            s.School = 0.9f; s.Separation = 0.4f; s.DepthLo = 0.1f; s.DepthHi = 0.5f;
            s.ColorDeep = "#4A7E8C"; s.ColorBright = "#DFE8EA"; s.ColorFin = "#6E98A4";
            s.Pattern = BodyPattern.Bars; s.StripeCount = 6; s.BodyHeight = 0.8f; s.TailFork = 0.85f;
        }),
        S("barracuda", "Barracuda", "Large Ocean", s =>
        {
            s.SizeM = 0.95f; s.Speed = 0.9f; s.TurnRate = 1.2f;
            s.School = 0.4f; s.DepthLo = 0.15f; s.DepthHi = 0.55f; s.Fear = 0.25f;
            s.ColorDeep = "#8A9AA4"; s.ColorBright = "#DFE8EA"; s.ColorFin = "#7E8E98";
            s.BodyHeight = 0.65f; s.BodyWidth = 0.6f; s.HeadBlunt = 0.8f; s.TailFork = 0.8f; s.TailLength = 0.2f;
        }),
        S("swordfish", "Swordfish", "Large Ocean", s =>
        {
            s.SizeM = 1.8f; s.Speed = 1.6f; s.TurnRate = 1.0f;
            s.DepthLo = 0.2f; s.DepthHi = 0.65f; s.Fear = 0.1f; s.Restfulness = 0.4f;
            s.ColorDeep = "#4A6E8E"; s.ColorBright = "#DFE8EE"; s.ColorFin = "#6E88A0";
            s.BodyHeight = 0.8f; s.HeadBlunt = 1.0f; s.TailFork = 0.85f; s.TailLength = 0.2f;
        }),
        S("manta-ray", "Manta Ray", "Large Ocean", s =>
        {
            s.Form = SpeciesForm.Ray;
            s.SizeM = 2.6f; s.Speed = 0.5f; s.TurnRate = 0.8f;
            s.DepthLo = 0.1f; s.DepthHi = 0.5f; s.Fear = 0.1f; s.Curiosity = 0.4f; s.Restfulness = 0.4f;
            s.ColorDeep = "#3A4A54"; s.ColorBright = "#F2F4F0"; s.ColorFin = "#5E6E78";
            s.BodyHeight = 0.5f; s.BodyWidth = 2.2f; s.TailLength = 0.12f; s.TailFork = 0.1f; s.DorsalHeight = 0.05f; s.HeadBlunt = 0.6f;
        }),
        S("stingray", "Stingray", "Large Ocean", s =>
        {
            s.Form = SpeciesForm.Ray;
            s.SizeM = 0.8f; s.Speed = 0.28f; s.TurnRate = 0.9f;
            s.DepthLo = 0.75f; s.DepthHi = 0.97f; s.Restfulness = 0.75f; s.Fear = 0.5f;
            s.ColorDeep = "#6E6254"; s.ColorBright = "#E8DFC8"; s.ColorFin = "#7E7264";
            s.BodyHeight = 0.4f; s.BodyWidth = 1.5f; s.TailLength = 0.3f; s.TailFork = 0.1f; s.DorsalHeight = 0.04f;
        }),
        S("eagle-ray", "Eagle Ray", "Large Ocean", s =>
        {
            s.Form = SpeciesForm.Ray;
            s.SizeM = 1.2f; s.Speed = 0.4f; s.TurnRate = 0.9f;
            s.DepthLo = 0.25f; s.DepthHi = 0.7f; s.Fear = 0.4f;
            s.ColorDeep = "#4A5E68"; s.ColorBright = "#F0E8D8"; s.ColorFin = "#5E7280";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.4f; s.BodyWidth = 1.7f; s.TailLength = 0.34f; s.TailFork = 0.1f;
        }),
        S("reef-shark", "Reef Shark", "Large Ocean", s =>
        {
            s.SizeM = 1.5f; s.Speed = 0.7f; s.TurnRate = 0.9f;
            s.DepthLo = 0.15f; s.DepthHi = 0.55f; s.Fear = 0.05f; s.Curiosity = 0.12f; s.Restfulness = 0.3f;
            s.ColorDeep = "#8A949E"; s.ColorBright = "#EFEFF2"; s.ColorFin = "#6E7A84";
            s.BodyHeight = 0.85f; s.DorsalHeight = 0.34f; s.TailFork = 0.8f; s.TailLength = 0.26f; s.HeadBlunt = 0.6f;
        }),
        S("hammerhead", "Hammerhead Shark", "Large Ocean", s =>
        {
            s.SizeM = 2.0f; s.Speed = 0.75f; s.TurnRate = 0.85f;
            s.DepthLo = 0.15f; s.DepthHi = 0.6f; s.Fear = 0.05f; s.Restfulness = 0.3f;
            s.ColorDeep = "#7E8A92"; s.ColorBright = "#EFEFF2"; s.ColorFin = "#5E6A74";
            s.BodyHeight = 0.85f; s.DorsalHeight = 0.32f; s.TailFork = 0.8f; s.HeadBlunt = 0.95f;
        }),
        S("whale-shark", "Whale Shark", "Large Ocean", s =>
        {
            s.SizeM = 4.5f; s.Speed = 0.32f; s.TurnRate = 0.5f;
            s.DepthLo = 0.1f; s.DepthHi = 0.45f; s.Fear = 0f; s.Curiosity = 0.15f; s.Restfulness = 0.35f;
            s.ColorDeep = "#5E7888"; s.ColorBright = "#C9D8DC"; s.ColorFin = "#4E6878";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.95f; s.BodyWidth = 0.95f; s.HeadBlunt = 0.7f; s.TailFork = 0.6f; s.TailLength = 0.24f;
        }),

        // ───────────────────────────── Deep Sea ─────────────────────────────
        S("anglerfish", "Anglerfish", "Deep Sea", s =>
        {
            s.SizeM = 0.28f; s.Speed = 0.12f; s.TurnRate = 1.0f;
            s.Restfulness = 0.85f; s.Fear = 0.1f; s.Curiosity = 0.3f; s.DepthLo = 0.78f; s.DepthHi = 0.97f;
            s.ColorDeep = "#4A4238"; s.ColorBright = "#6E665A"; s.ColorFin = "#3A342C";
            s.Glow = 0.85f; s.BodyHeight = 1.2f; s.BodyWidth = 1.1f; s.HeadBlunt = 0.85f; s.TailLength = 0.2f; s.DorsalHeight = 0.12f;
        }),
        S("lanternfish", "Lanternfish", "Deep Sea", s =>
        {
            s.SizeM = 0.09f; s.Speed = 0.5f; s.TurnRate = 2.2f;
            s.School = 0.8f; s.DepthLo = 0.4f; s.DepthHi = 0.8f;
            s.ColorDeep = "#2E3A4E"; s.ColorBright = "#8AC8D8"; s.ColorFin = "#3E4C60";
            s.Glow = 0.6f; s.Pattern = BodyPattern.GlowDots;
        }),
        S("viperfish", "Viperfish", "Deep Sea", s =>
        {
            s.SizeM = 0.35f; s.Speed = 0.3f; s.TurnRate = 1.2f;
            s.Restfulness = 0.7f; s.Fear = 0.15f; s.DepthLo = 0.7f; s.DepthHi = 0.95f;
            s.ColorDeep = "#3A4454"; s.ColorBright = "#8AA8B8"; s.ColorFin = "#2E3848";
            s.Glow = 0.5f; s.BodyHeight = 0.7f; s.HeadBlunt = 0.75f; s.TailLength = 0.24f;
        }),
        S("gulper-eel", "Gulper Eel", "Deep Sea", s =>
        {
            s.SizeM = 0.9f; s.Speed = 0.18f; s.TurnRate = 0.9f;
            s.Restfulness = 0.8f; s.DepthLo = 0.75f; s.DepthHi = 0.97f;
            s.ColorDeep = "#3A3644"; s.ColorBright = "#5E5868"; s.ColorFin = "#2E2C38";
            s.Glow = 0.3f; s.BodyHeight = 0.5f; s.BodyWidth = 0.4f; s.HeadBlunt = 0.9f; s.TailLength = 0.12f;
        }),
        S("oarfish", "Oarfish", "Deep Sea", s =>
        {
            s.SizeM = 3.4f; s.Speed = 0.2f; s.TurnRate = 0.6f;
            s.Restfulness = 0.6f; s.DepthLo = 0.35f; s.DepthHi = 0.85f;
            s.ColorDeep = "#B8C4D0"; s.ColorBright = "#E8EEF2"; s.ColorFin = "#C85A5A";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.5f; s.BodyWidth = 0.3f; s.DorsalHeight = 0.4f; s.TailLength = 0.06f; s.HeadBlunt = 0.5f;
        }),
        S("deep-sea-squid", "Deep Sea Squid", "Deep Sea", s =>
        {
            s.Form = SpeciesForm.Jelly;
            s.SizeM = 0.5f; s.Speed = 0.25f; s.TurnRate = 1.0f;
            s.DepthLo = 0.55f; s.DepthHi = 0.9f; s.Fear = 0.6f;
            s.ColorDeep = "#8A6E9E"; s.ColorBright = "#C8B0D8"; s.ColorFin = "#6E587E";
            s.Glow = 0.45f; s.BodyHeight = 1.6f;
        }),
        S("bioluminescent-fish", "Bioluminescent Fish", "Deep Sea", s =>
        {
            s.SizeM = 0.12f; s.Speed = 0.4f; s.TurnRate = 2.0f;
            s.School = 0.4f; s.DepthLo = 0.5f; s.DepthHi = 0.9f;
            s.ColorDeep = "#1E2E3E"; s.ColorBright = "#58E8E0"; s.ColorFin = "#2E4858";
            s.Glow = 1.0f; s.Pattern = BodyPattern.GlowDots;
        }),

        // ───────────────────────────── Aquatic Life ─────────────────────────────
        S("jellyfish", "Jellyfish", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Jelly;
            s.SizeM = 0.26f; s.Speed = 0.1f; s.TurnRate = 0.6f;
            s.Restfulness = 0.5f; s.Fear = 0.2f; s.DepthLo = 0.1f; s.DepthHi = 0.7f;
            s.ColorDeep = "#B8D8E8"; s.ColorBright = "#E8F4F8"; s.ColorFin = "#A8C8E0";
            s.Glow = 0.5f; s.BodyHeight = 1.3f;
        }),
        S("seahorse", "Seahorse", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Drifter;
            s.SizeM = 0.08f; s.Speed = 0.08f; s.TurnRate = 1.2f;
            s.Restfulness = 0.9f; s.Fear = 0.4f; s.DepthLo = 0.3f; s.DepthHi = 0.8f;
            s.ColorDeep = "#C9A05E"; s.ColorBright = "#E8CCA0"; s.ColorFin = "#A8844E";
            s.BodyHeight = 1.4f; s.BodyWidth = 0.45f; s.HeadBlunt = 0.7f; s.TailLength = 0.3f; s.TailFork = 0.1f; s.DorsalHeight = 0.2f;
        }),
        S("sea-turtle", "Sea Turtle", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Drifter;
            s.SizeM = 0.55f; s.Speed = 0.25f; s.TurnRate = 0.8f;
            s.Restfulness = 0.5f; s.Curiosity = 0.4f; s.Fear = 0.25f; s.DepthLo = 0.08f; s.DepthHi = 0.5f;
            s.ColorDeep = "#5E7E5A"; s.ColorBright = "#D8D0B0"; s.ColorFin = "#6E8E6A";
            s.Pattern = BodyPattern.Spots; s.BodyHeight = 0.75f; s.BodyWidth = 1.05f; s.HeadBlunt = 0.6f; s.TailLength = 0.12f; s.DorsalHeight = 0.2f; s.PectoralSize = 0.8f;
        }),
        S("octopus", "Octopus", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Jelly;
            s.SizeM = 0.4f; s.Speed = 0.15f; s.TurnRate = 0.9f;
            s.Restfulness = 0.75f; s.Fear = 0.8f; s.Curiosity = 0.5f; s.DepthLo = 0.7f; s.DepthHi = 0.97f;
            s.ColorDeep = "#B86E5E"; s.ColorBright = "#E8A898"; s.ColorFin = "#9E5E50";
            s.Glow = 0.15f; s.BodyHeight = 1.25f;
        }),
        S("cuttlefish", "Cuttlefish", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Jelly;
            s.SizeM = 0.25f; s.Speed = 0.2f; s.TurnRate = 1.0f;
            s.Restfulness = 0.6f; s.Curiosity = 0.5f; s.Fear = 0.5f; s.DepthLo = 0.5f; s.DepthHi = 0.9f;
            s.ColorDeep = "#C8A87E"; s.ColorBright = "#E8D4B0"; s.ColorFin = "#A8885E";
            s.BodyHeight = 1.15f;
        }),
        S("shrimp", "Shrimp", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Drifter;
            s.SizeM = 0.05f; s.Speed = 0.35f; s.TurnRate = 2.6f;
            s.Restfulness = 0.7f; s.Fear = 0.85f; s.DepthLo = 0.6f; s.DepthHi = 0.95f;
            s.ColorDeep = "#E8D8C8"; s.ColorBright = "#F8EFE4"; s.ColorFin = "#D8B8A8";
            s.BodyHeight = 0.75f; s.BodyWidth = 0.7f;
        }),
        S("crab", "Crab", "Aquatic Life", s =>
        {
            s.Form = SpeciesForm.Drifter;
            s.SizeM = 0.1f; s.Speed = 0.12f; s.TurnRate = 1.4f;
            s.Restfulness = 0.85f; s.Fear = 0.6f; s.DepthLo = 0.8f; s.DepthHi = 0.98f;
            s.ColorDeep = "#C4553C"; s.ColorBright = "#E8845C"; s.ColorFin = "#A84432";
            s.BodyHeight = 0.6f; s.BodyWidth = 1.2f; s.TailLength = 0.08f; s.DorsalHeight = 0.08f;
        }),
    };
}
