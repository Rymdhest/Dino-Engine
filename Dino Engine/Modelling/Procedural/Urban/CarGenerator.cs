using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Procedural.Urban
{
    public class CarGenerator
    {
        public static Mesh GenerateCar(out Vector3 leftLightPos, out Vector3 rightLightPos, out Vector3 exhaustPos)
        {
            Vector3 rngColor = MyMath.rng3D();

            Material carMaterial = new Material(new Colour(rngColor), 8);
            Material windowMaterial = new Material(new Colour(25, 36, 89, 1.0f), 3);
            Material detailMaterial = new Material(new Colour(110, 110, 110, 1.0f), 0);
            Material rubberMaterial = new Material(new Colour(10, 10, 10, 1.0f), 0);
            Material redLightMaterial = new Material(new Colour(230, 25, 25, 1.0f), Material.GLOW.materialIndex);
            Material frontLightMaterial = new Material(new Colour(230, 230, 230, 1.0f), Material.GLOW.materialIndex);

            float w = 1.0f;
            float h = 1.0f;
            float l = 4.0f;

            float windowSize = 0.8f;
            float windowDepth = 0.03f;
            float wheelRadius = 0.25f;
            float wheelWidth = 0.2f;
            float botWheelIndent = 0.35f;

            float exhaustLength = 0.1f;
            float exhaustRadius = 0.05f;

            int topStart = 5;

            List<Vector3> botShape = new List<Vector3>()
            {
                new Vector3(w*0.9f, 0.1f, 0.0f*l),
                new Vector3(w, 0, 0.22f*l),
                new Vector3(w, 0.2f, 0.26f*l),
                new Vector3(w, botWheelIndent, 0.3f*l),
                new Vector3(w, 0.2f, 0.34f*l),
                new Vector3(w*0.95f, 0, 0.38f*l),
                new Vector3(w*0.95f, 0, 0.6f*l),
                new Vector3(w*0.95f, 0, 0.82f*l),
                new Vector3(w, 0.2f, 0.86f*l),
                new Vector3(w, botWheelIndent, 0.9f*l),
                new Vector3(w, 0.2f, 0.94f*l),
                new Vector3(w, 0, 0.98f*l),
                new Vector3(w*0.8f, 0.1f, 1.1f*l)
            };

            List<Vector3> middleShape = new List<Vector3>()
            {
                new Vector3(w*0.95f, h*0.4f, botShape[0].Z),
                new Vector3(w*0.976f, h*0.43f, botShape[1].Z),
                new Vector3(w, h*0.45f, botShape[2].Z),
                new Vector3(w, h*0.5f, botShape[3].Z),
                new Vector3(w, h*0.5f, botShape[4].Z),
                new Vector3(w, h*0.5f, botShape[5].Z+0.5f),
                new Vector3(w, h*0.5f, botShape[6].Z+0.25f),
                new Vector3(w, h*0.5f, botShape[7].Z+0.025f),
                new Vector3(w, h*0.5f, botShape[8].Z),
                new Vector3(w, h*0.5f, botShape[9].Z),
                new Vector3(w, h*0.5f, botShape[10].Z),
                new Vector3(w, h*0.45f, botShape[11].Z),
                new Vector3(w*0.75f, h*0.45f, botShape[12].Z)
            };

            List<Vector3> topShape = new List<Vector3>()
            {
                new Vector3(w*0.75f, h*1.0f, middleShape[topStart-1].Z+0.5f),
                new Vector3(w*0.75f, h*1.0f, middleShape[topStart+0].Z+0.0f),
                new Vector3(w*0.75f, h*1.0f, middleShape[topStart+1].Z+0.0f),
                new Vector3(w*0.75f, h*0.9f, middleShape[topStart+2].Z-0.3f)
            };

            //Mesh car = MeshGenerator.generateShape(botShape, middleShape, carMaterial, true);
            Mesh car = new Mesh();
            int backStart = topStart + topShape.Count-2;

            Mesh botHalf = MeshGenerator.generateShape(botShape, middleShape,carMaterial, true);
            Mesh topHalf = MeshGenerator.generateExtrudedShape(middleShape.Slice(topStart-1, topShape.Count), topShape, carMaterial, windowMaterial, -windowDepth, windowSize, true);
            Mesh fronTop = MeshGenerator.generateShape(middleShape.Slice(0, topStart), carMaterial, UVTop: true);
            Mesh roof = MeshGenerator.generateShape(topShape, carMaterial, UVTop:true);
            Mesh backTop = MeshGenerator.generateShape(middleShape.Slice(backStart, middleShape.Count- backStart), carMaterial, UVTop:true);

            Vector3 p1 = middleShape[topStart-1];
            Vector3 p2 = middleShape[topStart-1] * new Vector3(-1f, 1f, 1f);
            Vector3 p3 = topShape[0] * new Vector3(-1f, 1f, 1f);
            Vector3 p4 = topShape[0];

            Mesh frontWindow = MeshGenerator.ExtrudedPlane(new MeshGenerator.Quad(p2, p1, p4, p3), -windowDepth, 0.915f, carMaterial, windowMaterial);

            Vector3 p5 = middleShape[topStart + topShape.Count-2];
            Vector3 p6 = middleShape[topStart  + topShape.Count-2] * new Vector3(-1f, 1f, 1f);
            Vector3 p7 = topShape[topShape.Count-1] * new Vector3(-1f, 1f, 1f);
            Vector3 p8 = topShape[topShape.Count-1];

            Mesh backWindow = MeshGenerator.ExtrudedPlane(new MeshGenerator.Quad(p7, p8, p5, p6), -windowDepth, windowSize, carMaterial, windowMaterial);

            Vector3 p9 = middleShape[middleShape.Count-1];
            Vector3 p10 = middleShape[middleShape.Count - 1] * new Vector3(-1f, 1f, 1f);
            Vector3 p11 = botShape[botShape.Count - 1] * new Vector3(-1f, 1f, 1f);
            Vector3 p12 = botShape[botShape.Count - 1];
            Mesh rear = MeshGenerator.ExtrudedPlane(new MeshGenerator.Quad(p12, p11, p10, p9), -windowDepth, 0.5f, carMaterial, detailMaterial);

            Vector3 p13 = middleShape[0];
            Vector3 p14 = middleShape[0] * new Vector3(-1f, 1f, 1f);
            Vector3 p15 = botShape[0] * new Vector3(-1f, 1f, 1f);
            Vector3 p16 = botShape[0];
            Mesh front = MeshGenerator.ExtrudedPlane(new MeshGenerator.Quad(p13, p14, p15, p16), -windowDepth, 0.5f, carMaterial, detailMaterial);

            List<Vector2> wheelLayers = new List<Vector2>() {
                new Vector2(wheelRadius, 0),
                new Vector2(wheelRadius, wheelWidth) };
            Mesh wheel = MeshGenerator.generateCylinder(wheelLayers, 14, rubberMaterial, 0);
            wheel.rotate(new Vector3(0f, 0f, -MathF.PI/2f));
            car += wheel.translated(botShape[3] + new Vector3(-wheelWidth * 0.95f, -wheelRadius * 1.0f, 0f));
            car += wheel.translated(botShape[9] + new Vector3(-wheelWidth * 0.95f, -wheelRadius * 1.0f, 0f));
            wheel.rotate(new Vector3(0f, 0f, MathF.PI));
            car += wheel.translated(botShape[3]*new Vector3(-1f, 1f, 1f) + new Vector3(wheelWidth * 0.95f, -wheelRadius * 1.0f, 0f));
            car += wheel.translated(botShape[9] * new Vector3(-1f, 1f, 1f) + new Vector3(wheelWidth * 0.95f, -wheelRadius * 1.0f, 0f));


            Mesh rearLights = (IcoSphereGenerator.CreateIcosphere(1, redLightMaterial)).scaled(new Vector3(0.1f));
            rearLights.translate(new Vector3(0f, p11.Y+(p10.Y-p11.Y)*0.5f, p9.Z-0.05f));
            car += rearLights.translated(new Vector3(p9.X * 0.75f, 0, 0));
            car += rearLights.translated(new Vector3(-p9.X * 0.75f, 0, 0));

            Mesh frontLight = (MeshGenerator.generateBox(frontLightMaterial)).scaled(new Vector3(0.3f, 0.15f, 0.04f));
            Vector3 frontMiddlePos = new Vector3(0f, p15.Y + (p13.Y - p15.Y) * 0.5f, p16.Z);
            leftLightPos = frontMiddlePos + new Vector3(-p9.X * 0.91f, 0, 0);
            rightLightPos = frontMiddlePos + new Vector3(p9.X * 0.91f, 0, 0);
            car += frontLight.translated(leftLightPos);
            car += frontLight.translated(rightLightPos);

            car += botHalf;
            car += topHalf;
            car += fronTop;
            car += backTop;
            car += roof;
            car += frontWindow;
            car += backWindow;
            car += rear;
            car += front;


            exhaustPos = p12+ new Vector3(-p12.X * 0.5f, 0f, 0f);
            List<Vector2> exhaustLayers = new List<Vector2>() {
                new Vector2(exhaustRadius, 0),
                new Vector2(exhaustRadius, exhaustLength)};
            Mesh exhaust = MeshGenerator.generateCylinder(exhaustLayers, 8, detailMaterial, 0);
            
            exhaust.rotate(new Vector3(MathF.PI / 2f, 0f, 0f));
            exhaust.translate(exhaustPos);
            exhaust.makeFlat(true, true);

            car += exhaust;

            car.translate(new Vector3(0f, wheelRadius * 2.0f - botWheelIndent, 0f));
            exhaustPos += (new Vector3(0f, wheelRadius * 2.0f - botWheelIndent, 0f));
            
            return car;
        }
    }
}
