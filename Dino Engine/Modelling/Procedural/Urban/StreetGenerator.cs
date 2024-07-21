using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Drawing;

namespace Dino_Engine.Modelling.Procedural.Urban
{
    public class StreetGenerator
    {
        public float segmentLength = 10;
        public float lineWidth = 0.27f;
        public int lanes = 3;
        public float laneWdith = 5f;
        public float sideWalkHeight =0.4f;
        public float sideWalkWidth = 9f;

        public Material streetMaterial = new Material(new Colour(25, 25, 27, 1.0f), 1);
        public Material lineMaterial = new Material(new Colour(233, 233, 233, 1.0f), 1);
        public Material lineMaterialCenter = new Material(new Colour(247, 219, 36, 1.0f), 1);
        public Material sideWalkMaterial = new Material(new Colour(95, 95, 95, 1.0f), 1);

        public float TotalWidth{ get => lanes * laneWdith * 2 - lineWidth * 1.0f + sideWalkWidth * 2f; }


        public Mesh GenerateCrossRoad()
        {
            Mesh.scaleUV = true;

            Mesh mesh = MeshGenerator.generatePlane(streetMaterial);
            float roadTotalWidth = lanes * laneWdith * 2f - lineWidth;

            mesh.scale(new Vector3(roadTotalWidth));
            int crossLinesPerLane = 4;
            float crossingSizeFactor = 0.7f;

            int totalCrossLines = crossLinesPerLane * lanes * 2+1;




            for (int sideIndex = 0; sideIndex<4; sideIndex++)
            {
                Mesh side = new Mesh();
                Mesh corner = MeshGenerator.generateBox(sideWalkMaterial);
                corner.scale(new Vector3(sideWalkWidth, sideWalkWidth, sideWalkHeight));
                corner.translate(new Vector3(TotalWidth * 0.5f - sideWalkWidth * 0.5f, TotalWidth*0.5f-sideWalkWidth*0.5f, -sideWalkHeight*0.5f));
                side += corner;



                for (int i = 0; i < totalCrossLines; i++)
                {
                    Material material = streetMaterial;
                    if (i % 2 == 1) material = lineMaterial;

                    float crossSectionWidth = roadTotalWidth / totalCrossLines;
                    Mesh crossWalkPart = MeshGenerator.generatePlane(material);
                    crossWalkPart.scale(new Vector3(crossSectionWidth, sideWalkWidth* crossingSizeFactor, 1f));
                    crossWalkPart.translate(new Vector3(i * crossSectionWidth+crossSectionWidth*0.5f-roadTotalWidth*0.5f, TotalWidth * 0.5f - sideWalkWidth * 0.5f, 0));


                    side += crossWalkPart;
                }
                Mesh gapFill = MeshGenerator.generatePlane(streetMaterial);
                float gapSize = sideWalkWidth * (1f - crossingSizeFactor) * 0.5f;
                gapFill.scale(new Vector3(roadTotalWidth, gapSize, 1f));

                side += gapFill.translated(new Vector3(0, TotalWidth * 0.5f - gapSize * 0.5f, 0));
                side += gapFill.translated(new Vector3(0, TotalWidth * 0.5f + gapSize * 0.5f - sideWalkWidth, 0));

                side.rotate(new Vector3(0f, 0f, MathF.PI*0.5f* sideIndex));
                mesh += side;

            }


            mesh.rotate(new Vector3(MathF.PI/2f, 0f, 0f));
            Mesh.scaleUV = false;


            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                Mesh trafficLight = GenerateTrafficLight();
                trafficLight.translate(new Vector3(TotalWidth * 0.5f - sideWalkWidth + 1, -sideWalkHeight * 0.5f, -sideWalkWidth+1f + TotalWidth*0.5f));
                trafficLight.rotate(new Vector3(0f, MathF.PI * 0.5f * sideIndex, 0f));
                mesh += trafficLight;
            }



            return mesh;
        }

