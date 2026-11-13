using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.World;
using Dino_Engine.World.Generation;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// How many enemies a land holds, worked out before anyone goes there, for the map table. A land is a pure function of its request, so this grows exactly the world the server will grow, from
    /// exactly the terms it will hold (<see cref="Lands.TermsFor"/>, <see cref="Lands.RequestFor"/>), and counts it by the same rules the server spawns by: <see cref="Stands"/> is what
    /// <c>EncounterSystem</c> brings in and what a place's <see cref="SiteState.HostilesTotal"/> counts, <see cref="IsTyrant"/> is who comes in as a tyrant, and <see cref="ArenaWaves"/> is how many
    /// an arena lets in. What depends on how a fight goes can only be said as the most it can come to: an arena's waves open as its tyrant weakens, its clutches hatch unless they're smashed, and on
    /// a land under the Ossuary's Writ what falls near a hero gets up once. A player is never told a number that isn't true, so keep every rule here one the server reads too.
    /// </summary>
    public readonly record struct LandCensus(int Enemies, int Packs, int Places, int Clutches, int MostThroughGates, int MostHatched, int MostRisen)
    {
        /// <summary>Whether a spawn stands in the land as an enemy, on a land with these terms: an arena's second tyrant only when the orders ask for two.</summary>
        public static bool Stands(SpawnRole role, LandTerms terms) => role.IsHostile() && (role != SpawnRole.Rival || terms.Has(OrdersCatalog.TwinPens));

        /// <summary>Whether a spawn comes in as the second tyrant the Twin Pens ask for: an arena's rival, or elsewhere the leader of the remotest bandit camp.</summary>
        public static bool IsTwin(WorldData world, LandTerms terms, WorldSite site, int spawnIndex) => world.Plan.Archetype.Arena
            ? site.Spawns[spawnIndex].Role == SpawnRole.Rival
            : terms.Has(OrdersCatalog.TwinPens) && site == Chronicle.TwinSite(world) && spawnIndex == FirstHostile(site);

        /// <summary>Whether a spawn comes in as a tyrant, which never rises again.</summary>
        public static bool IsTyrant(WorldData world, LandTerms terms, WorldSite site, int spawnIndex) => site.Spawns[spawnIndex].Role == SpawnRole.Boss || IsTwin(world, terms, site, spawnIndex);

        /// <summary>The first of a place's people, who leads them.</summary>
        public static int FirstHostile(WorldSite site)
        {
            for (int i = 0; i < site.Spawns.Count; i++)
            {
                if (site.Spawns[i].Role.IsHostile()) return i;
            }
            return -1;
        }

        /// <summary>The arena a world holds, the one its <c>ArenaSystem</c> runs, if it has one.</summary>
        public static ArenaPlan? ArenaOf(WorldData world) => world.Sites.FirstOrDefault(site => site.Arena != null)?.Arena;

        /// <summary>Everyone a grown world holds on these terms.</summary>
        public static LandCensus Of(WorldData world, LandTerms terms)
        {
            int enemies = 0, packs = 0, places = 0, tyrants = 0;
            void Count(WorldSite site, bool wild)
            {
                int here = 0;
                for (int i = 0; i < site.Spawns.Count; i++)
                {
                    if (!Stands(site.Spawns[i].Role, terms)) continue;
                    here++;
                    if (IsTyrant(world, terms, site, i)) tyrants++;
                }
                enemies += here;
                if (here == 0) return;
                if (wild) packs++;
                else places++;
            }
            foreach (WorldSite site in world.Sites) Count(site, wild: false);
            foreach (WorldSite haunt in world.Haunts) Count(haunt, wild: true);

            int clutches = 0, gates = 0, hatched = 0;
            if (ArenaOf(world) is ArenaPlan arena)
            {
                clutches = arena.Pieces.Count(piece => piece.Kind == ArenaPieceKind.Clutch);
                gates = ArenaWaves.MostLetThrough(arena.Entries.Count, terms);
                hatched = clutches * Rulebook.ClutchHatchlings;
            }

            // Whoever falls rises once, but a tyrant and whatever has already risen, and a clutch is only eggs.
            int risen = terms.Has(OrdersCatalog.Writ) ? enemies - tyrants + gates + hatched : 0;
            return new LandCensus(enemies, packs, places, clutches, gates, hatched, risen);
        }

        /// <summary>Grows a land as it will be with these orders given for it, as the server will, and counts it. Takes as long as growing a world does.</summary>
        public static LandCensus Foresee(LandOffer offer, IEnumerable<Item> orders)
        {
            LandTerms terms = Lands.TermsFor(offer, orders);
            return Of(new WorldGenerator().Generate(Lands.RequestFor(offer, terms)), terms);
        }
    }

    /// <summary>How an arena's entries let the brood in: once for every share of its tyrant's health it loses but the last, some of the gates each time and every one the last, each gate as many as the orders put at a place.</summary>
    public static class ArenaWaves
    {
        /// <summary>How many times the entries open in a fight.</summary>
        public static int Most => Math.Max((int)MathF.Round(1f / Rulebook.ArenaWaveEvery) - 1, 0);

        /// <summary>How many of an arena's entries open the <paramref name="wave"/>th time.</summary>
        public static int GatesAt(int wave, int gates) => wave >= Most ? gates : Math.Min(Rulebook.ArenaWaveGates, gates);

        /// <summary>How many come through each entry that opens.</summary>
        public static int EachGate(LandTerms terms) => Math.Max((int)MathF.Round(Rulebook.ArenaWaveSize * (1f + terms.MorePeople)), 1);

        /// <summary>How many come through in a whole fight, if the tyrant lives to see every wave.</summary>
        public static int MostLetThrough(int gates, LandTerms terms)
        {
            int total = 0;
            for (int wave = 1; wave <= Most; wave++) total += GatesAt(wave, gates) * EachGate(terms);
            return total;
        }
    }
}
