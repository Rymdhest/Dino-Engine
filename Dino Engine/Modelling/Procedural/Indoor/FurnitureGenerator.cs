using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Indoor
{
    public class FurnitureGenerator
    {

        public static Mesh GenerateTable(out float tableSurfaceHeight)
        {
            //Mesh.scaleUV = true;
            float legThickness = 0.4f;
            float tableThickness = 0.1f;
            float legHeight = 4f;
            Vector2 tableSize = new Vector2(6, 10);

            Material wood = new Material(new Colour(36, 26, 9, 1), Engine.RenderEngine.textureGenerator.grainIndex);
            Mesh box = MeshGenerator.generateBox(wood);

            Mesh table = new Mesh();

            Mesh leg = box.scaled(new Vector3(legThickness, legHeight, legThickness)).translated(new Vector3(0f, legHeight/2f, 0f));

            Vector2 offset = tableSize - new Vector2(legThickness);

            table += leg.translated(new Vector3(-offset.X / 2f, 0f, -offset.Y / 2f));
            table += leg.translated(new Vector3(-offset.X / 2f, 0f, offset.Y / 2f));
            table += leg.translated(new Vector3(offset.X / 2f, 0f, offset.Y / 2f));
            table += leg.translated(new Vector3(offset.X / 2f, 0f, -offset.Y / 2f));

            table += box.scaled(new Vector3(tableSize.X, tableThickness, tableSize.Y)).translated(new Vector3(0f, legHeight+tableThickness/2f, 0f));
            tableSurfaceHeight = legHeight + tableThickness*2f;

            Mesh.scaleUV = false;
            return table;
        }
        public static Mesh GenerateRoundTable(out float tableSurfaceHeight)
        {
            float legThickness = 0.35f;
            float tableThickness = 0.2f;
            float legHeight = 4f;
            float radius = 1.7f;

            Material wood = new Material(new Colour(36, 26, 9, 1), Engine.RenderEngine.textureGenerator.flatIndex);
            Mesh box = MeshGenerator.generateBox(wood);

            Mesh table = new Mesh();


            float r = radius;
            float h = tableThickness;
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(r*0.95f, 0),
                new Vector2(r, h*0.5f),
                new Vector2(r*0.95f, h*1.0f) };
            Mesh top = MeshGenerator.generateCylinder(layers, 16, wood, 0);
            table += top.translated(new Vector3(0f, legHeight, 0f));

            Mesh leg = box.scaled(new Vector3(legThickness, legHeight, legThickness)).translated(new Vector3(0f, legHeight / 2f, 0f));

            float offset = radius - legThickness;
            offset *= 0.65f;
            table += leg.translated(new Vector3(-offset, 0f, -offset ));
            table += leg.translated(new Vector3(-offset , 0f, offset ));
            table += leg.translated(new Vector3(offset , 0f, offset ));
            table += leg.translated(new Vector3(offset , 0f, -offset));

            tableSurfaceHeight = legHeight + tableThickness;
            return table;
        }

        public static Mesh GenerateLamp(out Vector3 lightPosition, out Vector3 lightDirection)
        {
            Material wood = new Material(new Colour(115, 115, 95, 1), Engine.RenderEngine.textureGenerator.flatIndex);
            Material glowMaterial = new Material(new Colour(55, 25, 20, 3), Engine.RenderEngine.textureGenerator.flatGlowIndex);

            Mesh lamp = new Mesh();

            Mesh ball = IcoSphereGenerator.CreateIcosphere(2, wood).scaled(new Vector3(0.35f));

            Transformation transformation= new Transformation();
            float r = 1f;
            float h = 0.35f;
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(r, 0),
                new Vector2(r*0.9f, h*0.85f),
                new Vector2(r*0.84f, h*1.0f) };
            Mesh pole = MeshGenerator.generateCylinder(layers, 16, wood, 0f);
            lamp += pole;
            transformation = new Transformation(new Vector3(0f, h, 0f), new Vector3(MathF.PI / 8f, 0f, 0f), new Vector3(1)) * transformation;

            r = 0.16f;
            h = 2.5f;
            layers = new List<Vector2>() {
                new Vector2(r, 0),
                new Vector2(r, h*1.0f) };
            pole = MeshGenerator.generateCylinder(layers, 16, wood);
            lamp += pole.Transformed(transformation);
            transformation = new Transformation(new Vector3(0f, h, 0f), new Vector3(0f), new Vector3(1)) * transformation;

            lamp += ball.Transformed(transformation);

            transformation = new Transformation(new Vector3(), new Vector3(-MathF.PI *0.37f, 0f, 0f), new Vector3(1)) * transformation;

            lamp += pole.Transformed(transformation);
            transformation = new Transformation(new Vector3(0f, h, 0f), new Vector3(0f), new Vector3(1)) * transformation;

            lamp += ball.Transformed(transformation);

            transformation = new Transformation(new Vector3(), new Vector3(-MathF.PI * 0.47f, 0f, 0f), new Vector3(1)) * transformation;

            r = 0.86f;
            h = 1.3f;
            layers = new List<Vector2>() {
                new Vector2(r*0.1f, 0),
                new Vector2(r, h*1.0f),
                new Vector2(r*0.91f, h*1.1f),
                new Vector2(r*0.81f, h*1.0f),
                new Vector2(r*0.05f, h*0.1f)};
            pole = MeshGenerator.generateCylinder(layers, 16, glowMaterial);
            lamp += pole.Transformed(transformation);

            lightPosition = transformation.position;
            //lightDirection = (new Vector4(0f, 1f, 0f, 0.0f) * Matrix4.Invert((MyMath.createTransformationMatrix(transformation)))).Xyz;
            //lightDirection = (new Vector4(0.1f, 0.0f, 0.0f, 0.0f)*((MyMath.createTransformationMatrix(transformation)))).Xyz;
            lightDirection = transformation.rotation+new Vector3(MathF.PI, 0, 0f);
            return lamp;
        }

        public static Mesh GenerateCandle(out Vector3 lightPosition)
        {
            Material wood = new Material(new Colour(124, 87, 66, 1), Engine.RenderEngine.textureGenerator.flatIndex);
            Material candleMaterial = new Material(new Colour(255, 255, 255, 1), Engine.RenderEngine.textureGenerator.flatIndex);
            Material glowMaterial = new Material(new Colour(255, 247, 209, 1), Engine.RenderEngine.textureGenerator.flatGlowIndex);

            float waxStickHeight = 1f+MyMath.rng(1.7f);

            Transformation transformation = new Transformation();
            Mesh lamp = new Mesh();

            float r = 0.8f;
            float h = 0.35f;
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(r*0.75f, 0),
                new Vector2(r*1.0f, h*0.85f),
                new Vector2(r*0.94f, h*0.83f),
                new Vector2(r*0.74f, h*0.03f),
                new Vector2(r*0.44f, h*0.04f),
                new Vector2(r*0.34f, h*0.14f),
                new Vector2(r*0.19f, h*0.34f),
                new Vector2(r*0.20f, h*0.84f),
                new Vector2(r*0.25f, h*1.84f),
                new Vector2(r*0.21f, h*1.86f),
                new Vector2(r*0.21f, h*1.0f)};
            Mesh pole = MeshGenerator.generateCylinder(layers, 16, wood);
            lamp += pole;
            transformation = new Transformation(new Vector3(0f, h, 0f), new Vector3(0, 0f, 0f), new Vector3(1)) * transformation;


            r = r*0.2f;
            h = waxStickHeight;
            layers = new List<Vector2>() {
                new Vector2(r*1.0f, 0),
                new Vector2(r*1.0f, h*0.1f),
                new Vector2(r*1.0f, h*1.0f),
                new Vector2(r*0.95f, h*1.01f),
                new Vector2(r*0.65f, h*0.98f),
                new Vector2(r*0.35f, h*0.99f) };
            pole = MeshGenerator.generateCylinder(layers, 16, candleMaterial, 0.1f);
            lamp += pole.Transformed(transformation);
            transformation = new Transformation(new Vector3(0f, h, 0f), new Vector3(0, 0f, 0f), new Vector3(1)) * transformation;

            lamp += IcoSphereGenerator.CreateIcosphere(1, glowMaterial).scaled(new Vector3(0.1f, 0.33f, 0.1f)).Transformed(transformation);

            lightPosition = transformation.position;
            return lamp;
        }

        public static Mesh GenerateChair()
        {
            float legThickness = 0.35f;
            float chairThickness = 0.2f;
            float legHeight = 2.6f;
            float backHeight = 4f;
            float innerSupportHeight = backHeight * 0.6f;
            float innerSupport2Height = legThickness * 1.5f;

            Vector2 chairSize = new Vector2(3, 3);

            Material wood = new Material(new Colour(36, 26, 9, 1), Engine.RenderEngine.textureGenerator.flatIndex);
            Mesh box = MeshGenerator.generateBox(wood);

            Mesh chair = new Mesh();

            Mesh leg = box.scaled(new Vector3(legThickness, legHeight, legThickness)).translated(new Vector3(0f, legHeight / 2f, 0f));

            Vector2 offset = chairSize - new Vector2(legThickness);

            chair += leg.translated(new Vector3(-offset.X / 2f, 0f, -offset.Y / 2f));
            chair += leg.translated(new Vector3(-offset.X / 2f, 0f, offset.Y / 2f));
            chair += leg.translated(new Vector3(offset.X / 2f, 0f, offset.Y / 2f));
            chair += leg.translated(new Vector3(offset.X / 2f, 0f, -offset.Y / 2f));

            chair += box.scaled(new Vector3(chairSize.X, chairThickness, chairSize.Y)).translated(new Vector3(0f, legHeight + chairThickness / 2f, 0f));

            Mesh backSupport = box.scaled(new Vector3(legThickness, backHeight, legThickness)).translated(new Vector3(0f, backHeight/2f, 0f));
            Mesh backSupportInner = box.scaled(new Vector3(legThickness*0.6f, innerSupportHeight, legThickness*0.3f)).translated(new Vector3(0f, backHeight / 2f, 0f));
            Mesh backSupportInner2 = box.scaled(new Vector3(chairSize.X-legThickness*2f, innerSupport2Height, legThickness * 0.3f)).translated(new Vector3(0f, backHeight / 2f, 0f));
            Mesh back = new Mesh();
            back += backSupport.translated(new Vector3(offset.X / 2f, 0f, 0));
            back += backSupport.translated(new Vector3(-offset.X / 2f, 0f, 0));

            back += backSupportInner.translated(new Vector3(0f, 0f, 0));
            back += backSupportInner.translated(new Vector3(offset.X*0.16f, 0f, 0));
            back += backSupportInner.translated(new Vector3(offset.X * 0.32f, 0f, 0));
            back += backSupportInner.translated(new Vector3(-offset.X * 0.16f, 0f, 0));
            back += backSupportInner2.translated(new Vector3(0f, innerSupportHeight/2f+ innerSupport2Height*0.5f, 0));
            back += backSupportInner2.translated(new Vector3(0f, -innerSupportHeight / 2f - innerSupport2Height * 0.5f, 0));

            back += backSupportInner.translated(new Vector3(-offset.X * 0.32f, 0f, 0));

            chair += back.Transformed(new Transformation( new Vector3(0f, legHeight+legThickness/2f, offset.Y / 2f), new Vector3(0.12f, 0f, 0f), new Vector3(1f)));

            return chair;
        }
    }
}
