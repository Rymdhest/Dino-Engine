using Dino_Defenders.Client;
using Dino_Defenders.Server;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.World.Content;
using Dino_Engine.World.Lexicon;
using OpenTK.Mathematics;

namespace Dino_Defenders.AssetLexicon
{
    /// <summary>
    /// The game's side of the asset lexicon (LEXICON.md): everything that walks. Three sections, nearest the gate first:
    /// <list type="bullet">
    /// <item>Dinosaurs: a row for every species in <see cref="SaurianCatalog"/>, by how it's built, and in each row the same five plots in the same order, the Reich's in harness, wild, raised as
    /// bones, rotting and a ghost, each holding whatever the game draws that way, an enemy, a captive or the Haven's giants, or standing empty and saying so, which is what's missing.</item>
    /// <item>Creatures: everything else in <see cref="Bestiary.Everyone"/>, by what it's drawn as, so that what shares a look stands together, and what's summoned.</item>
    /// <item>People: the looks everyone the game draws as a person is made from, villagers and heroes, and what stands in the Haven.</item>
    /// </list>
    /// All of it read from the catalogues as they are, so a new kind turns up in its place by itself. Each stands three quarters on to the camera, facing its left, as the game's parades do.
    /// </summary>
    [LexiconShelf]
    public sealed class CreatureShelf : ILexiconShelf
    {
        /// <summary>Radians every creature is turned, front to the camera's left, as <see cref="EncounterSystem.Parade"/> stands them.</summary>
        public const float Facing = MathF.PI * 0.8f;

        private const float PersonPlot = 7.5f;
        private const float Room = 3f;              // Metres between a body's reach and its neighbour's plot.

        /// <summary>The ways a dinosaur is met, in the order every species' row shows them, with the word for one met so, as the bestiary names them (Bone Raptor, Ghost Raptor).</summary>
        private static readonly (string Word, string Plaque, string Adjective, SaurianCondition Condition, bool Harnessed)[] Ways =
        {
            ("Reich", "In the Reich's harness", "Reich", SaurianCondition.Living, true),
            ("Wild", "Wild", "wild", SaurianCondition.Living, false),
            ("Bones", "Raised as bones", "bone", SaurianCondition.Bones, false),
            ("Rotting", "Rotting", "rotting", SaurianCondition.Rotting, false),
            ("Ghost", "A ghost", "ghost", SaurianCondition.Ghost, false),
        };

        public IEnumerable<ExhibitSection> Sections()
        {
            yield return Dinosaurs();
            yield return Creatures();
            yield return People();
        }

        // ---------------------------------------------------------------- Dinosaurs

        private static ExhibitSection Dinosaurs()
        {
            var section = new ExhibitSection
            {
                Key = "dinosaurs",
                Name = "Dinosaurs",
                Blurb = "A row for every species, by how it's built, and in every row the same five plots: in the Reich's harness, wild, raised as bones, rotting, and a ghost. An empty plot is a way no species is drawn yet.",
                Order = 10f,
                Ground = ExhibitGround.Gravel,
            };
            foreach (SaurianForm form in Enum.GetValues<SaurianForm>())
            {
                foreach (SaurianSpecies species in SaurianCatalog.All.Where(species => species.Form == form)) section.Families.Add(Species(species));
            }
            return section;
        }

        private static ExhibitFamily Species(SaurianSpecies species)
        {
            var cells = new List<Exhibit>();
            int found = 0;
            foreach ((string word, string plaque, string adjective, SaurianCondition condition, bool harnessed) in Ways)
            {
                if (species.Free && (harnessed || condition != SaurianCondition.Living)) continue;
                Exhibit cell = Cell(species, word, plaque, adjective, condition, harnessed);
                if (cell.Kind != LexiconKind.Missing) found++;
                cells.Add(cell);
            }

            // Every plot in a row as big as its biggest, so that the five ways line up, each body still in the middle of its own.
            Vector2 size = new Vector2(cells.Max(cell => cell.Size.X), cells.Max(cell => cell.Size.Y));
            foreach (Exhibit cell in cells) cell.Size = size;

            int ways = species.Free ? 1 : Ways.Length;
            var family = new ExhibitFamily { Name = species.Name, Detail = $"{species.Form} · {found} of {ways} {(ways == 1 ? "way" : "ways")} drawn" };
            family.Exhibits.AddRange(cells);
            return family;
        }

