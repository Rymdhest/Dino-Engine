namespace Dino_Engine.World.Content
{
    /// <summary>
    /// What shape a biome's mountains take. The world's rock (<see cref="Geology"/>) says how they break, and this what they break into, so one rock can stand in towers in one
    /// biome and in ledges in the next, as limestone does in the karst of the south and the escarpments of the north. A mountain takes the form of whichever biomes are on it,
    /// blended across their borders (RockSculpt). Read TERRAIN.md.
    /// </summary>
    public enum MountainForm
    {
        Ledged,     // Beds laid level: cliffs in steps with grassy benches between, and tops cut flat at a bed. Escarpments, mesas, downs.
        Jagged,     // The high mountains: sharp crests drawn up into teeth, couloirs down every face, everything steep and broken.
        Towers,     // Steep towers with clefts between them, standing out of the mountain's foot: karst, sugarloaves, pillars of sandstone.
        Domes,      // Swelling bare domes, sheets peeling off them in rings, and only shallow runnels down them.
        Canyons,    // A plateau cut flat at a bed, and narrow ravines through it with sheer stepped walls.
        Columns,    // Lava laid in flows, in steps, each a cliff of columns over a bench.
        Blades,     // Beds stood on end: ribs and fins in rows, all leaning one way, a gentle back and a sheer front.
    }

    /// <summary>The numbers a form shapes its mountains with (RockSculpt). The rock's own numbers are multiplied by them.</summary>
    public readonly record struct FormStyle(
        float Bedding,      // How much of a slope's height goes into ledges, against what the rock gives.
        float Gullies,      // How hard water cuts into it, against what the rock gives.
        float Facets,       // How strongly its faces break into planes along the joints, against what the rock gives.
        float Blocks,       // Metres a joint block can stand back from its neighbours in a face, drawn with the ground (TerrainGenerator).
        float RavineDepth,  // Metres the biggest ravine cuts down.
        float RavineWidth,  // Metres across a big ravine's floor.
        float RavineWall,   // Metres over which its walls rise: little for a slot, a lot for a V.
        float Crests,       // How sharply crests are drawn up into edges and teeth.
        float Cap,          // How much of its tops are cut off flat at the foot of a bed, 0..1,
        float CapLevel);    // and how far up its mountain that bed is, as a share of the way from its foot to its top.

    /// <summary>The forms at a point, each by how much of it they have, and the numbers they shape it with between them.</summary>
    public struct FormBlend
    {
        public FormStyle Style;
        public float Ledged, Jagged, Towers, Domes, Canyons, Columns, Blades;

        public readonly float this[MountainForm form] => form switch
        {
            MountainForm.Ledged => Ledged,
            MountainForm.Jagged => Jagged,
            MountainForm.Towers => Towers,
            MountainForm.Domes => Domes,
            MountainForm.Canyons => Canyons,
            MountainForm.Columns => Columns,
            _ => Blades,
        };

        /// <summary>Whichever form has the most of the point.</summary>
        public readonly MountainForm Strongest
        {
            get
            {
                MountainForm best = MountainForm.Ledged;
                for (int i = 1; i < MountainForms.Count; i++)
                {
                    if (this[(MountainForm)i] > this[best]) best = (MountainForm)i;
                }
                return best;
            }
        }

        internal void Add(MountainForm form, float weight)
        {
            switch (form)
            {
                case MountainForm.Ledged: Ledged += weight; break;
                case MountainForm.Jagged: Jagged += weight; break;
                case MountainForm.Towers: Towers += weight; break;
                case MountainForm.Domes: Domes += weight; break;
                case MountainForm.Canyons: Canyons += weight; break;
                case MountainForm.Columns: Columns += weight; break;
                default: Blades += weight; break;
            }
            FormStyle s = MountainForms.Of(form);
            Style = new FormStyle(Style.Bedding + s.Bedding * weight, Style.Gullies + s.Gullies * weight, Style.Facets + s.Facets * weight, Style.Blocks + s.Blocks * weight,
                Style.RavineDepth + s.RavineDepth * weight, Style.RavineWidth + s.RavineWidth * weight, Style.RavineWall + s.RavineWall * weight, Style.Crests + s.Crests * weight,
                Style.Cap + s.Cap * weight, Style.CapLevel + s.CapLevel * weight);
        }
    }

    public static class MountainForms
    {
        public const int Count = 7;

        /// <summary>
        /// The forms of the mountains at a point, from the biomes there, by their weights, unless the kind of world has one of its own. Before the biomes are sown there are
        /// none to go by, and it's whatever the first biome of the world would make of its rock.
        /// </summary>
        public static FormBlend At(WorldData world, float x, float z)
        {
            var blend = new FormBlend();
            IReadOnlyList<BiomeDefinition> biomes = world.Plan.Biomes;
            RockKind kind = world.Geology.Kind;
            // A kind of world known for its mountains has them whatever stands on them, as the Badlands' are all mesas and canyons.
            if (world.Plan.Archetype.Mountains is MountainForm known)
            {
                blend.Add(Resolve(known, kind), 1f);
                return blend;
            }
            float total = 0f;
            for (int b = 0; b < world.BiomeWeights.Length; b++)
            {
                float weight = world.BiomeWeights[b].Sample(x, z);
                if (weight <= 0.002f) continue;
                blend.Add(Resolve(biomes[b].Mountains, kind), weight);
                total += weight;
            }
            if (total <= 0f)
            {
                blend.Add(Resolve(biomes.Count > 0 ? biomes[0].Mountains : MountainForm.Ledged, kind), 1f);
                total = 1f;
            }
            float inverse = 1f / total;
            blend.Ledged *= inverse;
            blend.Jagged *= inverse;
            blend.Towers *= inverse;
            blend.Domes *= inverse;
            blend.Canyons *= inverse;
            blend.Columns *= inverse;
            blend.Blades *= inverse;
            FormStyle s = blend.Style;
            blend.Style = new FormStyle(s.Bedding * inverse, s.Gullies * inverse, s.Facets * inverse, s.Blocks * inverse, s.RavineDepth * inverse, s.RavineWidth * inverse,
                s.RavineWall * inverse, s.Crests * inverse, s.Cap * inverse, s.CapLevel * inverse);
            return blend;
        }

        public static FormStyle Of(MountainForm form) => form switch
        {
            MountainForm.Ledged => new FormStyle(1.7f, 0.8f, 1f, 0.45f, 9f, 6f, 2.2f, 0f, 0.55f, 0.76f),
            MountainForm.Jagged => new FormStyle(0.55f, 1.55f, 1.35f, 0.6f, 7f, 3f, 7f, 1f, 0f, 0.8f),
            MountainForm.Towers => new FormStyle(0.6f, 0.6f, 0.8f, 0.4f, 4f, 3f, 2f, 0f, 0f, 0.8f),
            MountainForm.Domes => new FormStyle(0.3f, 0.35f, 0.4f, 0.22f, 2.5f, 4f, 6f, 0f, 0f, 0.8f),
            MountainForm.Canyons => new FormStyle(1.5f, 1f, 0.9f, 0.5f, 24f, 5f, 1.4f, 0f, 1f, 0.58f),
            MountainForm.Columns => new FormStyle(1.9f, 0.7f, 0.7f, 0.75f, 12f, 5f, 1.6f, 0f, 0.75f, 0.66f),
            _ => new FormStyle(0.45f, 1.2f, 1.45f, 0.55f, 6f, 3f, 2.5f, 0.4f, 0f, 0.8f),
        };

        /// <summary>
        /// What a biome's form comes to in a world of this rock: columns are basalt's alone and blades need beds stood on end, as schist's are, so neither can be anything else's;
        /// and basalt laid level is always in columns, as schist can never lie in ledges.
        /// </summary>
        public static MountainForm Resolve(MountainForm form, RockKind kind) => (form, kind) switch
        {
            (MountainForm.Columns, not RockKind.Basalt) => MountainForm.Ledged,
            (MountainForm.Blades, not RockKind.Schist) => MountainForm.Jagged,
            (MountainForm.Ledged, RockKind.Basalt) => MountainForm.Columns,
            (MountainForm.Ledged, RockKind.Schist) => MountainForm.Blades,
            _ => form,
        };
    }
}
