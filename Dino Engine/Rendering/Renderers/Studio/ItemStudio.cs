using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Studio
{
    /// <summary>Where a picture is: which texture, and which part of it. Anything that shows one should ask for it every time, as it can be painted over once nobody has for a while.</summary>
    public readonly record struct StudioPicture(int Texture, Vector2 From, Vector2 To);

    /// <summary>
    /// Takes pictures of models and keeps them, as the imposters' studio does, for icons. It's the same mesh as the world's and the same materials, so a picture can't disagree with the
    /// thing, under lights of its own (<c>Shaders/Studio/</c>). Pictures are drawn a few at the start of a frame and kept in one atlas, the oldest giving way when that fills up, so the
    /// interface can ask for whatever it likes every frame and draw its slot glyph until there's something to show.
    /// </summary>
    public sealed class ItemStudio
    {
        public const int Cell = 128;

        private const int Shrink = 4;
        private const int AtlasCells = 16;
        private const int MostAFrame = 2;
        private const float Margin = 0.1f;
        private const float OutlineWidth = 5f;

        private sealed record Request(ulong Key, Func<glModel?> Model, GearDisplay.Angle Angle, Colour Halo);

        private readonly ShaderProgram _itemShader = new ShaderProgram("Studio/Item.vert", "Studio/Item.frag");
        private readonly ShaderProgram _finishShader = new ShaderProgram("Simple.vert", "Studio/Item_Finish.frag");
        private readonly FrameBuffer _stage;
        private readonly FrameBuffer _atlas;

        private readonly Dictionary<ulong, int> _cells = new();
        private readonly ulong[] _keys = new ulong[AtlasCells * AtlasCells];
        private readonly long[] _lastAsked = new long[AtlasCells * AtlasCells];
        private readonly List<Request> _waiting = new();
        private readonly HashSet<ulong> _queued = new();
        private long _frame;

        public ItemStudio()
        {
            var stage = new FrameBufferSettings(new Vector2i(Cell * Shrink));
            stage.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0) { formatInternal = PixelInternalFormat.Rgba16f, pixelType = PixelType.Float });
            stage.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment1));
            stage.depthAttachmentSettings = new DepthAttachmentSettings { isTexture = true };
            _stage = new FrameBuffer(stage);

            var atlas = new FrameBufferSettings(new Vector2i(Cell * AtlasCells));
            atlas.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0));
            _atlas = new FrameBuffer(atlas);
            _atlas.bind();
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            _atlas.unbind();

            _itemShader.bind();
            _itemShader.loadUniformInt("albedoMapTextureArray", 0);
            _itemShader.loadUniformInt("normalMapTextureArray", 1);
            _itemShader.loadUniformInt("materialMapTextureArray", 2);
            _itemShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _itemShader.loadUniformInt("normalMapModelTextureArray", 4);
            _itemShader.loadUniformInt("materialMapModelTextureArray", 5);
            _itemShader.unBind();

            _finishShader.bind();
            _finishShader.loadUniformInt("colourMap", 0);
            _finishShader.loadUniformInt("normalMap", 1);
            _finishShader.loadUniformInt("depthMap", 2);
            _finishShader.unBind();
        }

        /// <summary>The picture of something, or nothing until it's been taken.</summary>
        /// <param name="key">What it's a picture of. Two of the same are one picture.</param>
        /// <param name="model">The model, or nothing if there isn't one yet. It's asked again until there is.</param>
        /// <param name="halo">What glows behind it, if anything does.</param>
        public StudioPicture? Picture(ulong key, Func<glModel?> model, GearDisplay.Angle angle, Colour halo)
        {
            if (_cells.TryGetValue(key, out int cell))
            {
                _lastAsked[cell] = _frame;
                int column = cell % AtlasCells, row = cell / AtlasCells;
                float size = 1f / AtlasCells;

                // A framebuffer's rows run upwards, and the interface's run down.
                return new StudioPicture(_atlas.GetAttachment(0), new Vector2(column * size, (row + 1) * size), new Vector2((column + 1) * size, row * size));
            }

            if (_queued.Add(key)) _waiting.Add(new Request(key, model, angle, halo));
            return null;
        }

        /// <summary>Runs at the start of a frame, before anything else is drawn, and leaves things as they were.</summary>
        public void RenderPass()
        {
            _frame++;
            int taken = 0;
            for (int i = 0; i < _waiting.Count && taken < MostAFrame; i++)
            {
                Request request = _waiting[i];
                if (request.Model() is not glModel model) continue;
                Take(request, model);
                _queued.Remove(request.Key);
                _waiting.RemoveAt(i--);
                taken++;
            }
        }

        private void Take(Request request, glModel model)
        {
            int cell = FreeCell();
            Matrix4 view = Matrix4.CreateRotationX(request.Angle.Rise) * Matrix4.CreateRotationY(request.Angle.Yaw) * Matrix4.CreateRotationX(request.Angle.Pitch) * Matrix4.CreateRotationZ(request.Angle.Roll);
            Frame(model.box, ref view, out Matrix4 projection);

            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);

            _stage.bind();
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            var textures = Engine.RenderEngine.textureGenerator;
            _itemShader.bind();
            _itemShader.loadUniformInt("numberOfMaterials", textures.loadedMaterialTextures);
            _itemShader.loadUniformMatrix4f("viewMatrix", view);
            _itemShader.loadUniformMatrix4f("projectionMatrix", projection);
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2DArray, textures.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2DArray, textures.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2DArray, textures.megaMaterialTextureArray);
            GL.ActiveTexture(TextureUnit.Texture3); GL.BindTexture(TextureTarget.Texture2DArray, textures.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4); GL.BindTexture(TextureTarget.Texture2DArray, textures.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5); GL.BindTexture(TextureTarget.Texture2DArray, textures.megaMaterialModelTextureArray);

            GL.BindVertexArray(model.getVAOID());
            for (int i = 0; i <= 5; i++) GL.EnableVertexAttribArray(i);
            GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            for (int i = 0; i <= 5; i++) GL.DisableVertexAttribArray(i);
            GL.BindVertexArray(0);

            Finish(cell, request.Halo);
            _keys[cell] = request.Key;
            _cells[request.Key] = cell;
            _lastAsked[cell] = _frame;
        }

        // Looks at what's there, and fits the camera to what can be seen from here.
        private static void Frame(AABB box, ref Matrix4 view, out Matrix4 projection)
        {
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? box.Min.X : box.Max.X, (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 4) == 0 ? box.Min.Z : box.Max.Z);
                Vector3 seen = Vector3.TransformPosition(corner, view);
                min = Vector3.ComponentMin(min, seen);
                max = Vector3.ComponentMax(max, seen);
            }

            Vector3 middle = (min + max) * 0.5f;
            view *= Matrix4.CreateTranslation(-middle.X, -middle.Y, -max.Z - 1f);

            float half = MathF.Max(max.X - min.X, max.Y - min.Y) * 0.5f * (1f + Margin * 2f);
            projection = Matrix4.CreateOrthographicOffCenter(-half, half, -half, half, 0.01f, max.Z - min.Z + 2f);
        }

        private void Finish(int cell, Colour halo)
        {
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);

            _atlas.bind();
            GL.Enable(EnableCap.ScissorTest);
            int x = cell % AtlasCells * Cell, y = cell / AtlasCells * Cell;
            GL.Scissor(x, y, Cell, Cell);
            GL.Viewport(x, y, Cell, Cell);

            _finishShader.bind();
            _finishShader.loadUniformVector2f("texel", new Vector2(1f / (Cell * Shrink)));
            _finishShader.loadUniformInt("shrink", Shrink);
            _finishShader.loadUniformFloat("outlineWidth", OutlineWidth);
            _finishShader.loadUniformVector4f("halo", new Vector4(halo.Red, halo.Green, halo.Blue, halo.Alpha));

            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, _stage.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2D, _stage.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2D, _stage.getDepthAttachment());
            Engine.RenderEngine.ScreenQuadRenderer.DrawQuad();

            GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, 0);
            _finishShader.unBind();

            GL.Disable(EnableCap.ScissorTest);
            GL.DepthMask(true);
            _atlas.unbind();
        }

        private int FreeCell()
        {
            int oldest = 0;
            for (int i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] == 0) return i;
                if (_lastAsked[i] < _lastAsked[oldest]) oldest = i;
            }
            _cells.Remove(_keys[oldest]);
            return oldest;
        }

        public void CleanUp()
        {
            _itemShader.cleanUp();
            _finishShader.cleanUp();
            _stage.cleanUp();
            _atlas.cleanUp();
        }
    }
}
