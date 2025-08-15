using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Reflection.Emit;
using static Dino_Engine.Textures.MaterialLayersManipulator;

namespace Dino_Engine.Textures
{
    public class MaterialLayer
    {
        private FrameBuffer _materialBuffer1;
        private FrameBuffer _materialBuffer2;
        private bool _toggle = true;
        public static ProceduralTextureRenderer procTextGen;
        public static MaterialLayersManipulator MaterialLayersCombiner;

        public MaterialLayer(bool bind = true)
        {

            DrawBufferSettings albedoAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            albedoAttachment.formatInternal = PixelInternalFormat.Rgba8;
            albedoAttachment.pixelType = PixelType.UnsignedByte;
            albedoAttachment.formatExternal = PixelFormat.Rgba;
            albedoAttachment.wrapMode = TextureWrapMode.Repeat;
            albedoAttachment.minFilterType = TextureMinFilter.Linear;

            DrawBufferSettings materialAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1);
            materialAttachment.formatInternal = PixelInternalFormat.Rgba8;
            materialAttachment.pixelType = PixelType.UnsignedByte;
            materialAttachment.formatExternal = PixelFormat.Rgba;
            materialAttachment.wrapMode = TextureWrapMode.Repeat;
            materialAttachment.minFilterType = TextureMinFilter.Linear;

            DrawBufferSettings heightAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment2);
            heightAttachment.formatInternal = PixelInternalFormat.R8;
            heightAttachment.pixelType = PixelType.UnsignedByte;
            heightAttachment.formatExternal = PixelFormat.Red;
            heightAttachment.wrapMode = TextureWrapMode.Repeat;
            heightAttachment.minFilterType = TextureMinFilter.Linear;

            FrameBufferSettings materialSettings = new FrameBufferSettings(TextureGenerator.TEXTURE_RESOLUTION);
            materialSettings.drawBuffers.Add(albedoAttachment);
            materialSettings.drawBuffers.Add(materialAttachment);
            materialSettings.drawBuffers.Add(heightAttachment);

            _materialBuffer1 = new FrameBuffer(materialSettings);
            _materialBuffer2 = new FrameBuffer(materialSettings);

            TextureGenerator.MaterialLayerHandler.addLayer(this);

            if (bind) GetNextFrameBuffer().bind();
        }

        public MaterialLayer tap()
        {
            GetNextFrameBuffer().bind();
            Engine.RenderEngine.ScreenQuadRenderer.Render();
            StepToggle();
            return this;
        }

        public MaterialLayer setToDebugColour()
        {

            var materialBase = procTextGen.CreateMaterial(new Material(new Colour(0.0f, 0.0f, 0.0f), 0.35f, 0f, 0.0f, 0.0f), height: 0.5f);
            var materialMiddle = procTextGen.CreateMaterial(new Material(new Colour(1.0f, 1.0f, 1.0f), 0.35f, 0f, 0.0f, 0.0f), height: 0.5f);
            var materialTop = procTextGen.CreateMaterial(new Material(new Colour(0.0f, 0.0f, 1.0f), 0.35f, 0f, 0.0f, 0.0f), height: 0.9f);
            var materialBot = procTextGen.CreateMaterial(new Material(new Colour(1.0f, 0.0f, 0.0f), 0.35f, 0f, 0.0f, 0.0f), height: 0.1f);
            TextureGenerator.MaterialLayersCombiner.combine(this, materialBase, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Override, smoothness: 0.0f);
            TextureGenerator.MaterialLayersCombiner.combine(this, materialMiddle, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, smoothness: 0.02f);
            TextureGenerator.MaterialLayersCombiner.combine(this, materialTop, FilterMode.Lesser, heightOperation: Operation.Nothing, materialOperation: Operation.Override, smoothness: 0.0f);
            TextureGenerator.MaterialLayersCombiner.combine(this, materialBot, FilterMode.Greater, heightOperation: Operation.Nothing, materialOperation: Operation.Override, smoothness: 0.0f);
            return this;
        }
        public MaterialLayer setMaterial( Material material)
        {
            var newMaterialLayer = procTextGen.CreateMaterial(material);
            MaterialLayersCombiner.combine(this, newMaterialLayer, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Override);
            newMaterialLayer.destroy();
            return this;
        }
        public MaterialLayer addHeight(float amount)
        {
            var flatHeight = procTextGen.CreateFlatHeight(height: amount);
            MaterialLayersCombiner.combine(this, flatHeight, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight:-1.0f);
            flatHeight.destroy();
            return this;
        }
        public MaterialLayer scaleHeight(float factor)
        {
            var flatHeight = procTextGen.CreateFlatHeight(height: factor);
            MaterialLayersCombiner.combine(this, flatHeight, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: -1.0f);
            flatHeight.destroy();
            return this;
        }

        public MaterialLayer invertHeight()
        {
            var flatHeight1 = procTextGen.CreateFlatHeight(height: 1.0f);
            MaterialLayersCombiner.combine(flatHeight1, this, FilterMode.Everywhere, heightOperation: Operation.Subtract, materialOperation: Operation.Override);
            MaterialLayersCombiner.combine(this, flatHeight1, FilterMode.Everywhere, heightOperation: Operation.Override, materialOperation: Operation.Override);
            flatHeight1.destroy();
            return this;
        }

        public MaterialLayer mix(MaterialLayer readLayer, FilterMode filterMode, Operation materialOperation = Operation.SameAsOther, Operation heightOperation = Operation.SameAsOther, float weight = 0.5f, float smoothness = 0f)
        {
            var combined = MaterialLayersCombiner.combine(this, readLayer, filterMode: filterMode, heightOperation: heightOperation, materialOperation: materialOperation, weight: weight, smoothness:smoothness);

            return combined;
        }
        public MaterialLayer Paste(MaterialLayer readLayer, FilterMode filterMode = FilterMode.Everywhere, Operation materialOperation = Operation.Override, Operation heightOperation = Operation.Add, float weight = 0.5f, float smoothness = 0f)
        {
            var combined = MaterialLayersCombiner.combine(this, readLayer, filterMode: filterMode, heightOperation: heightOperation, materialOperation: materialOperation, weight: weight, smoothness: smoothness);

            return combined;
        }

        public void CleanUp()
        {
            _materialBuffer1.cleanUp();
            _materialBuffer2.cleanUp();
        }

        public void destroy()
        {

            TextureGenerator.MaterialLayerHandler.removeLayer(this);
            CleanUp();
        }

        public FrameBuffer GetNextFrameBuffer()
        {
            if (_toggle) return _materialBuffer1;
            else return _materialBuffer2;
        }
        public FrameBuffer GetLastFrameBuffer()
        {
            if (_toggle) return _materialBuffer2;
            else return _materialBuffer1;
        }
        public void StepToggle()
        {
            if (_toggle == true) _toggle = false;
            else _toggle = true;
        }


    }
}
