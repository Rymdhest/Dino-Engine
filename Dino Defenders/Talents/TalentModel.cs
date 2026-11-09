using Dino_Defenders.Stats;
using OpenTK.Mathematics;

namespace Dino_Defenders.Talents
{
    /// <summary>
    /// A cluster: a few small nodes curled around something worth reaching. Each small node gives exactly one thing, the same thing, and the
    /// notable at the end gives several. Its id is saved in heroes, so it stays.
    /// </summary>
    public sealed class Pod
    {
        public string Id = "";
        public string Name = "";
        public int Smalls = 3;
        public Mod Small;
        public Mod[] Mods = Array.Empty<Mod>();
    }

    /// <summary>
    /// One of the six ways out of the middle: a travel node, a notable that says what kind of hero this is going to be, and a fork into the two
    /// regions it opens onto, the one it points into and the next one going round. Its id is saved in heroes, so it stays.
    /// </summary>
    public sealed class Beginning
    {
        public string Id = "";
        public string Name = "";
        public string Lore = "";
        public float Angle;

        /// <summary>The region it points into, then the one beside it, clockwise.</summary>
        public string[] Regions = Array.Empty<string>();
        public Mod[] Mods = Array.Empty<Mod>();
    }

    /// <summary>
    /// A path out from where a way out of the middle forks, or from another lane, with clusters hanging off it on either side. Lanes are what give a region its shape:
    /// they branch as they go out, bend, and stop where the region's ideas run out, so no two regions look alike.
    /// </summary>
    public sealed class Lane
    {
        public string Id = "";

        /// <summary>The lane it branches from, or nothing for the one that starts where its region's way out of the middle forks.</summary>
        public string? From;

        /// <summary>Degrees from the middle of its region that it settles at.</summary>
        public float Angle;

        /// <summary>How far from the root it leaves the lane it branches from.</summary>
        public float Start;

        /// <summary>How far from the root it ends.</summary>
        public float End;

        /// <summary>Degrees it drifts for every thousand units it goes out.</summary>
        public float Bend;

        public Pod[] Pods = Array.Empty<Pod>();
    }

    /// <summary>
    /// A ring of small nodes around a heart, with spokes in to it and notables set into the rim. Wheels are where the rare and powerful things are:
    /// penetration, maximum resistances, conversion, more projectiles, and the pools.
    /// </summary>
    public sealed class Wheel
    {
        public string Id = "";
        public string Name = "";
        public float Radius;
        public float Angle;

        /// <summary>What the rim's small nodes give, in turn.</summary>
        public Mod[] Ring = Array.Empty<Mod>();

        /// <summary>Set into the rim, evenly. Their own small-node counts are ignored.</summary>
        public Pod[] Notables = Array.Empty<Pod>();

        /// <summary>What the heart gives, unless a keystone sits there.</summary>
        public Mod[] Centre = Array.Empty<Mod>();
        public Keystone? Heart;
        public int RimNodes = 12;
        public int Spokes = 2;
    }

    /// <summary>A node that changes how someone plays, rather than how much. Each is placed by hand, where its idea lives.</summary>
    public sealed class Keystone
    {
        public string Id = "";
        public string Name = "";
        public string Lore = "";
        public string? Title;
        public Mod[] Mods = Array.Empty<Mod>();
        public float Radius;
        public float Angle;

        /// <summary>What the small nodes leading to it give, if anything but the land's attribute.</summary>
        public Mod? Path;

        /// <summary>Its picture on the tree: see <see cref="TalentGlyph"/>.</summary>
        public TalentGlyph Emblem;
    }

    /// <summary>A way of fighting. None of them is a class: where someone spends their points is what they are.</summary>
    public sealed class Region
    {
        public string Id = "";
        public string Name = "";
        public string Adjective = "";
        public string Noun = "";
        public Vector3 Colour;

        /// <summary>Degrees clockwise from the top of the tree, and how wide it is.</summary>
        public float Centre;
        public float Width;

        /// <summary>What its travel nodes give: the first, and the second every other node for those between two.</summary>
        public Stat Attribute;
        public Stat? Second;

        public Lane[] Lanes = Array.Empty<Lane>();
        public Wheel[] Wheels = Array.Empty<Wheel>();
        public Keystone[] Keystones = Array.Empty<Keystone>();

        /// <summary>The notable the region opens with, right where its way out of the middle forks, so that the fork is a choice between two of them.</summary>
        public Pod? Entry;
    }

    /// <summary>Where two neighbouring regions meet, a notable for those who walk both. <see cref="Left"/> is the region counter-clockwise of the join.</summary>
    public sealed class Bridge
    {
        public string Id = "";
        public string Name = "";
        public int Left;
        public float Radius;
        public Mod Small;
        public Mod[] Mods = Array.Empty<Mod>();
    }

    /// <summary>
    /// A highway: nodes far apart, each giving only a little, so that getting somewhere costs few points. A ring goes all the way round at one
    /// distance from the root; a spoke runs straight out at one angle, from one ring to another.
    /// </summary>
    public sealed class Road
    {
        public string Id = "";
        public string Name = "";
        public bool Ring;
        public float Radius;    // For a ring.
        public float Angle;     // For a spoke, with how far in and out it runs.
        public float From;
        public float To;

        /// <summary>For a ring that only goes part of the way round: clockwise from one angle to the other. Both nothing is all the way round.</summary>
        public float ArcFrom;
        public float ArcTo;

        public bool Whole => ArcFrom == ArcTo;

        /// <summary>How far round from where it starts an angle is, in degrees, for a ring that only goes part of the way.</summary>
        public float Along(float degrees) => ((degrees - ArcFrom) % 360f + 360f) % 360f;

        public bool Covers(float degrees) => Whole || Along(degrees) <= Along(ArcTo);
    }

    /// <summary>Two waygates, far apart. Light one and the other can be taken from there without walking.</summary>
    public sealed class Gate
    {
        public string Id = "";
        public float RadiusA;
        public float AngleA;
        public float RadiusB;
        public float AngleB;
    }
}
