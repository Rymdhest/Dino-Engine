namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>A Voronoi diagram (<see cref="Canvas.Cells"/>). Distances are in cells.</summary>
    public sealed class Cells
    {
        /// <summary>The true distance to the cell's border, 0 on it: a joint keeps its width round corners.</summary>
        public readonly Field Border;
        /// <summary>The distance to the cell's site (F1).</summary>
        public readonly Field Centre;
        /// <summary>A random number of the cell's own, in [0, 1).</summary>
        public readonly Field Id;
        public readonly Field Id2;
        /// <summary>Where the site is from here, in cells, across and down.</summary>
        public readonly Field ToSiteX;
        public readonly Field ToSiteY;
        /// <summary>The distance to the second nearest site (F2).</summary>
        public readonly Field Second;
        /// <summary>The id of the cell across the nearest border.</summary>
        public readonly Field Neighbour;

        internal Cells(Node node)
        {
            Border = new Field(node, 0, 0);
            Centre = new Field(node, 0, 1);
            Id = new Field(node, 0, 2);
            Id2 = new Field(node, 0, 3);
            ToSiteX = new Field(node, 1, 0);
            ToSiteY = new Field(node, 1, 1);
            Second = new Field(node, 1, 2);
            Neighbour = new Field(node, 1, 3);
        }
    }

    /// <summary>Courses of bricks, blocks, boards or tiles (<see cref="Canvas.Bricks"/>). Distances are in the tile.</summary>
    public sealed class Bricks
    {
        /// <summary>The distance in to the piece's edge, in the tile: 0 there, negative in the joint, half the joint's width at the joint's middle.</summary>
        public readonly Field Edge;
        public readonly Field Id;
        /// <summary>How far along the piece, 0 to 1, and across it.</summary>
        public readonly Field Along;
        public readonly Field Across;
        /// <summary>The piece's length along its course and its course's height, in the tile.</summary>
        public readonly Field Length;
        public readonly Field Height;
        /// <summary>A random number of its course's own.</summary>
        public readonly Field Course;
        public readonly Field Place;
        internal readonly Node Node;

        internal Bricks(Node node)
        {
            Node = node;
            Edge = new Field(node, 0, 0);
            Id = new Field(node, 0, 1);
            Along = new Field(node, 0, 2);
            Across = new Field(node, 0, 3);
            Length = new Field(node, 1, 0);
            Height = new Field(node, 1, 1);
            Course = new Field(node, 1, 2);
            Place = new Field(node, 1, 3);
        }
    }

    /// <summary>Scattered stones (<see cref="Canvas.Scatter"/>).</summary>
    public sealed class Stones
    {
        /// <summary>The height of the highest stone here, 0 to 1, and 0 where there's none.</summary>
        public readonly Field Height;
        public readonly Field Id;
        /// <summary>How far out from its middle, 0 to 1.</summary>
        public readonly Field Out;
        /// <summary>1 on a stone, 0 off it, softened over its outermost rim.</summary>
        public readonly Field Cover;
        /// <summary>Where this is on the stone, along its long axis and across it, each -1 to 1.</summary>
        public readonly Field LocalX;
        public readonly Field LocalY;
        public readonly Field Id2;

        internal Stones(Node node)
        {
            Height = new Field(node, 0, 0);
            Id = new Field(node, 0, 1);
            Out = new Field(node, 0, 2);
            Cover = new Field(node, 0, 3);
            LocalX = new Field(node, 1, 0);
            LocalY = new Field(node, 1, 1);
            Id2 = new Field(node, 1, 2);
        }
    }

    /// <summary>A plain weave (<see cref="Canvas.Weave"/>).</summary>
    public sealed class Weave
    {
        public readonly Field Height;
        public readonly Field Id;
        /// <summary>0 on a thread running up the tile, 1 on one running across.</summary>
        public readonly Field Direction;
        /// <summary>How far along its current crossing, 0 to 1.</summary>
        public readonly Field Along;

        internal Weave(Node node)
        {
            Height = new Field(node, 0, 0);
            Id = new Field(node, 0, 1);
            Direction = new Field(node, 0, 2);
            Along = new Field(node, 0, 3);
        }
    }

    /// <summary>Links of mail (<see cref="Canvas.Mail"/>).</summary>
    public sealed class Links
    {
        public readonly Field Height;
        public readonly Field Id;
        /// <summary>How far round its link, 0 to 1.</summary>
        public readonly Field Round;
        public readonly Field Cover;

        internal Links(Node node)
        {
            Height = new Field(node, 0, 0);
            Id = new Field(node, 0, 1);
            Round = new Field(node, 0, 2);
            Cover = new Field(node, 0, 3);
        }
    }

    /// <summary>Broken rock (<see cref="Canvas.Facets"/>).</summary>
    public sealed class Facets
    {
        public readonly Field Height;
        public readonly Field Id;
        /// <summary>The distance to the face's border, in cells.</summary>
        public readonly Field Border;
        /// <summary>How far the face's plane leans here.</summary>
        public readonly Field Plane;

        internal Facets(Node node)
        {
            Height = new Field(node, 0, 0);
            Id = new Field(node, 0, 1);
            Border = new Field(node, 0, 2);
            Plane = new Field(node, 0, 3);
        }
    }

    /// <summary>The grain of sawn boards (<see cref="Canvas.Grain"/>).</summary>
    public sealed class Grain
    {
        /// <summary>The dark late wood of each growth ring, 0 to 1.</summary>
        public readonly Field Late;
        /// <summary>Where in its ring this is, 0 to 1.</summary>
        public readonly Field Ring;
        /// <summary>Fine pores along the grain, around 0.5.</summary>
        public readonly Field Pores;
        /// <summary>1 in a knot.</summary>
        public readonly Field Knots;

        internal Grain(Node node)
        {
            Late = new Field(node, 0, 0);
            Ring = new Field(node, 0, 1);
            Pores = new Field(node, 0, 2);
            Knots = new Field(node, 0, 3);
        }
    }
}
