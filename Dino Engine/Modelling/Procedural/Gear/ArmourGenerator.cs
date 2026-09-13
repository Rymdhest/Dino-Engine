using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    public class ArmourGenerator
    {

        public static Mesh GenerateShield()
        {
            VertexMaterial ironMaterial = new VertexMaterial(TextureGenerator.iron);
            VertexMaterial wodMaterial = new VertexMaterial(TextureGenerator.oakBark);

            Mesh shield = IcoSphereGenerator.CreateIcosphere(2, wodMaterial);
            float shieldThickness = 0.1f;
            float shieldRadius = 0.7f;
            shield.scale(new Vector3(shieldRadius));
            for (int i = 0; i<shield.meshVertices.Count; i++)
            {
                Vector3 pos = shield.meshVertices[i].position;
                pos.Z = MyMath.clamp(pos.Z, -shieldThickness*0.5f, shieldThickness*0.5f);
                shield.meshVertices[i].position = pos;
            }
            shield.makeFlat();

            Mesh nob = IcoSphereGenerator.CreateIcosphere(1, ironMaterial);

            shield += nob.scaled(new Vector3(new Vector3(0.16f, 0.16f, shieldThickness*0.5f))).translated(new Vector3(0f, 0f, shieldThickness*0.5f));
            shield += nob.scaled(new Vector3(new Vector3(0.1f, 0.1f, shieldThickness * 1.5f))).translated(new Vector3(0f, 0f, shieldThickness * 0.5f));

            int numNobs = 20;
            for (int i = 0; i<numNobs; i++)
            {
                float t = i / (numNobs - 1.0f);
                float x = MathF.Sin(t * MathF.Tau) * (shieldRadius*0.9f);
                float y = MathF.Cos(t * MathF.Tau) * (shieldRadius * 0.9f);
                float z = shieldThickness*0.5f;

                shield += nob.scaled(new Vector3(new Vector3(0.03f, 0.03f, 0.03f))).translated(new Vector3(x, y, z));

            }

            shield.translate(new Vector3(0f, 0f, shieldThickness*0.5f));

            return shield;
        }
    }
}