        // Whatever the game draws as one species one way: the enemies that look so, or failing those what's caged in prisons, or the Haven's giants, or nothing.
        private static Exhibit Cell(SaurianSpecies species, string word, string plaque, string adjective, SaurianCondition condition, bool harnessed)
        {
            bool Is(string look) => SaurianSpec.Parse(look) is SaurianSpec spec && spec.Species == species.Id && spec.Condition == condition && spec.Harnessed == harnessed;
            string named = $"{SaurianSpec.Prefix}{species.Id}{(harnessed ? ".reich" : condition switch { SaurianCondition.Bones => ".bones", SaurianCondition.Rotting => ".rot", SaurianCondition.Ghost => ".ghost", _ => "" })}";
            string key = $"creature/{species.Id}.{word.ToLowerInvariant()}";

            List<EnemyKind> kinds = Bestiary.Everyone.Where(kind => Is(LookOf(kind))).ToList();
            if (kinds.Count > 0)
            {
                EnemyKind first = kinds[0];
                string look = LookOf(first);
                return Creature(key, string.Join(" / ", kinds.Select(kind => kind.Name)), $"{word} · {string.Join(", ", kinds.Select(kind => kind.Id))} · {look}", look, first.Scale,
                    $"{species.Name} {word} {plaque} dinosaur {species.Form}");
            }

            foreach ((string captive, int _, float scale, Vector3 _) in PlaceStory.Captives)
            {
                if (!Is(captive + "0")) continue;
                return Creature(key, $"Captive {species.Name}", $"{word} · only caged, never fought · {captive}0", captive + "0", scale, $"{species.Name} {word} {plaque} dinosaur prison cage");
            }

            foreach ((string giant, float scale) in Dino_Engine.Wildlife.Herds.Haven)
            {
                if (!Is(giant)) continue;
                return Creature(key, $"Haven {species.Name}", $"{word} · the Haven's own, only looked at · {giant}", giant, scale, $"{species.Name} {word} {plaque} dinosaur sauropod herd");
            }

            // Nothing: as much room as the wild one of its kind would want, so that the row still lines up.
            (Vector2 size, _, _, _) = Measure($"{SaurianSpec.Prefix}{species.Id}.0", 1f);
            return new Exhibit
            {
                Key = key,
                Name = $"No {adjective} {species.Name.ToLowerInvariant()} yet",
                Detail = $"Nothing is drawn as {named}",
                Kind = LexiconKind.Missing,
                Look = named,
                Size = size,
                Keywords = $"{species.Name} {word} {plaque} missing dinosaur",
            };
        }

