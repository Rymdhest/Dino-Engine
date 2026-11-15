using Dino_Engine.Animation;
using Dino_Engine.GUI.Text;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>A picture with straight alpha, rows from the top, four bytes a pixel, as <see cref="PngWriter"/> and the GUI take it.</summary>
    internal sealed class Picture
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Rgba;

        public Picture(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }

        /// <summary>Lays another picture over this one at a place, where its alpha says.</summary>
        public void Lay(Picture other, int atX, int atY)
        {
            for (int y = 0; y < other.Height; y++)
            {
                int ty = atY + y;
                if (ty < 0 || ty >= Height) continue;
                for (int x = 0; x < other.Width; x++)
                {
                    int tx = atX + x;
                    if (tx < 0 || tx >= Width) continue;
                    int s = (y * other.Width + x) * 4;
                    int a = other.Rgba[s + 3];
                    if (a == 0) continue;
                    int d = (ty * Width + tx) * 4;
                    int back = Rgba[d + 3];
                    int outA = a + back * (255 - a) / 255;
                    for (int ch = 0; ch < 3; ch++)
                    {
                        int over = other.Rgba[s + ch] * a + Rgba[d + ch] * back * (255 - a) / 255;
                        Rgba[d + ch] = (byte)(outA == 0 ? 0 : over / outA);
                    }
                    Rgba[d + 3] = (byte)outA;
                }
            }
        }
    }

    /// <summary>
    /// The game's logo, grown in code as everything else is, so that it can't be imported and can't go stale: the main menu draws it, and
    /// <c>--logo folder</c> prints it for anywhere outside the game (Marketing/Social). It's a three-colour print, red, bone and ink, flat and cut like
    /// paper, with no gradient, shading or texture anywhere. The red is the land's own, the Falu red of the north's houses, with its white trim as the
    /// bone lines, and never the Reich's oxblood (LORE.md).
    ///
    /// The mark is a land under the Reich: the tyrant, the game's own animal posed by the game's own animator mid-roar, seen from the side and cut to
    /// facets (finer round the head, so its teeth stay), too big for the frame, on a hill of ink before the land's jagged mountains, and one Defender on
    /// the hill with a spear, the wind at their back. The hill is edged in bone, as the ring is, and the beast's eye is the one point of light on it.
    ///
    /// The title is set in Georgia's heaviest capitals, as a poster's, with the engine's own type: DINO small and wide between two rules, DEFENDERS
    /// large, printed the mark's way, red with a bone line round it and ink round that.
    /// </summary>
    internal static class Logo
    {
        public const string Tagline = "Take it back, a place at a time";

        private readonly record struct Rgb(byte R, byte G, byte B);

        private static readonly Rgb Ink = new(24, 18, 14);
        private static readonly Rgb Bone = new(236, 226, 204);
        private static readonly Rgb Red = new(184, 52, 32);
        private static readonly Rgb RedFar = new(160, 40, 27);
        private static readonly Rgb RedNear = new(138, 33, 22);

        // ------------------------------------------------------------------------------------------------------------------------ the menu's copy

        private static Task<Picture>? _growing;
        private static Task<Picture[]>? _iconGrowing;
        private static bool _windowIconSet;
        private static FontFace? _bold;
        private static FontFace? _italic;

        /// <summary>The height the main menu shows the logo at, in its own pixels: the picture is grown at twice that, as a window may be sharper than they are.</summary>
        public const float MenuHeight = 96f;

        /// <summary>The sizes an icon holds: Windows takes the small ones for a title bar and the large for the taskbar and a shortcut.</summary>
        private static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

        /// <summary>
        /// Begins growing the menu's copy and the window's icon on another thread, as the tyrant takes a second or two to grow and nothing should wait for it.
        /// Called on the main thread, which loads the type, as the font index isn't shared safely.
        /// </summary>
        public static void Begin()
        {
            if (_growing != null) return;
            Faces();
            _growing = Task.Run(() => Lockup((int)(MenuHeight * 2f)));
            _iconGrowing = Task.Run(() => new[] { 16, 32, 48, 256 }.Select(size => Mark(size, clearCorners: true)).ToArray());
        }

        /// <summary>Gives the window the mark as its icon once it's grown, and nothing until then: called each frame, on the main thread, and does the work once.</summary>
        public static void KeepWindowIcon(OpenTK.Windowing.Desktop.NativeWindow window)
        {
            if (_windowIconSet) return;
            Begin();
            if (!_iconGrowing!.IsCompleted) return;
            _windowIconSet = true;
            if (_iconGrowing.IsFaulted)
            {
                Console.WriteLine($"The window's icon couldn't be grown: {_iconGrowing.Exception?.InnerException?.Message}");
                return;
            }
            var images = _iconGrowing.Result.Select(p => new OpenTK.Windowing.Common.Input.Image(p.Width, p.Height, p.Rgba)).ToArray();
            window.Icon = new OpenTK.Windowing.Common.Input.WindowIcon(images);
        }

        /// <summary>The menu's copy, once it's grown, and nothing until then.</summary>
        public static bool TryTake(out Picture picture)
        {
            picture = null!;
            Begin();
            if (!_growing!.IsCompleted) return false;
            if (_growing.IsFaulted)
            {
                Console.WriteLine($"The logo couldn't be grown: {_growing.Exception?.InnerException?.Message}");
                return false;
            }
            picture = _growing.Result;
            return true;
        }

        private static void Faces()
        {
            _bold ??= SystemFonts.TryLoad("Georgia Bold") ?? Ui.Face;
            _italic ??= SystemFonts.TryLoad("Georgia Italic") ?? _bold;
        }

        // ------------------------------------------------------------------------------------------------------------------------ printing it

        /// <summary>Writes the set a social page needs to a folder, from which Marketing/Social/make_social.py lays the banners. Headless: no window, no engine.</summary>
        public static int Export(string folder)
        {
            Directory.CreateDirectory(folder);
            Faces();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach ((string name, int size, bool clear) in new[] { ("emblem_1024.png", 1024, false), ("mark_1024.png", 1024, true), ("youtube_avatar_800.png", 800, false), ("discord_icon_512.png", 512, false), ("reddit_icon_256.png", 256, false) })
            {
                Save(Path.Combine(folder, name), Mark(size, clear));
            }
            Save(Path.Combine(folder, "title.png"), Title(240, false));
            Save(Path.Combine(folder, "title_tagline.png"), Title(240, true));
            Save(Path.Combine(folder, "lockup_512.png"), Lockup(512));

            // The icon the game's and the launcher's exes are built with (ApplicationIcon in both projects), and so their windows and shortcuts carry.
            string ico = Path.Combine(folder, "dino_defenders.ico");
            IcoWriter.Write(ico, IconSizes.Select(size => (size, Mark(size, clearCorners: true).Rgba)).ToList());
            Console.WriteLine($"  {Path.GetFileName(ico),-24} {string.Join(", ", IconSizes)}");
            Console.WriteLine($"Wrote the logo to {folder} in {watch.Elapsed.TotalSeconds:0.0} s");
            return 0;
        }

        private static void Save(string path, Picture picture)
        {
            PngWriter.Write(path, picture.Rgba, picture.Width, picture.Height);
            Console.WriteLine($"  {Path.GetFileName(path),-24} {picture.Width}x{picture.Height}");
        }

        // ------------------------------------------------------------------------------------------------------------------------ the lockup

        /// <summary>The mark and the title side by side, this many pixels tall, over nothing.</summary>
        public static Picture Lockup(int height)
        {
            Picture mark = Mark(height, clearCorners: true);
            Picture title = Title((int)MathF.Round(height * 0.40f), false);
            int gap = (int)MathF.Round(height * 0.10f);
            var whole = new Picture(mark.Width + gap + title.Width, height, new byte[(mark.Width + gap + title.Width) * height * 4]);
            whole.Lay(mark, 0, 0);
            whole.Lay(title, mark.Width + gap, (height - title.Height) / 2);
            return whole;
        }

        // ------------------------------------------------------------------------------------------------------------------------ the mark

        /// <summary>The round mark, size pixels square: over ink to its corners, or over nothing outside its ring.</summary>
        public static Picture Mark(int size, bool clearCorners)
        {
            (Vector2[] outline, Vector2 eye, float aspect) = Tyrant();
            float s = size;
            var sheet = new Sheet(size, size, TimesFor(size));
            float c = s / 2f;
            float field = 0.445f * s;            // the land's radius: inside it the land, outside the ring
            float line = 0.0065f * s;
            float outer = 0.492f * s;
            if (clearCorners) sheet.Disc(new Vector2(c, c), outer + line, Ink);
            else sheet.Fill(Ink);

            // The land, inside the ring.
            sheet.Clip(new Vector2(c, c), field);
            sheet.Disc(new Vector2(c, c), field, Red);

            float scale = 1.02f * s;              // wider than the frame: its tail goes out of it
            float feet = 0.74f * s;
            float right = 0.84f * s;
            float left = right - scale;
            float top = feet - scale * aspect;
            float hillTop = feet - 0.004f * s;
            float HillAt(float x)
            {
                float dx = (x - c) / (1.15f * s);
                return hillTop + 0.55f * s * (1f - MathF.Sqrt(MathF.Max(0f, 1f - dx * dx)));
            }

            // The mountains, two ridges in deeper reds.
            var rng = new Random(3);
            sheet.Polygon(Ridge(rng, -0.1f * s, 1.1f * s, hillTop + 0.02f * s, 0.10f * s, 0.21f * s, 9), RedFar);
            sheet.Polygon(Ridge(rng, -0.1f * s, 1.1f * s, hillTop + 0.02f * s, 0.05f * s, 0.13f * s, 7), RedNear);

            // The hill, in ink with a bone line along its top.
            var crest = new List<Vector2>(67);
            for (int i = 0; i <= 64; i++)
            {
                float x = -0.6f * s + 2.2f * s * i / 64f;
                crest.Add(new Vector2(x, HillAt(x)));
            }
            var bone = new List<Vector2>(crest.Count + 2);
            foreach (Vector2 p in crest) bone.Add(new Vector2(p.X, p.Y - line));
            bone.Add(new Vector2(1.6f * s, 2f * s));
            bone.Add(new Vector2(-0.6f * s, 2f * s));
            sheet.Polygon(bone, Bone);
            crest.Add(new Vector2(1.6f * s, 2f * s));
            crest.Add(new Vector2(-0.6f * s, 2f * s));
            sheet.Polygon(crest, Ink);

            // A few blades of grass, bent, in two clumps.
            foreach ((float cx, int seed) in new[] { (0.135f * s, 1), (0.80f * s, 5) })
            {
                var r = new Random(seed);
                for (int i = 0; i < 3; i++)
                {
                    float bx = cx + (i - 1) * 0.018f * s + (r.NextSingle() * 0.008f - 0.004f) * s;
                    float h = (0.05f + r.NextSingle() * 0.045f) * s;
                    float lean = (r.NextSingle() * 1.2f - 0.6f) * h * 0.5f;
                    float w = 0.0055f * s;
                    float by = HillAt(bx) + 0.004f * s;
                    sheet.Polygon(new[]
                    {
                        new Vector2(bx - w, by), new Vector2(bx - w * 0.45f + lean * 0.35f, by - h * 0.5f), new Vector2(bx + lean, by - h),
                        new Vector2(bx + w * 0.45f + lean * 0.35f, by - h * 0.5f), new Vector2(bx + w, by),
                    }, Ink);
                }
            }

            // The Defender: one figure on the hill in a cloak, facing the beast, spear planted. The cloak's hem is blown a little towards it, as the wind is at their back.
            {
                float fx = 0.215f * s;
                float fy = HillAt(fx) + 0.003f * s;
                float h = 0.095f * s;
                float hr = 0.0105f * s;
                float shoulder = fy - h + 2.3f * hr;
                var headAt = new Vector2(fx + 0.001f * s, fy - h + hr);
                sheet.Polygon(new[]
                {
                    new Vector2(fx - 0.015f * s, shoulder + 0.004f * s), new Vector2(fx - 0.009f * s, shoulder), new Vector2(fx + 0.010f * s, shoulder),
                    new Vector2(fx + 0.017f * s, shoulder + 0.006f * s), new Vector2(fx + 0.026f * s, fy - h * 0.30f), new Vector2(fx + 0.034f * s, fy),
                    new Vector2(fx - 0.019f * s, fy), new Vector2(fx - 0.017f * s, fy - h * 0.40f),
                }, Ink);
                sheet.Polygon(new[] { new Vector2(fx - 0.004f * s, fy - h + 1.6f * hr), new Vector2(fx + 0.006f * s, fy - h + 1.6f * hr), new Vector2(fx + 0.006f * s, shoulder), new Vector2(fx - 0.004f * s, shoulder) }, Ink);
                float sx = fx + 0.028f * s;
                sheet.Polygon(new[] { new Vector2(fx + 0.008f * s, shoulder + 0.004f * s), new Vector2(sx + 0.004f * s, fy - h * 0.66f), new Vector2(sx - 0.004f * s, fy - h * 0.58f), new Vector2(fx + 0.004f * s, shoulder + 0.014f * s) }, Ink);
                sheet.Polygon(new[] { new Vector2(sx - 0.0024f * s, fy), new Vector2(sx + 0.0024f * s, fy), new Vector2(sx + 0.0024f * s, fy - h * 1.25f), new Vector2(sx - 0.0024f * s, fy - h * 1.25f) }, Ink);
                sheet.Polygon(new[] { new Vector2(sx, fy - h * 1.50f), new Vector2(sx + 0.0075f * s, fy - h * 1.33f), new Vector2(sx, fy - h * 1.20f), new Vector2(sx - 0.0075f * s, fy - h * 1.33f) }, Ink);
                sheet.Disc(headAt, hr, Ink);
            }

            // The tyrant, and its eye.
            var beast = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++) beast[i] = new Vector2(left + outline[i].X * scale, top + outline[i].Y * scale * aspect);
            sheet.Polygon(beast, Ink);
            float ex = left + eye.X * scale, ey = top + eye.Y * scale * aspect, es = 0.0115f * s;
            sheet.Polygon(new[] { new Vector2(ex - es * 1.1f, ey - es * 0.45f), new Vector2(ex + es * 1.3f, ey - es * 0.15f), new Vector2(ex + es * 0.5f, ey + es * 0.55f), new Vector2(ex - es * 0.9f, ey + es * 0.5f) }, Bone);

            // The ring: a bone line where the land ends, ink, and a bone line at the edge.
            sheet.Unclip();
            sheet.Ring(new Vector2(c, c), field, line, Bone);
            sheet.Ring(new Vector2(c, c), outer, line, Bone);
            return sheet.Down();
        }

        /// <summary>How many times the size a picture is drawn at before it's brought down, which is all the softening its edges get: four, and fewer only when that would be enormous.</summary>
        private static int TimesFor(int size) => size <= 1024 ? 4 : size <= 2048 ? 2 : 1;

        /// <summary>A jagged mountain line from x0 to x1 standing on a base, its peaks lo to hi above it, each with a shoulder, as the land's jagged form breaks.</summary>
        private static List<Vector2> Ridge(Random rng, float x0, float x1, float @base, float lo, float hi, int n)
        {
            var xs = new float[n];
            var ys = new float[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = x0 + (x1 - x0) * i / (n - 1);
                ys[i] = @base - (lo + (hi - lo) * rng.NextSingle());
            }
            ys[0] = ys[n - 1] = @base - lo * 0.4f;
            var pts = new List<Vector2>(2 * n + 2) { new Vector2(x0, @base + 10f) };
            for (int i = 0; i < n - 1; i++)
            {
                pts.Add(new Vector2(xs[i], ys[i]));
                pts.Add(new Vector2(xs[i] * 0.55f + xs[i + 1] * 0.45f, MathF.Max(ys[i], ys[i + 1]) + (0.3f + 0.3f * rng.NextSingle()) * MathF.Abs(ys[i + 1] - ys[i]) + 0.02f * (x1 - x0)));
            }
            pts.Add(new Vector2(xs[n - 1], ys[n - 1]));
            pts.Add(new Vector2(x1, @base + 10f));
            return pts;
        }

        // ------------------------------------------------------------------------------------------------------------------------ the tyrant

        private static readonly object TyrantGate = new();
        private static (Vector2[] outline, Vector2 eye, float aspect)? _tyrant;

        // Where the eye and the base of the neck are on the animal, as shares of its box, read off the picture once.
        private static readonly Vector2 EyeAt = new(0.9194f, 0.0653f);
        private static readonly Vector2 NeckAt = new(0.74f, 0.0754f);
        private const float HeadScale = 1.2f;      // the head grown about the neck, as an illustrator would draw it

        /// <summary>
        /// The tyrant's outline as a polygon in its own box (x and y from 0 to 1, y down), its eye in the same, and the box's height over its width.
        /// The game's own animal, grown from its seed and posed by the game's animator part way through a roar, seen from the side, cut to facets:
        /// coarse on the body and fine round the head so the teeth stay.
        /// </summary>
        private static (Vector2[] outline, Vector2 eye, float aspect) Tyrant()
        {
            lock (TyrantGate)
            {
                if (_tyrant is { } ready) return ready;
                Grid mask = Silhouette();
                (int x0, int x1, int y0, int y1) = mask.Bounds();
                float w = x1 - x0, h = y1 - y0;
                float headFrom = x0 + 0.86f * w;
                Vector2[] outline = Simplify(Trace(mask), p => p.X > headFrom ? 0.00136f * w : 0.00347f * w);

                var neck = new Vector2(x0 + NeckAt.X * w, y0 + NeckAt.Y * h);
                Vector2 Grown(Vector2 p, float share) => p + (p - neck) * ((HeadScale - 1f) * share);
                for (int i = 0; i < outline.Length; i++)
                {
                    float ramp = Math.Clamp((outline[i].X - (neck.X - 0.05f * w)) / (0.1f * w), 0f, 1f);
                    outline[i] = Grown(outline[i], ramp * ramp * (3f - 2f * ramp));
                }
                Vector2 eye = Grown(new Vector2(x0 + EyeAt.X * w, y0 + EyeAt.Y * h), 1f);

                for (int i = 0; i < outline.Length; i++) outline[i] = new Vector2((outline[i].X - x0) / w, (outline[i].Y - y0) / h);
                var result = (outline, new Vector2((eye.X - x0) / w, (eye.Y - y0) / h), h / w);
                _tyrant = result;
                return result;
            }
        }

        private sealed class FlatGround : IGround
        {
            public float HeightAt(float x, float z) => 0f;
        }

        /// <summary>The tyrant from the side, 0.93 s into its roar, as a mask about a thousand pixels long: the first animal of its kind, as the AnimLab's sheet shows it.</summary>
        private static Grid Silhouette()
        {
            SaurianSpec spec = SaurianSpec.Parse("dino:tyrant.0")!.Value;
            Mesh mesh = SaurianForge.Make(spec);
            var animator = new CharacterAnimator(SaurianForge.RigOf(spec), MotionStyle.Tyrant, 7);
            animator.Hold(Carry.Nothing, false);

            // The roar as the lab shows it (Dino AnimLab sheet --do roar): repeated with a gap, two cycles of warming up, and the sixth of twelve frames across the third.
            const float impact = 0.7f, length = 1.9f, gap = 0.3f, cycle = length + gap, warmup = cycle * 2f, step = 1f / 60f;
            const float at = warmup + cycle * 5f / 12f;
            var ground = new FlatGround();
            for (float time = step; ; time += step)
            {
                AnimationInput input = AnimationInput.At(Vector3.Zero, 0f);
                input.Alert = 1f;
                int serial = (int)MathF.Floor(time / cycle);
                float into = time - serial * cycle;
                if (into < length) input.Action = new ActionInput { Gesture = Gestures.Roar, Elapsed = into, Impact = impact, Length = length, Serial = serial };
                animator.Step(input, step, ground);
                if (time >= at) break;
            }

            // Every vertex where its bones put it.
            var vertices = mesh.meshVertices;
            var index = new Dictionary<MeshVertex, int>(ReferenceEqualityComparer.Instance as IEqualityComparer<MeshVertex>);
            var posed = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                MeshVertex vertex = vertices[i];
                index[vertex] = i;
                Vector2 weights = vertex.BoneWeights;
                float total = weights.X + weights.Y;
                weights = total > 0.001f ? weights / total : new Vector2(1f, 0f);
                Matrix4 skin = animator.Skinning[(int)vertex.boneIndices.X];
                if (weights.Y > 0f) skin = skin * weights.X + animator.Skinning[(int)vertex.boneIndices.Y] * weights.Y;
                posed[i] = Vector3.TransformPosition(vertex.position, skin);
            }

            // Seen from its left, the way it faces (+z) to the right of the picture and up up, about a thousand pixels long.
            float minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 p in posed)
            {
                minZ = MathF.Min(minZ, p.Z);
                maxZ = MathF.Max(maxZ, p.Z);
                minY = MathF.Min(minY, p.Y);
                maxY = MathF.Max(maxY, p.Y);
            }
            const int pad = 8;
            float pixels = 1000f / (maxZ - minZ);
            var grid = new Grid(1000 + 2 * pad, (int)MathF.Ceiling((maxY - minY) * pixels) + 2 * pad);
            Vector2 Screen(Vector3 p) => new Vector2((p.Z - minZ) * pixels + pad, (maxY - p.Y) * pixels + pad);
            var corners = new Vector2[3];
            foreach (Face face in mesh.faces)
            {
                corners[0] = Screen(posed[index[face.A]]);
                corners[1] = Screen(posed[index[face.B]]);
                corners[2] = Screen(posed[index[face.C]]);
                grid.Polygon(corners);
            }

            // The feet stand on the ground: nothing below it.
            int groundRow = (int)MathF.Floor((maxY - 0f) * pixels + pad);
            grid.ClearBelow(groundRow);
            grid.KeepLargest();
            grid.FillHoles();
            return grid;
        }

        /// <summary>The outer edge of a mask as an ordered walk round it (Moore neighbourhood).</summary>
        private static List<Vector2> Trace(Grid mask)
        {
            int w = mask.Width, h = mask.Height;
            (int sx, int sy) = mask.First();
            // Clockwise from the west.
            int[] dx = { -1, -1, 0, 1, 1, 1, 0, -1 };
            int[] dy = { 0, -1, -1, -1, 0, 1, 1, 1 };
            var pts = new List<Vector2> { new Vector2(sx, sy) };
            int px = sx, py = sy, back = 0;
            while (pts.Count < 200000)
            {
                bool found = false;
                for (int k = 0; k < 8; k++)
                {
                    int d = (back + k) % 8;
                    int qx = px + dx[d], qy = py + dy[d];
                    if (qx < 0 || qy < 0 || qx >= w || qy >= h || !mask[qx, qy]) continue;
                    back = (d + 5) % 8;
                    px = qx;
                    py = qy;
                    found = true;
                    break;
                }
                if (!found || (px == sx && py == sy)) break;
                pts.Add(new Vector2(px, py));
            }
            return pts;
        }

        /// <summary>Douglas and Peucker on a closed outline, the tolerance a function of where a point is.</summary>
        private static Vector2[] Simplify(List<Vector2> pts, Func<Vector2, float> toleranceAt)
        {
            int far = 0;
            float farthest = -1f;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - pts[0]).LengthSquared;
                if (d > farthest)
                {
                    farthest = d;
                    far = i;
                }
            }
            var keep = new List<int> { 0 };
            Keep(pts, 0, far, toleranceAt, keep);
            keep.Add(far);
            Keep(pts, far, pts.Count - 1, toleranceAt, keep);
            keep.Add(pts.Count - 1);
            var outline = new Vector2[keep.Count];
            for (int i = 0; i < keep.Count; i++) outline[i] = pts[keep[i]];
            return outline;
        }

        private static void Keep(List<Vector2> pts, int a, int b, Func<Vector2, float> toleranceAt, List<int> keep)
        {
            if (b - a < 2) return;
            Vector2 p = pts[a], d = pts[b] - p;
            float n = d.Length;
            int worst = -1;
            float most = 1f;
            for (int i = a + 1; i < b; i++)
            {
                Vector2 q = pts[i];
                float dist = n < 1e-6f ? (q - p).Length : MathF.Abs(d.X * (q.Y - p.Y) - d.Y * (q.X - p.X)) / n;
                float over = dist / toleranceAt(q);
                if (over > most)
                {
                    most = over;
                    worst = i;
                }
            }
            if (worst < 0) return;
            Keep(pts, a, worst, toleranceAt, keep);
            keep.Add(worst);
            Keep(pts, worst, b, toleranceAt, keep);
        }

        // ------------------------------------------------------------------------------------------------------------------------ the title

        /// <summary>
        /// DINO over DEFENDERS, cap being DEFENDERS' capital height in pixels: DEFENDERS red with a bone line and an ink edge, DINO in bone between
        /// two rules ending in diamonds, and the tagline, if asked for, under it in bone italic. Over nothing.
        /// </summary>
        public static Picture Title(int cap, bool tagline)
        {
            Faces();
            int k = cap <= 120 ? 4 : 2;
            float c = cap * k;
            Grid big = SetLine(_bold!, "DEFENDERS", c, -0.015f);
            Grid small = SetLine(_bold!, "DINO", c * 0.46f, 0.42f);
            int pad = (int)(c * 0.45f);
            int gap = (int)(c * 0.22f);
            int width = Math.Max(big.Width, small.Width) + 2 * pad;
            int height = pad + small.Height + gap + big.Height + pad;
            int sx = (width - small.Width) / 2;
            int by = pad + small.Height + gap;
            int bx = (width - big.Width) / 2;
            var smallMask = new Grid(width, height);
            smallMask.Lay(small, sx, pad);
            var bigMask = new Grid(width, height);
            bigMask.Lay(big, bx, by);

            // The rules either side of DINO, out to DEFENDERS' ends, each ending in a diamond.
            float ry = pad + small.Height * 0.56f;
            float rw = MathF.Max(1f, c * 0.03f);
            foreach (int side in new[] { -1, 1 })
            {
                float inner = side < 0 ? sx - c * 0.22f : sx + small.Width + c * 0.22f;
                float outer = side < 0 ? bx + c * 0.12f : bx + big.Width - c * 0.12f;
                float a = MathF.Min(inner, outer), b = MathF.Max(inner, outer);
                smallMask.Polygon(new[] { new Vector2(a, ry - rw / 2f), new Vector2(b, ry - rw / 2f), new Vector2(b, ry + rw / 2f), new Vector2(a, ry + rw / 2f) });
                float ds = c * 0.06f;
                smallMask.Polygon(new[] { new Vector2(outer, ry - ds), new Vector2(outer + ds, ry), new Vector2(outer, ry + ds), new Vector2(outer - ds, ry) });
            }

            var sheet = new Sheet(width, height, 1);
            sheet.Print(smallMask, Bone, 0f, c * 0.05f);
            sheet.Print(bigMask, Red, c * 0.022f, c * 0.075f);
            Picture title = sheet.Down(k);
            if (!tagline) return title;

            Grid tag = SetLine(_italic!, Tagline, c * 0.30f, 0f);
            var tagMask = new Grid(tag.Width + 2 * pad, tag.Height + 2 * pad);
            tagMask.Lay(tag, pad, pad);
            var tagSheet = new Sheet(tagMask.Width, tagMask.Height, 1);
            tagSheet.Print(tagMask, Bone, 0f, c * 0.035f);
            Picture line = tagSheet.Down(k);
            int below = (int)(c * 0.2f) / k;
            var both = new Picture(Math.Max(title.Width, line.Width), title.Height + below + line.Height, new byte[Math.Max(title.Width, line.Width) * (title.Height + below + line.Height) * 4]);
            both.Lay(title, (both.Width - title.Width) / 2, 0);
            both.Lay(line, (both.Width - line.Width) / 2, title.Height + below);
            return both;
        }

        /// <summary>One line of type as a mask cropped to its ink, cap being its capitals' height in pixels and tracking a share of an em between letters.</summary>
        private static Grid SetLine(FontFace face, string text, float cap, float tracking)
        {
            float capUnits = face.CapHeight > 0 ? face.CapHeight : face.UnitsPerEm * 0.7f;
            float scale = cap / capUnits;
            float track = tracking * face.UnitsPerEm * scale;
            var glyphs = new List<(GlyphBitmap bitmap, int x)>();
            float pen = 0f;
            int previous = 0;
            foreach (char ch in text)
            {
                int glyph = face.GetGlyphIndex(ch);
                if (previous != 0) pen += face.GetKerning(previous, glyph) * scale;
                if (ch != ' ')
                {
                    GlyphBitmap bitmap = GlyphRasterizer.Rasterize(face.GetGlyphOutline(glyph), scale);
                    if (!bitmap.IsEmpty) glyphs.Add((bitmap, (int)MathF.Round(pen)));
                }
                pen += face.GetAdvanceWidth(glyph) * scale + track;
                previous = glyph;
            }
            int baseline = (int)MathF.Ceiling(face.Ascender * scale) + 2;
            int width = (int)MathF.Ceiling(pen) + 4;
            int height = baseline + (int)MathF.Ceiling(-face.Descender * scale) + 4;
            var grid = new Grid(width, height);
            foreach ((GlyphBitmap bitmap, int x) in glyphs)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int ty = baseline + bitmap.OffsetY + y;
                    if (ty < 0 || ty >= height) continue;
                    for (int gx = 0; gx < bitmap.Width; gx++)
                    {
                        int tx = x + bitmap.OffsetX + gx;
                        if (tx < 0 || tx >= width) continue;
                        if (bitmap.Pixels[y * bitmap.Width + gx] > 127) grid[tx, ty] = true;
                    }
                }
            }
            return grid.Cropped();
        }

        // ------------------------------------------------------------------------------------------------------------------------ drawing flat

        /// <summary>A mask of pixels, y down.</summary>
        private sealed class Grid
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool[] Bits;

            public Grid(int width, int height)
            {
                Width = width;
                Height = height;
                Bits = new bool[width * height];
            }

            public bool this[int x, int y]
            {
                get => Bits[y * Width + x];
                set => Bits[y * Width + x] = value;
            }

            /// <summary>Fills a polygon, even and odd, by rows at their middles.</summary>
            public void Polygon(IReadOnlyList<Vector2> pts) => Scan(pts, Width, Height, (y, a, b) => { for (int x = a; x < b; x++) Bits[y * Width + x] = true; });

            public void Lay(Grid other, int atX, int atY)
            {
                for (int y = 0; y < other.Height; y++)
                {
                    for (int x = 0; x < other.Width; x++)
                    {
                        if (other.Bits[y * other.Width + x]) Bits[(atY + y) * Width + atX + x] = true;
                    }
                }
            }

            public (int x0, int x1, int y0, int y1) Bounds()
            {
                int x0 = Width, x1 = -1, y0 = Height, y1 = -1;
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (!Bits[y * Width + x]) continue;
                        x0 = Math.Min(x0, x);
                        x1 = Math.Max(x1, x);
                        y0 = Math.Min(y0, y);
                        y1 = Math.Max(y1, y);
                    }
                }
                return (x0, x1, y0, y1);
            }

            public Grid Cropped()
            {
                (int x0, int x1, int y0, int y1) = Bounds();
                if (x1 < 0) return new Grid(1, 1);
                var grid = new Grid(x1 - x0 + 1, y1 - y0 + 1);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++) grid.Bits[(y - y0) * grid.Width + x - x0] = Bits[y * Width + x];
                }
                return grid;
            }

            /// <summary>The topmost, leftmost pixel that's set.</summary>
            public (int x, int y) First()
            {
                for (int i = 0; i < Bits.Length; i++)
                {
                    if (Bits[i]) return (i % Width, i / Width);
                }
                return (0, 0);
            }

            public void ClearBelow(int row)
            {
                for (int y = Math.Max(0, row); y < Height; y++) Array.Fill(Bits, false, y * Width, Width);
            }

            /// <summary>Keeps only the largest piece, 4-connected.</summary>
            public void KeepLargest()
            {
                var label = new int[Bits.Length];
                var stack = new Stack<int>();
                int best = 0, bestSize = 0, count = 0;
                for (int seed = 0; seed < Bits.Length; seed++)
                {
                    if (!Bits[seed] || label[seed] != 0) continue;
                    int id = ++count, size = 0;
                    stack.Push(seed);
                    label[seed] = id;
                    while (stack.Count > 0)
                    {
                        int i = stack.Pop();
                        size++;
                        int x = i % Width, y = i / Width;
                        if (x > 0 && Bits[i - 1] && label[i - 1] == 0) { label[i - 1] = id; stack.Push(i - 1); }
                        if (x < Width - 1 && Bits[i + 1] && label[i + 1] == 0) { label[i + 1] = id; stack.Push(i + 1); }
                        if (y > 0 && Bits[i - Width] && label[i - Width] == 0) { label[i - Width] = id; stack.Push(i - Width); }
                        if (y < Height - 1 && Bits[i + Width] && label[i + Width] == 0) { label[i + Width] = id; stack.Push(i + Width); }
                    }
                    if (size > bestSize)
                    {
                        bestSize = size;
                        best = id;
                    }
                }
                for (int i = 0; i < Bits.Length; i++) Bits[i] = label[i] == best;
            }

            /// <summary>Fills whatever the outside can't reach.</summary>
            public void FillHoles()
            {
                var outside = new bool[Bits.Length];
                var stack = new Stack<int>();
                void Seed(int i)
                {
                    if (Bits[i] || outside[i]) return;
                    outside[i] = true;
                    stack.Push(i);
                }
                for (int x = 0; x < Width; x++) { Seed(x); Seed((Height - 1) * Width + x); }
                for (int y = 0; y < Height; y++) { Seed(y * Width); Seed(y * Width + Width - 1); }
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % Width, y = i / Width;
                    if (x > 0) Seed(i - 1);
                    if (x < Width - 1) Seed(i + 1);
                    if (y > 0) Seed(i - Width);
                    if (y < Height - 1) Seed(i + Width);
                }
                for (int i = 0; i < Bits.Length; i++) Bits[i] = !outside[i];
            }
        }

        /// <summary>Rows of a polygon, even and odd, sampled at each row's middle: (row, from, to) with to left out.</summary>
        private static void Scan(IReadOnlyList<Vector2> pts, int width, int height, Action<int, int, int> row)
        {
            int n = pts.Count;
            if (n < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 p in pts)
            {
                minY = MathF.Min(minY, p.Y);
                maxY = MathF.Max(maxY, p.Y);
            }
            int y0 = Math.Max(0, (int)MathF.Floor(minY)), y1 = Math.Min(height - 1, (int)MathF.Ceiling(maxY));
            var crossings = new float[n];
            for (int y = y0; y <= y1; y++)
            {
                float yc = y + 0.5f;
                int count = 0;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % n];
                    if ((a.Y <= yc) == (b.Y <= yc)) continue;
                    crossings[count++] = a.X + (yc - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                }
                if (count < 2) continue;
                Array.Sort(crossings, 0, count);
                for (int i = 0; i + 1 < count; i += 2)
                {
                    int from = Math.Max(0, (int)MathF.Round(crossings[i]));
                    int to = Math.Min(width, (int)MathF.Round(crossings[i + 1]));
                    if (to > from) row(y, from, to);
                }
            }
        }

        /// <summary>
        /// Flat colour laid down in layers at some times the size, as a print is cut, and brought down to size at the end, which is all the softening its
        /// edges get. Keeps what's been painted, so what hasn't is clear.
        /// </summary>
        private sealed class Sheet
        {
            private readonly int _w;
            private readonly int _h;
            private readonly int _k;
            private readonly byte[] _rgb;
            private readonly bool[] _painted;
            private Vector2 _clipCentre;
            private float _clipRadius = -1f;

            public Sheet(int width, int height, int times)
            {
                _k = times;
                _w = width * times;
                _h = height * times;
                _rgb = new byte[_w * _h * 3];
                _painted = new bool[_w * _h];
            }

            public void Clip(Vector2 centre, float radius)
            {
                _clipCentre = centre * _k;
                _clipRadius = radius * _k;
            }

            public void Unclip() => _clipRadius = -1f;

            private void Paint(int i, Rgb colour)
            {
                _rgb[i * 3] = colour.R;
                _rgb[i * 3 + 1] = colour.G;
                _rgb[i * 3 + 2] = colour.B;
                _painted[i] = true;
            }

            private bool Allowed(int x, int y)
            {
                if (_clipRadius < 0f) return true;
                float dx = x + 0.5f - _clipCentre.X, dy = y + 0.5f - _clipCentre.Y;
                return dx * dx + dy * dy <= _clipRadius * _clipRadius;
            }

            public void Fill(Rgb colour)
            {
                for (int i = 0; i < _painted.Length; i++) Paint(i, colour);
            }

            public void Polygon(IReadOnlyList<Vector2> pts, Rgb colour)
            {
                var scaled = new Vector2[pts.Count];
                for (int i = 0; i < pts.Count; i++) scaled[i] = pts[i] * _k;
                Scan(scaled, _w, _h, (y, a, b) =>
                {
                    for (int x = a; x < b; x++)
                    {
                        if (Allowed(x, y)) Paint(y * _w + x, colour);
                    }
                });
            }

            public void Disc(Vector2 centre, float radius, Rgb colour) => Annulus(centre, 0f, radius, colour);

            public void Ring(Vector2 centre, float radius, float width, Rgb colour) => Annulus(centre, radius - width / 2f, radius + width / 2f, colour);

            private void Annulus(Vector2 centre, float inner, float outer, Rgb colour)
            {
                Vector2 c = centre * _k;
                float r0 = inner * _k, r1 = outer * _k;
                int x0 = Math.Max(0, (int)MathF.Floor(c.X - r1)), x1 = Math.Min(_w - 1, (int)MathF.Ceiling(c.X + r1));
                int y0 = Math.Max(0, (int)MathF.Floor(c.Y - r1)), y1 = Math.Min(_h - 1, (int)MathF.Ceiling(c.Y + r1));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - c.X, dy = y + 0.5f - c.Y;
                        float d2 = dx * dx + dy * dy;
                        if (d2 <= r1 * r1 && d2 >= r0 * r0 && Allowed(x, y)) Paint(y * _w + x, colour);
                    }
                }
            }

            /// <summary>A shape printed the mark's way, from a mask the sheet's own size: ink out to edge, a bone line out to line, and its fill inside.</summary>
            public void Print(Grid mask, Rgb fill, float line, float edge)
            {
                float[] distance = Distance(mask);
                for (int i = 0; i < _painted.Length; i++)
                {
                    float d = distance[i];
                    if (mask.Bits[i]) Paint(i, fill);
                    else if (d <= line) Paint(i, Bone);
                    else if (d <= edge) Paint(i, Ink);
                }
            }

            /// <summary>How far every pixel is from the mask, in pixels: Felzenszwalb and Huttenlocher's transform, in each direction in turn.</summary>
            private static float[] Distance(Grid mask)
            {
                const float Inf = 1e20f;
                int w = mask.Width, h = mask.Height;
                var f = new float[w * h];
                for (int i = 0; i < f.Length; i++) f[i] = mask.Bits[i] ? 0f : Inf;
                int longest = Math.Max(w, h);
                var line = new float[longest];
                var d = new float[longest];
                var v = new int[longest];
                var z = new float[longest + 1];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++) line[y] = f[y * w + x];
                    Transform(line, h, d, v, z);
                    for (int y = 0; y < h; y++) f[y * w + x] = d[y];
                }
                for (int y = 0; y < h; y++)
                {
                    Array.Copy(f, y * w, line, 0, w);
                    Transform(line, w, d, v, z);
                    Array.Copy(d, 0, f, y * w, w);
                }
                for (int i = 0; i < f.Length; i++) f[i] = MathF.Sqrt(f[i]);
                return f;
            }

            private static void Transform(float[] f, int n, float[] d, int[] v, float[] z)
            {
                const float Inf = 1e20f;
                int k = 0;
                v[0] = 0;
                z[0] = -Inf;
                z[1] = Inf;
                for (int q = 1; q < n; q++)
                {
                    float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                    while (s <= z[k])
                    {
                        k--;
                        s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                    }
                    k++;
                    v[k] = q;
                    z[k] = s;
                    z[k + 1] = Inf;
                }
                k = 0;
                for (int q = 0; q < n; q++)
                {
                    while (z[k + 1] < q) k++;
                    d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
                }
            }

            /// <summary>Brought down to size, a box at a time: the colour of what was painted in it, and how much of it was.</summary>
            public Picture Down() => Down(_k);

            public Picture Down(int times)
            {
                int width = _w / times, height = _h / times;
                var rgba = new byte[width * height * 4];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int r = 0, g = 0, b = 0, count = 0;
                        for (int sy = 0; sy < times; sy++)
                        {
                            int row = (y * times + sy) * _w + x * times;
                            for (int sx = 0; sx < times; sx++)
                            {
                                int i = row + sx;
                                if (!_painted[i]) continue;
                                r += _rgb[i * 3];
                                g += _rgb[i * 3 + 1];
                                b += _rgb[i * 3 + 2];
                                count++;
                            }
                        }
                        int o = (y * width + x) * 4;
                        if (count == 0) continue;
                        rgba[o] = (byte)(r / count);
                        rgba[o + 1] = (byte)(g / count);
                        rgba[o + 2] = (byte)(b / count);
                        rgba[o + 3] = (byte)(count * 255 / (times * times));
                    }
                }
                return new Picture(width, height, rgba);
            }
        }
    }
}
