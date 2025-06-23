
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Rendering
{
    public class DepthAttachmentSettings
    {
        public bool isTexture = false;
        public bool isShadowDepthTexture = false;
        public PixelInternalFormat precision = PixelInternalFormat.DepthComponent32;
        public DepthAttachmentSettings() {
        }
    }
}