        /// <summary>
        /// The ground a body covers, stood as the lexicon stands it, worked out from the body itself and never by growing it: a dinosaur's spine with its flesh either side, its head, its legs
        /// and arms; one of the land's animals from its snout to its rump and tail. Turned to <see cref="Facing"/> and scaled, with room round it for what's laid over a body, a harness, a
        /// commander's banners, a coat. Says its plot's size, how far from the plot's middle its origin goes so that the whole of it is in the middle, how tall it is, and how near the middle
        /// of its plot someone coming from the south can stand and be clear of it. The body is filled in with points no further apart than <see cref="Step"/>, so that nothing of it is
        /// missed between them.
        /// </summary>
        private static (Vector2 Size, Vector2 Offset, float Height, float Clear) Measure(string look, float scale)
        {
            var points = new List<Vector2>();   // In the body's own frame: x to its left, z ahead.
            float reach = 0.6f, tall;
            if (SaurianSpec.Parse(look) is SaurianSpec spec && SaurianCatalog.Find(spec.Species) is SaurianSpecies species)
            {
                var body = new SaurianBody(species, spec.Seed);
                SaurianAxis axis = body.Axis;
                tall = 0f;
                for (int i = 0; i < axis.Points.Length; i++)
                {
                    Vector3 point = axis.Points[i];
                    tall = MathF.Max(tall, point.Y + axis.Deep[i]);
                    if (i == 0) continue;
                    Vector3 last = axis.Points[i - 1];
                    Patch(points, new Vector2(last.X - axis.Wide[i - 1], last.Z), new Vector2(last.X + axis.Wide[i - 1], last.Z), new Vector2(point.X - axis.Wide[i], point.Z), new Vector2(point.X + axis.Wide[i], point.Z));
                }
                SaurianHead head = body.Head;
                Vector3 back = head.ToBody(Vector3.Zero), snout = head.ToBody(new Vector3(0f, 0f, head.Length));
                Vector2 ahead = snout.Xz - back.Xz;
                Vector2 side = ahead.LengthSquared > 1e-6f ? new Vector2(ahead.Y, -ahead.X).Normalized() : Vector2.UnitX;
                Patch(points, back.Xz - side * (head.BackWidth * 0.5f), back.Xz + side * (head.BackWidth * 0.5f), snout.Xz - side * (head.TipWidth * 0.5f), snout.Xz + side * (head.TipWidth * 0.5f));
                tall = MathF.Max(tall, MathF.Max(back.Y + head.BackDepth, snout.Y + head.TipDepth));
                foreach (SaurianLeg leg in body.Legs)
                {
                    Line(points, leg.Hip.Xz, leg.Knee.Xz);
                    Line(points, leg.Knee.Xz, leg.Ankle.Xz);
                    Line(points, leg.Ankle.Xz, leg.Ball.Xz);
                }
                foreach (SaurianArm arm in body.Arms)
                {
                    Line(points, arm.Shoulder.Xz, arm.Elbow.Xz);
                    Line(points, arm.Elbow.Xz, arm.Hand.Xz);
                }
                reach += body.Length * 0.06f;
            }
            else if (BoarSpec.Parse(look) is BoarSpec boar)
            {
                var body = new BoarBody(boar);
                tall = Quadruped(points, body.RumpBack, body.SnoutTip.Z, body.Length * 0.24f, body.Length * 0.15f);
            }
            else if (WolfSpec.Parse(look) is WolfSpec wolf)
            {
                var body = new WolfBody(wolf);
                tall = Quadruped(points, body.RumpBack, body.NoseTip.Z, body.Length * 0.2f, body.Length * 0.45f);
            }
            else if (BearSpec.Parse(look) is BearSpec bear)
            {
                var body = new BearBody(bear);
                tall = Quadruped(points, body.RumpBack, body.NoseTip.Z, body.Length * 0.26f, body.Length * 0.06f);
            }
            else return (new Vector2(PersonPlot * MathF.Max(scale, 1f)), Vector2.Zero, PersonHeight * scale, PersonClear * scale);

            // Turned about the vertical as the world turns it, +z ahead going to (sin, cos) of the yaw.
            (float sin, float cos) = MathF.SinCos(Facing);
            var turned = new List<Vector2>(points.Count);
            Vector2 min = new Vector2(float.MaxValue), max = new Vector2(float.MinValue);
            foreach (Vector2 point in points)
            {
                Vector2 at = new Vector2(point.X * cos + point.Y * sin, -point.X * sin + point.Y * cos) * scale;
                turned.Add(at);
                min = Vector2.ComponentMin(min, at);
                max = Vector2.ComponentMax(max, at);
            }
            float margin = reach * scale;
            min -= new Vector2(margin);
            max += new Vector2(margin);
            Vector2 middle = (min + max) * 0.5f;

            // Due south of the middle, as far back as the nearest of it to that line wants: the flesh and what's over it, and whoever's standing there.
            float near = margin + StanderRadius, clear = 0f;
            foreach (Vector2 at in turned)
            {
                float across = at.X - middle.X;
                if (MathF.Abs(across) < near) clear = MathF.Max(clear, middle.Y - at.Y + MathF.Sqrt(near * near - across * across));
            }
            return (max - min + new Vector2(Room), -middle, tall * scale, clear);
        }

        private const float Step = 0.2f;            // Metres in a body's own frame between the points it's filled in with.
        private const float StanderRadius = 0.5f;   // Metres round someone standing.
        private const float PersonHeight = 1.9f;
        private const float PersonClear = 0.8f;     // Metres from a person's or a prop's middle to where someone can stand by them.

