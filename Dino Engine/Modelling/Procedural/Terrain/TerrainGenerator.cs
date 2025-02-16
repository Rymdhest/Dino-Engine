using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Util.Noise;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public class TerrainGenerator
    {

        public float _mountainCoverage = 0.1f;
        public float _frequenzy = 0.01f;
        public int _octaves = 10;
        public float yScale = 160f;

        private OpenSimplexNoise noise;

        public TerrainGenerator()
        {
            noise = new OpenSimplexNoise();
        }
        public TerrainGenerator(long seed)
        {
            noise = new OpenSimplexNoise(seed);
        }
        public Entity generateTerrainChunkEntity(Vector2 worldPos, Vector2 worldSize, float quadsPerMeter)
        {

            Vector2i resolution = new Vector2i((int)(worldSize.X * quadsPerMeter)+1, (int)(worldSize.Y*quadsPerMeter)+1);
            ECSEngine eCSEngine = Engine.Instance.ECSEngine;

            Entity terrainEntity = new Entity("Terrain");

            FloatGrid terrainGrid = generateChunk(worldPos, worldSize, resolution);
            Material grass = new Material(new Colour(255, 255, 255),Engine.RenderEngine.textureGenerator.grain);
            grass = new Material(new Colour(105, 55, 55), Engine.RenderEngine.textureGenerator.grain);
            Mesh groundMesh = TerrainMeshGenerator.GridToMesh(terrainGrid, worldSize, grass,  out Vector3Grid terrainNormals);
            glModel groundModel = glLoader.loadToVAO(groundMesh);

            terrainEntity.addComponent(new TransformationComponent(new Vector3(worldPos.X, 0f, worldPos.Y), new Vector3(0), new Vector3(1f)));
            terrainEntity.addComponent(new ModelComponent(groundModel));
            terrainEntity.addComponent(new TerrainMapsComponent(terrainGrid, terrainNormals));
            terrainEntity.addComponent(new CollisionComponent(new TerrainHitBox(new Vector3(0), new Vector3(worldSize.X, yScale, worldSize.Y))));
            eCSEngine.AddEnityToSystem<TerrainRenderSystem>(terrainEntity);
            eCSEngine.AddEnityToSystem<TerrainSystem>(terrainEntity);
            eCSEngine.AddEnityToSystem<CollidableSystem>(terrainEntity);

            return terrainEntity;
        }

        public FloatGrid generateChunk(Vector2 positionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid grid = new FloatGrid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution-new Vector2(1));

            for (int z = 0; z < grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    grid.Values[x, z] = getHeightAt(positionWorld+new Vector2(x, z)* cellSizeWorld);

                    //grid.Values[x, z] = 10f;
                    /*
                    grid.Values[x, z] = 2f*MathF.Sin(x*cellSizeWorld.X)+10f;
                    grid.Values[x, z] = (x * cellSizeWorld.X);
                    if (x == 3 && z == 3)   grid.Values[x, z] = 10;
                    if (x == 3 && z == 1) grid.Values[x, z] = 20;
                    */
                }
            }

            return grid;
        }

        public float getHeightAt(Vector2 position)
        {
            float x = position.X;
            float z = position.Y;
            float y = 0f;
            float frequency = _frequenzy;
            float amplitude = 1f;
            float totalAmplitude = 0f;

            float mountainFactor = MyMath.clamp01( noise.Evaluate(position.X*0.006f, position.Y*0.006f)/2f+0.5f);
            mountainFactor = MathF.Pow(mountainFactor, 2.8f);
            for (int i = 0; i < _octaves; i++)
            {
                y +=( 1f-MathF.Abs( noise.Evaluate(x * frequency, z * frequency))) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            y /= totalAmplitude;
            y *= mountainFactor;

            y *= yScale;
            y -= 5f;
            if (y < 0f) y *= 0.5f;

            return y;
        }

        
    }
}
