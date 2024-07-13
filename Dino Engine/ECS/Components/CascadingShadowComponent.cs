using Dino_Engine.Rendering;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class CascadingShadowComponent : Component
    {

        public class ShadowCascade
        {
            private Matrix4 cascadeProjectionMatrix;
            private FrameBuffer cascadeFrameBuffer;
            private float projectionSize;
            private float polygonOffset;
            public ShadowCascade(Vector2i resolution, float projectionSize, float polygonOffset = 7f)
            {
                this.projectionSize = projectionSize;
                this.polygonOffset = polygonOffset;
                FrameBufferSettings settings = new FrameBufferSettings(resolution);
                DepthAttachmentSettings depthAttachmentSettings = new DepthAttachmentSettings();
                depthAttachmentSettings.isTexture = true;
                depthAttachmentSettings.isShadowDepthTexture = true;
                settings.depthAttachmentSettings = depthAttachmentSettings;
                cascadeFrameBuffer = new FrameBuffer(settings);

                cascadeProjectionMatrix = Matrix4.CreateOrthographic(projectionSize, projectionSize, -projectionSize, projectionSize);
            }

            public void CleanUp()
            {
                cascadeFrameBuffer.cleanUp();
            }

            public float getPolygonOffset()
            {
                return polygonOffset;
            }

            public Vector2i getResolution()
            {
                return cascadeFrameBuffer.getResolution();
            }

            public int getDepthTexture()
            {
                return cascadeFrameBuffer.getDepthAttachment();
            }

            public Matrix4 getProjectionMatrix()
            {
                return cascadeProjectionMatrix;
            }

            public void bindFrameBuffer()
            {
                cascadeFrameBuffer.bind();
            }
            public float getProjectionSize()
            {
                return projectionSize;
            }
            public override string ToString()
            {
                return $"Projection Matrix:\n" +
                    $"{cascadeProjectionMatrix}\n"+
                    $"Framebuffer: {cascadeFrameBuffer}\n" +
                    $"Size: {projectionSize}\n" +
                    $"Polygon Offset: {polygonOffset}";
            }
        }


        private List<ShadowCascade> _cascades = new List<ShadowCascade>();
        Matrix4 _lightViewMatrix = Matrix4.Identity;
        internal List<ShadowCascade> Cascades { get => _cascades;}
        public Matrix4 LightViewMatrix { get => _lightViewMatrix; set => _lightViewMatrix = value; }

        public CascadingShadowComponent(Vector2i resolution, int numCascades, float size)
        {
            for (int i = 0; i<numCascades; i++)
            {
                Cascades.Add(new ShadowCascade(resolution, size));
                size /= 3.0f;
            }
            _cascades.Sort((p1, p2) => p1.getProjectionSize().CompareTo(p2.getProjectionSize()));
        }

        public override void CleanUp()
        {
            foreach(ShadowCascade cascade in Cascades)
            {
                cascade.CleanUp();
            }
        }
    }
}