        // A four-legged body from its snout back to the end of its tail, as wide as it is. Says how tall it stands, about, from how long it is.
        private static float Quadruped(List<Vector2> points, float rump, float nose, float halfWidth, float tail)
        {
            Patch(points, new Vector2(-halfWidth, rump - tail), new Vector2(halfWidth, rump - tail), new Vector2(-halfWidth, nose), new Vector2(halfWidth, nose));
            return (nose - rump) * 0.6f;
        }

        // Points over a four-sided patch, a0 to a1 along one end and b0 to b1 along the other.
        private static void Patch(List<Vector2> points, Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            int across = Math.Max(1, (int)MathF.Ceiling(MathF.Max((a1 - a0).Length, (b1 - b0).Length) / Step));
            int along = Math.Max(1, (int)MathF.Ceiling(MathF.Max((b0 - a0).Length, (b1 - a1).Length) / Step));
            for (int i = 0; i <= along; i++)
            {
                float t = i / (float)along;
                for (int j = 0; j <= across; j++)
                {
                    float s = j / (float)across;
                    points.Add(Vector2.Lerp(Vector2.Lerp(a0, a1, s), Vector2.Lerp(b0, b1, s), t));
                }
            }
        }

        private static void Line(List<Vector2> points, Vector2 a, Vector2 b) => Patch(points, a, a, b, b);

        // ---------------------------------------------------------------- Creatures