        public Mesh GenerateStreet(int segments, out float totalLength)
        {
            Mesh.scaleUV = true;

            totalLength = segmentLength * segments;

            Mesh street = new Mesh();

            Mesh unitPlaneStreet = MeshGenerator.generatePlane(streetMaterial);
            unitPlaneStreet.rotate(new Vector3(MathF.PI / 2f, 0f, 0f));
            Mesh unitPlaneLine = MeshGenerator.generatePlane(lineMaterial);
            unitPlaneLine.rotate(new Vector3(MathF.PI / 2f, 0f, 0f));

            street += MeshGenerator.generatePlane(lineMaterialCenter).rotated(new Vector3(MathF.PI / 2f, 0f, 0f)).scaled(new Vector3(lineWidth, 1f, totalLength)).translated(new Vector3(0f, 0f, totalLength * 0.5f));

            Mesh segment = new Mesh();

            for (int i = 1; i <= lanes; i++)
            {
                if (i != lanes)
                {
                    segment += unitPlaneLine.scaled(new Vector3(lineWidth, 1f, segmentLength * 0.5f)).translated(new Vector3(i * laneWdith, 0f, 0f));
                }
                segment += unitPlaneStreet.scaled(new Vector3(laneWdith - lineWidth * 1f, 1f, segmentLength * 0.5f)).translated(new Vector3(i * laneWdith - laneWdith / 2f, 0f, 0f));
            }
            segment += segment.rotated(new Vector3(0f, MathF.PI / 1f, 0f));

            Mesh segment2 = unitPlaneStreet.scaled(new Vector3(laneWdith * lanes - lineWidth * 0.5f, 1f, segmentLength * 0.5f)).translated(new Vector3((laneWdith * lanes * 0.5f) + lineWidth * 0.25f, 0f, segmentLength * 0.5f));
            segment += segment2;
            segment += segment2.translated(new Vector3(0f, 0f, -segmentLength)).rotated(new Vector3(0f, MathF.PI / 1f, 0f));



            segment.translate(new Vector3(0f, 0f, segmentLength * 0.5f - segmentLength / 4f));

            for (int i = 0; i < segments; i++)
            {
                street += segment.translated(new Vector3(0f, 0f, i * segmentLength));
            }

            Mesh sidewalk = MeshGenerator.generateBox(sideWalkMaterial);
            sidewalk.scale(new Vector3(sideWalkWidth, sideWalkHeight, totalLength));
            sidewalk.translate(new Vector3(0f, sideWalkHeight * 0.5f, totalLength * 0.5f));

            float sideWalkOffset = lanes * laneWdith - lineWidth * 0.5f+sideWalkWidth*0.5f;
            street += sidewalk.translated(new Vector3(sideWalkOffset, 0f, 0f));
            street += sidewalk.translated(new Vector3(-sideWalkOffset, 0f, 0f));

            Mesh.scaleUV = false;
            return street;

        }

        public Mesh GenerateTrafficLight()
        {
            Material poleMaterial = new Material(new Colour(122, 122, 122, 1.0f), 1);
            Material blackMaterial = new Material(new Colour(20, 20, 20, 1.0f), 1);
            Material redMaterial = new Material(new Colour(235, 15, 15, 1.0f), 1);
            Material greenMaterial = new Material(new Colour(15, 235, 15, 1.0f), 1);
            Material yellowMaterial = new Material(new Colour(235, 235, 15, 1.0f), 1);

            float r = 0.5f;
            float angle = MathF.PI / 2f;
            float h = 11f;
            Mesh mesh = new Mesh();
            Transformation transformation = new Transformation();
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(r, 0),
                new Vector2(r, h*0.05f),
                new Vector2(r*0.94f, h*0.053f),
                new Vector2(r*0.94f, h*0.24f),
                new Vector2(r*0.64f, h*0.255f),
                new Vector2(r*0.64f, h*1.0f) };
            Mesh pole = MeshGenerator.generateCylinder(layers, 8, poleMaterial, true);
            mesh += pole;


            float h2 = lanes * laneWdith;

            List<Vector2> layers2 = new List<Vector2>() {
                new Vector2(r*0.6f, 0.0f),
                new Vector2(r*0.6f, h2)};
            pole = MeshGenerator.generateCylinder(layers2, 8, poleMaterial, true);

            transformation *= new Transformation(new Vector3(0f, h - r * 1.5f, 0f), new Vector3(0, 0f, angle), new Vector3(1f));
            pole.Transform(transformation);
            mesh += pole;

            for (int i = 0; i < lanes; i++)
            {
                Mesh lightBox = MeshGenerator.generateBox(blackMaterial);
                lightBox.scale(new Vector3(1f, 2f, 0.5f));
                lightBox += (IcoSphereGenerator.CreateIcosphere(1, redMaterial)).scaled(new Vector3(0.25f)).translated(new Vector3(0f, -0.5f, 0.2f));
                lightBox += (IcoSphereGenerator.CreateIcosphere(1, yellowMaterial)).scaled(new Vector3(0.25f)).translated(new Vector3(0f, 0.0f, 0.2f));
                lightBox += (IcoSphereGenerator.CreateIcosphere(1, greenMaterial)).scaled(new Vector3(0.25f)).translated(new Vector3(0f, 0.5f, 0.2f));
                Transformation transformation2 = new Transformation(new Vector3(0, laneWdith * i+laneWdith*0.5f, 0.25f), new Vector3(0, 0f, angle), new Vector3(1f))* transformation;

                lightBox.Transform(transformation2);

                mesh += lightBox;
            }


            return mesh;
        }
    }
}
