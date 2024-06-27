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
        public float sideWalkHeight = 0.3f;
        public float sideWalkWidth = 5f;

        public Material streetMaterial = new Material(new Colour(25, 25, 27, 1.0f), 0.0f, 0.8f, 0.0f);
        public Material lineMaterial = new Material(new Colour(233, 233, 233, 1.0f), 0.0f, 0.4f, 0.0f);
        public Material lineMaterialCenter = new Material(new Colour(247, 219, 36, 1.0f), 0.0f, 0.4f, 0.0f);
        public Material sideWalkMaterial = new Material(new Colour(95, 95, 95, 1.0f), 0.0f, 0.6f, 0.0f);

        public float TotalWidth{ get => lanes * laneWdith * 2 - lineWidth * 1.0f + sideWalkWidth * 2f; }

        public glModel GenerateStreet(int segments, out float totalLength)
        {

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

            float sideWalkOffset = lanes * laneWdith + laneWdith * 0.5f - lineWidth * 0.5f;
            street += sidewalk.translated(new Vector3(sideWalkOffset, 0f, 0f));
            street += sidewalk.translated(new Vector3(-sideWalkOffset, 0f, 0f));

            return glLoader.loadToVAO(street);

        }
    }
}