        private static ExhibitSection Creatures()
        {
            var section = new ExhibitSection
            {
                Key = "creatures",
                Name = "Creatures",
                Blurb = "Everything else a land can field, by what it's drawn as, so that what shares a look stands together: the Reich's soldiers, turncoats and outlaws, the land's powers' servants, the dead, the land's animals, and what heroes raise.",
                Order = 20f,
                Ground = ExhibitGround.Gravel,
            };
            var families = new Dictionary<string, (int Order, ExhibitFamily Family)>();
            void Add(int order, string name, string detail, Exhibit exhibit)
            {
                if (!families.TryGetValue(name, out var family)) families[name] = family = (order, new ExhibitFamily { Name = name, Detail = detail });
                family.Family.Exhibits.Add(exhibit);
            }

            foreach (EnemyKind kind in Bestiary.Everyone)
            {
                string look = LookOf(kind);
                if (SaurianSpec.Parse(look) != null) continue;
                (int order, string name, string detail) = FamilyOf(kind, look);
                Add(order, name, detail, Creature($"creature/{kind.Id}", kind.Name, $"{kind.Id} · {look}{(kind.Scale != 1f ? FormattableString.Invariant($" · {kind.Scale:0.##}×") : "")}", look, kind.Scale, $"{name} {kind.Look}"));
            }
            // What's set down rather than raised: its stand, which is what stands there, with whatever weapon it's given hovering over it.
            Exhibit turret = Creature("creature/turret", Bestiary.Turret.Name, $"turret · its stand: the weapon it's given hovers over it", Gameplay.MinionLooks.Stand, 1f, "summoned minion");
            turret.Prop = true;
            Add(5, "Raised and summoned", "What heroes call up to fight beside them", turret);

            foreach ((int _, ExhibitFamily family) in families.Values.OrderBy(entry => entry.Order).ThenBy(entry => entry.Family.Name, StringComparer.Ordinal)) section.Families.Add(family);
            foreach (ExhibitFamily family in section.Families) family.Detail += $" · {LexiconWords.Kinds(family.Exhibits.Count)}";
            return section;
        }

        // Which family a kind belongs in, by what it's drawn as. A look nothing here knows goes in a family of its own, so it's seen, and wants a line here.
        private static (int Order, string Name, string Detail) FamilyOf(EnemyKind kind, string look)
        {
            if (kind == Bestiary.Bones || kind == Bestiary.Hound) return (5, "Raised and summoned", "What heroes call up to fight beside them");
            if (look.StartsWith(CharacterFactory.ReichPrefix, StringComparison.Ordinal)) return (0, "The Reich's soldiers", "Greycoats in iron helms, who march behind the beasts");
            if (look.StartsWith("bandit", StringComparison.Ordinal) || look.StartsWith("cutthroat", StringComparison.Ordinal) || look.StartsWith("brute", StringComparison.Ordinal) || look == "warlord")
                return (1, "Turncoats and outlaws", "The land's own, who hold the roads for the Reich");
            if (look.StartsWith("acolyte:", StringComparison.Ordinal) || look.StartsWith("robed:", StringComparison.Ordinal)) return (2, "Cultists and casters", "Who serve the land's powers, robed or hooded");
            if (look.StartsWith("skeleton", StringComparison.Ordinal) || look == "revenant") return (3, "The dead", "What the Ossuary raised");
            if (CharacterFactory.IsBeast(look)) return (4, "The land's animals", "Wild, and grown as the dinosaurs are");
            return (9, "Unsorted", "Drawn as nothing the lexicon knows: give their look a family in CreatureShelf");
        }

        // ---------------------------------------------------------------- People

        private static ExhibitSection People()
        {
            var section = new ExhibitSection
            {
                Key = "people",
                Name = "People",
                Blurb = "Everyone the game draws as a person is one of these looks: villagers, merchants, elders, the Haven's keepers and the beta vendor are villagers, and each hero is told apart by their colours.",
                Order = 30f,
                Ground = ExhibitGround.Gravel,
            };

            var villagers = new ExhibitFamily { Name = "Villagers", Detail = $"Merchants, elders, keepers and watchers too · {LexiconWords.Kinds(CharacterFactory.VillagerLooks)}" };
            for (int i = 0; i < CharacterFactory.VillagerLooks; i++)
            {
                villagers.Exhibits.Add(Creature($"person/villager{i}", $"Villager {i + 1}", $"villager{i}, the same as villager{i + CharacterFactory.VillagerLooks}", $"villager{i}", 1f, "person npc villager merchant elder keeper vendor"));
            }
            section.Families.Add(villagers);

            var heroes = new ExhibitFamily { Name = "Heroes", Detail = $"In each player's colours · {LexiconWords.Kinds(CharacterFactory.HeroLooks)}" };
            for (int i = 0; i < CharacterFactory.HeroLooks; i++)
            {
                heroes.Exhibits.Add(Creature($"person/hero{i}", $"Hero, player {i + 1}", $"hero{i} · the {Ordinal(i + 1)} to join, wearing nothing", $"hero{i}", 1f, "person hero player"));
            }
            section.Families.Add(heroes);

            var haven = new ExhibitFamily { Name = "In the Haven", Detail = "What stands there for heroes to use" };
            Exhibit dummy = Creature("person/dummy", Bestiary.Dummy.Name, "dummy · on the training ground, three pens of them", Bestiary.Dummy.Look, DummyScale, "haven training dummy");
            dummy.Prop = true;
            haven.Exhibits.Add(dummy);
            section.Families.Add(haven);
            return section;
        }

        /// <summary>The size the training ground's dummies are drawn at (EncounterSystem.DummyScale).</summary>
        private const float DummyScale = 1.25f;

        private static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => $"{n}th" };

        // ---------------------------------------------------------------- Shared

        /// <summary>What a kind is drawn as in the lexicon: the first of its looks, as <see cref="Bestiary.ModelOf"/> names them.</summary>
        public static string LookOf(EnemyKind kind)
        {
            if (kind.Look == "bandit") return $"bandit0:{kind == Bestiary.Archer}";
            return kind.Variants > 0 ? $"{kind.Look}0" : kind.Look;
        }

        // A body on show, with room for the whole of it as it stands.
        private static Exhibit Creature(string key, string name, string detail, string look, float scale, string keywords)
        {
            (Vector2 size, Vector2 offset, float height, float clear) = Measure(look, scale);
            return new Exhibit
            {
                Key = key,
                Name = name,
                Detail = detail,
                Kind = LexiconKind.Creature,
                Look = look,
                Scale = scale,
                Yaw = Facing,
                Size = size,
                Offset = offset,
                Height = height,
                Clear = clear,
                Keywords = keywords,
            };
        }
    }
}
