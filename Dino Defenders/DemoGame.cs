using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Util.Data_Structures.Grids;
using Util.Noise;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Components;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util.Data_Structures;
using System.Xml.Linq;
using System.Drawing;
using Dino_Engine.Textures;
using Dino_Engine.Modelling.Procedural.Indoor;

namespace Dino_Defenders
{
    internal class DemoGame : Game
    {

        private TerrainGenerator terrainGenerator;
        public DemoGame(Engine engine) : base(engine)
        {
            terrainGenerator = new TerrainGenerator();
            SpawnWorld();
        }
        public override void update()
        {
            if (Engine.WindowHandler.IsKeyPressed(Keys.F1))
            {
                Engine.RenderEngine.textureGenerator.CleanUp();
                Engine.RenderEngine.textureGenerator = new Dino_Engine.Textures.TextureGenerator();
                Engine.RenderEngine.textureGenerator.GenerateAllTextures();
                SpawnWorld();
            }
            /*
            if (Engine.WindowHandler.IsKeyPressed(Keys.B))
            {
                float size = 0.8f;
                float speed = 55f;
                Colour colour = new Colour(255, 255, 255, 4.0f);
                Material bigBallMaterial = new Material(colour, Material.GLOW.materialIndex);
                EntityOLD bigBall = new EntityOLD("Big Ball");
                bigBall.addComponent(new TransformationComponent(new Transformation(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position, new Vector3(0f), new Vector3(size))));
                bigBall.addComponent(new ModelComponent(IcoSphereGenerator.CreateIcosphere(1, bigBallMaterial)));
                bigBall.addComponent(new VelocityComponent(speed*eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.createForwardVector()));
                bigBall.addComponent(new MassComponent(1.0f));
                bigBall.addComponent(new SelfDestroyComponent(15f));
                bigBall.addComponent(new ColourComponent(colour));
                bigBall.addComponent(new AttunuationComponent(0.01f, 0.005f, 0.001f));
                bigBall.addComponent(new CollisionComponent(new SphereHitbox(size)));

                CollisionEventComponent collisionComponent = new CollisionEventComponent((collider, collisionPoint, collisionNormal) =>
                {
                    float sphereRadius = ((SphereHitbox)bigBall.getComponent<CollisionComponent>().HitBox).Radius;

                    Vector3 newPosition = collisionPoint + collisionNormal * sphereRadius;
                    Vector3 newVelocity = MyMath.reflect(bigBall.getComponent<VelocityComponent>().velocity, collisionNormal);

                    bigBall.CleanUp();

                    for (int i = 0; i< 10; i++)
                    {
                        smallSmallRandomBall(newPosition);
                    }
                    EntityOLD emitter = new EntityOLD("Ball Particle Emitter");
                    emitter.addComponent(new TransformationComponent(new Transformation(newPosition, new Vector3(0f, 0f, 0f), new Vector3(1))));
                    //emitter.addComponent(new ChildComponent(roadCone));
                    var emitterComponent = new ParticleEmitterComponent();
                    emitterComponent.particleSpeed = 0.4f;
                    emitterComponent.particlesPerSecond = 10f;
                    emitterComponent.particleSizeStart = 2.25f;
                    emitterComponent.particleWeight = -0.25f;
                    emitterComponent.particleSpeed = 6f;
                    emitterComponent.particleDuration = 1.4f;
                    emitterComponent.particleDirectionError = 0.5f;
                    emitterComponent.particlePositionError = 0.5f;
                    emitter.addComponent(emitterComponent);
                    emitter.addComponent(new DirectionComponent(new Vector3(0f, 1f, 0f)));
                    emitter.addComponent(new SelfDestroyComponent(2f));
                    eCSEngine.AddEnityToSystem<ParticleEmitterSystem>(emitter);
                    eCSEngine.AddEnityToSystem<SelfDestroySystem>(emitter);



                    EntityOLD grassDisplaceEntity = new EntityOLD("Grass Displace");
                    grassDisplaceEntity.addComponent(new TransformationComponent(collisionPoint, new Vector3(0), new Vector3(1)));
                    grassDisplaceEntity.addComponent(new GrassBlastComponent(5f, 5f, 20f));
                    eCSEngine.AddEnityToSystem<GrassBlastSystem>(grassDisplaceEntity);

                });
                bigBall.addComponent(collisionComponent);


                eCSEngine.AddEnityToSystem<ModelRenderSystem>(bigBall);
                eCSEngine.AddEnityToSystem<VelocitySystem>(bigBall);
                eCSEngine.AddEnityToSystem<GravitySystem>(bigBall);
                eCSEngine.AddEnityToSystem<SelfDestroySystem>(bigBall);
                eCSEngine.AddEnityToSystem<PointLightSystem>(bigBall);
                eCSEngine.AddEnityToSystem<GrassInteractSystem>(bigBall);
                eCSEngine.AddEnityToSystem<CollidingSystem>(bigBall);

            }
            */
        }
        /*
        private void smallSmallRandomBall(Vector3 position)
        {
            ECSEngine eCSEngine = Engine.ECSEngine;
            float size = 0.5f+MyMath.rng()*0.7f;
            float speed = 15f;
            Vector3 col = MyMath.rng3D(0.5f)+new Vector3(0.5f);
            Colour colour = new Colour(col.X, col.Y, col.Z, 1.0f);
            Material bigBallMaterial = new Material(colour, Material.GLOW.materialIndex);
            EntityOLD smallBall = new EntityOLD("Small Ball");
            smallBall.addComponent(new TransformationComponent(position, new Vector3(0f), new Vector3(size)));
            smallBall.addComponent(new ModelComponent(IcoSphereGenerator.CreateIcosphere(1, bigBallMaterial)));
            smallBall.addComponent(new VelocityComponent(speed * MyMath.rng3DMinusPlus().Normalized()));
            smallBall.addComponent(new MassComponent(1.0f));
            smallBall.addComponent(new SelfDestroyComponent(7f+MyMath.rng(3f)));
            smallBall.addComponent(new ColourComponent(colour));
            smallBall.addComponent(new AttunuationComponent(0.01f, 0.01f, 0.01f));
            smallBall.addComponent(new CollisionComponent(new SphereHitbox(size)));

            CollisionEventComponent collisionComponent = new CollisionEventComponent((collider, collisionPoint, collisionNormal) =>
            {
                //reverse movement
                Vector3 velocity = smallBall.getComponent<VelocityComponent>().velocity;
                Transformation transf = smallBall.getComponent<TransformationComponent>().Transformation;
                transf.translate(-velocity * Engine.Delta);
                smallBall.getComponent<TransformationComponent>().Transformation = transf;

                float movementRemaining = velocity.Length*Engine.Delta;
                float sphereRadius = ((SphereHitbox)smallBall.getComponent<CollisionComponent>().HitBox).Radius;
                float distanceToCollision = Vector3.Distance(transf.position, collisionPoint)-sphereRadius;
                float movement = MathF.Min(movementRemaining, distanceToCollision);
                transf.translate(movement*(collisionPoint-transf.position).Normalized());
                movementRemaining -= movement;


                if (movementRemaining > 0.000001f)
                {
                    Vector3 newVelocity = MyMath.reflect(smallBall.getComponent<VelocityComponent>().velocity, collisionNormal) * 0.55f;
                    smallBall.getComponent<VelocityComponent>().velocity = newVelocity;
                    smallBall.getComponent<TransformationComponent>().SetLocalTransformation(transf.position + movementRemaining*newVelocity.Normalized());
                } else
                {
                    smallBall.getComponent<TransformationComponent>().SetLocalTransformation(new Vector3(transf.position.X, collisionPoint.Y+sphereRadius, transf.position.Z));
                    //smallBall.getComponent<TransformationComponent>().SetLocalTransformation(transf.position);
                }



            });
            smallBall.addComponent(collisionComponent);
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(smallBall);
            eCSEngine.AddEnityToSystem<VelocitySystem>(smallBall);
            eCSEngine.AddEnityToSystem<GravitySystem>(smallBall);
            eCSEngine.AddEnityToSystem<SelfDestroySystem>(smallBall);
            eCSEngine.AddEnityToSystem<PointLightSystem>(smallBall);
            eCSEngine.AddEnityToSystem<GrassInteractSystem>(smallBall);
            eCSEngine.AddEnityToSystem<CollidingSystem>(smallBall);
        }
        */


        private void SpawnWorld()
        {
            ECSWorld world = Engine.world;
            world.DirtyEntitiesBuffers.ClearAll();
            world.DirtyEntities.Clear();
            //terrainGenerator = new TerrainGenerator();
            world.ClearAllEntitiesExcept(world.QueryEntities(new BitMask(typeof(MainCameraComponent)), BitMask.Empty).ToArray());

            world.RegisterSingleton<TerrainQuadTreeSingleton>(world.CreateEntity(new TerrainQuadTreeSingleton(new QuadTreeNode(new Vector2(0, 0), 1000f, 0))));
            world.RegisterSingleton<TerrainGeneratorSingleton>(world.CreateEntity(new TerrainGeneratorSingleton(terrainGenerator)));
            world.RegisterSingleton<CollisionEventBufferSingleton>(world.CreateEntity(new CollisionEventBufferSingleton()));
            world.RegisterSingleton<RenderSpatialGridSingleton>(world.CreateEntity(new RenderSpatialGridSingleton(15f)));
            world.ApplyDeferredCommands();

            Mesh boxMesh = MeshGenerator.generateBox(new VertexMaterial(TextureGenerator.brick));
            Mesh.scaleUV = true;
            boxMesh.scale(new Vector3(10f, 10f, 10f));
            boxMesh.scaleUVs(new Vector2(1.0f, 1.0f));
            boxMesh.ProjectUVsWorldSpaceCube(0.5f);
            //Mesh.scaleUV = true;

            float y = world.GetComponent<TerrainGeneratorSingleton>(world.GetSingleton<TerrainGeneratorSingleton>()).Generator.getHeightAt(new Vector2(100, 100));
            y += 4;
            world.CreateEntity(
                new PositionComponent(new Vector3(100, y, 100)),
                new RotationComponent(new Vector3(0,1f, 0)),
                new ScaleComponent(new Vector3(1)),
                new ModelComponent(glLoader.loadToVAO(boxMesh)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );
            
            world.CreateEntity("Sun",
                new DirectionalLightTag(),
                new DirectionNormalizedComponent(new Vector3(-1.10f, -3.5f, -2.9f)),
                new ColorComponent(new Colour(1.0f, 1.0f, 1.0f, 10f)),
                new AmbientLightComponent(0.05f),
                new DirectionalCascadingShadowComponent(new Vector2i(1024, 1024) * 2, 3, 1750),
                new CelestialBodyComponent()
            ) ;
            
            for (int i = 0; i<0; i++)
            {
                world.CreateEntity("Moon",
                    new DirectionalLightTag(),
                    new DirectionNormalizedComponent(new Vector3(MyMath.rngMinusPlus(), -MyMath.rng()*1.0f, MyMath.rngMinusPlus())),
                    new CelestialBodyComponent(),
                    new AmbientLightComponent(0.0f),
                    new ColorComponent(new Colour(0.3f, 0.5f, 1.0f, 1.0f)),
                    new DirectionalCascadingShadowComponent(new Vector2i(1024, 1024) * 4, 1, 1000)
                );
            }
            

            
            world.CreateEntity("Sky",
                new DirectionalLightTag(),
                new DirectionNormalizedComponent(new Vector3(0.01f, -1.0f, 0.01f)),
                new ColorComponent(new Colour(86, 155, 255, 1.0f)),
                new SkyTag(),
                new AmbientLightComponent(0.8f)
            );
            

            spawnCity(Engine.world);
            spawnTestScene(Engine.world);
            //spawnIndoorScene(eCSEngine);
        }
        
        private void spawnTestScene(ECSWorld world)
        {


            glModel testModel = glLoader.loadToVAO(TreeGenerator.GenerateBirchTree());
            Engine.RenderEngine.textureGenerator.AddImposterToModel(testModel, 30);
            world.CreateEntity("test",
                new PositionComponent(new Vector3(10, 0, -5)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(1f)),
                new ModelComponent(testModel),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            Mesh candle = FurnitureGenerator.GenerateCandle2();
            world.CreateEntity("candle",
                new PositionComponent(new Vector3(0, 0, -5)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(8f)),
                new ModelComponent(glLoader.loadToVAO(candle)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            Mesh sphere = IcoSphereGenerator.CreateIcosphere(3, new VertexMaterial(TextureGenerator.cobble), 1);
            world.CreateEntity("sphere",
                new PositionComponent(new Vector3(20, 5, 0)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(1f)),
                new ModelComponent(glLoader.loadToVAO(sphere)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            Mesh cube = MeshGenerator.generateBox(new VertexMaterial(TextureGenerator.grass));
            world.CreateEntity("cube",
                new PositionComponent(new Vector3(24, 5, 0)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(2f)),
                new ModelComponent(glLoader.loadToVAO(cube)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            Mesh floorMesh = MeshGenerator.generatePlane(new VertexMaterial(TextureGenerator.metalFloor, new Colour(55,55,55)));
            floorMesh.scaleUVs(new Vector2(20f));
            world.CreateEntity("ground",
                new PositionComponent(new Vector3(0, 0, 0)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(4f*2)),
                new ModelComponent(glLoader.loadToVAO(floorMesh)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            world.CreateEntity("raw leaf mesh",
                new PositionComponent(new Vector3(0, 5, -30)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(10f)),
                new ModelComponent(glLoader.loadToVAO(TreeGenerator.GenerateLeaf())),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            world.CreateEntity("raw branch mesh",
                new PositionComponent(new Vector3(20, 0, -30)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(0.5f)),
                new ModelComponent(glLoader.loadToVAO(TextureGenerator.TEST_BRANCH_MESH)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            world.CreateEntity("raw tree branch mesh",
                new PositionComponent(new Vector3(40, 0, -30)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(0.5f)),
                new ModelComponent(glLoader.loadToVAO(TextureGenerator.TEST_TREE_BRANCh_MESH)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );


            Mesh cubeMeshLeaf = MeshGenerator.generateBox(new VertexMaterial(TextureGenerator.pineBranch));
            world.CreateEntity("leaf texture cube",
                new PositionComponent(new Vector3(0f, 5, -50)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(10f)),
                new ModelComponent(glLoader.loadToVAO(cubeMeshLeaf)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            Mesh boxMeshLeaf = MeshGenerator.generateBox(new VertexMaterial(TextureGenerator.birchTwig));
            world.CreateEntity("branch texture cube",
                new PositionComponent(new Vector3(20f, 5, -50)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(10f)),
                new ModelComponent(glLoader.loadToVAO(boxMeshLeaf)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            Mesh cubeMeshTree = MeshGenerator.generateBox(new VertexMaterial(TextureGenerator.oakBranch));
            world.CreateEntity("tree texture cube",
                new PositionComponent(new Vector3(40, 5, -50)),
                new RotationComponent(new Vector3(0f, 0f, 0f)),
                new ScaleComponent(new Vector3(10f)),
                new ModelComponent(glLoader.loadToVAO(cubeMeshTree)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

    
            float terrainSize = 1000f;




            Mesh RockMesh = IcoSphereGenerator.CreateIcosphere(4, new VertexMaterial(TextureGenerator.rock, new Colour(255, 255, 255)),8);

            OpenSimplexNoise noise = new OpenSimplexNoise();
            for (int i = 0; i < RockMesh.meshVertices.Count; i++)
            {
                Vector3 oldPos = RockMesh.meshVertices[i].position;
                float noiseValue = noise.FBM(oldPos.X, oldPos.Y, oldPos.Z, 0.86f,5);
                Vector3 newPos = oldPos + oldPos * noiseValue * 0.66f;
                RockMesh.meshVertices[i].position = newPos;
            }


            RockMesh.FlatRandomness(0.01f);
            //RockMesh.makeFlat(flatMaterial: true, flatNormal: true);
            glModel rockModel = glLoader.loadToVAO(RockMesh);
            Engine.RenderEngine.textureGenerator.AddImposterToModel(rockModel, 30);

            for (int i = 0; i < 4000; i++)
            {
                Vector3 treePos = new Vector3(MyMath.rng(terrainSize), 0, MyMath.rng(terrainSize));
                treePos.Y = terrainGenerator.getHeightAt(treePos.Xz);
                float height = 0.4f + MyMath.rng(2.9f);
                float radius = 0.4f + MyMath.rng(2.8f);
                world.CreateEntity("rock test: " + i,
                    new PositionComponent(treePos),
                    new RotationComponent(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f)),
                    new ScaleComponent(new Vector3(radius, height, radius)),
                    new ModelComponent(rockModel),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent()
                );
            }

            
            spawnModelOverTerrain(2000, glLoader.loadToVAO(TreeGenerator.GenerateFern()), 30f);
            spawnModelOverTerrain(1000, glLoader.loadToVAO(TreeGenerator.GenerateFlowerBush(new Vector3(6f, 1f, 1f))), 30f);  // yellow
            spawnModelOverTerrain(1000, glLoader.loadToVAO(TreeGenerator.GenerateFlowerBush(new Vector3(10f, 0.1f, 0.1f))), 30f);  // red
            spawnModelOverTerrain(1000, glLoader.loadToVAO(TreeGenerator.GenerateFlowerBush(new Vector3(10f, 1f, 10f))), 30f);  // purple
            spawnModelOverTerrain(2000, glLoader.loadToVAO(TreeGenerator.GenerateFlowerBush(new Vector3(3.5f, 2.5f, 2.5f))), 30f);  //white
            spawnModelOverTerrain(2000, glLoader.loadToVAO(TreeGenerator.generatePineTree(45, 0.35f, alive: true, fallen: false)));
            spawnModelOverTerrain(1900, glLoader.loadToVAO(TreeGenerator.generatePineTree(25, 0.5f, alive: true, fallen: false)));
            spawnModelOverTerrain(1900, glLoader.loadToVAO(TreeGenerator.generatePineTree(10, 0.7f, alive: true, fallen: false)));
            spawnModelOverTerrain(1500, glLoader.loadToVAO(TreeGenerator.generatePineTree(15, 0.35f, alive: false, fallen: false)));

            spawnModelOverTerrain(400, glLoader.loadToVAO(TreeGenerator.GenerateFallenPineTree()));
            spawnModelOverTerrain(500, glLoader.loadToVAO(TreeGenerator.GenerateBirchTree()));

            
        }
       
        private void spawnModelOverTerrain(int n, glModel model, float imposterDistance = 60f)
        {
            float terrainSize = 1000f;
            ECSWorld world = Engine.world;
            Engine.RenderEngine.textureGenerator.AddImposterToModel(model, imposterDistance);
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = new Vector3(MyMath.rng(terrainSize), 0, MyMath.rng(terrainSize));
                pos.Y = terrainGenerator.getHeightAt(pos.Xz);
                world.CreateEntity("auto spawned model"+model.ToString()+" " + i,
                    new PositionComponent(pos),
                    new RotationComponent(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f)),
                    new ScaleComponent(new Vector3(1f+MyMath.rngMinusPlus(0.3f))),
                    new ModelComponent(model),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent()
                );
            }
        }



        private void spawnTerrain(ECSWorld world)
        {


            /*
            DebugRenderer.texture = terrainSteepnessMap.GetTexture();
            //DebugRenderer.texture = spawnGrid.GenerateTexture();

            //PoissonDiskSampling poissonDiskSampling = new PoissonDiskSampling(terrainSteepnessMap, 300f);

            BetterNoiseSampling betterNoiseSampling = new BetterNoiseSampling(terrainGrid.Resolution);

            //List<Vector2> spawnPoints = poissonDiskSampling.GeneratePoints();
            List<Vector2> spawnPoints = betterNoiseSampling.GeneratePoints(spawnGrid);
            */
            /*
            Mesh mesh = treeGenerator.GenerateFractalTree(2);
            mesh.makeFlat(true, true);
            mesh.scale(new Vector3(8f));
            glModel treeModel = glLoader.loadToVAO(mesh);
            int i = 0;
            foreach (Vector2 spawn in spawnPoints)
            {
                Entity tree = new Entity("tree");
                float y = terrainGrid.BilinearInterpolate(spawn);
                if (y < 0) continue;
                if (y > 45) continue;
                tree.addComponent(new TransformationComponent(new Vector3(spawn.X, y - 0.1f, spawn.Y), new Vector3(0, MyMath.rng(MathF.PI * 2f), 0f), new Vector3(0.5f + MyMath.rng(0.5f))));
                tree.addComponent(new ModelComponent(treeModel));
                eCSEngine.AddEnityToSystem<InstancedModelSystem>(tree);
                tree.addComponent(new ChildComponent(groundPlane));
                tree.addComponent(new SelfDestroyComponent(1 + MyMath.rng(2f)));
                RenderEngine._debugRenderer.circles.Add(new Circle(spawn, 1f));
                i++;
            }
            Console.WriteLine(i+" trees");

            Mesh rockMesh = IcoSphereGenerator.CreateIcosphere(2, Material.ROCK);
            rockMesh.FlatRandomness(0.1f);
            rockMesh.makeFlat(true, true);
            glModel rockModel = glLoader.loadToVAO(rockMesh);
            spawnPoints = betterNoiseSampling.GeneratePoints(terrainSteepnessMap);
            */

            /*
            Mesh rockMesh2 = new Mesh();
            foreach (Vector2 spawn in spawnPoints)
            {

                float y = terrainGrid.BilinearInterpolate(spawn);
                Vector3 position = new Vector3(spawn.X, y - 0.1f, spawn.Y);
                Vector3 scale = new Vector3(new Vector3(0.5f) + MyMath.rng3D(2.0f));
                scale += new Vector3(1f - terrainSteepnessMap.BilinearInterpolate(position.Xz)) * 1f;
                Transformation transformation = new Transformation(position, MyMath.rng3D(MathF.PI * 2f), scale);

                rockMesh2 += rockMesh.Transformed(transformation);

                RenderEngine._debugRenderer.circles.Add(new Circle(spawn, 1f));
            }
            Entity rock = new Entity("rock");
            //if (y < 0) continue;
            //if (y > 45) continue;

            rock.addComponent(new TransformationComponent(new Transformation()));
            rock.addComponent(new ModelComponent(rockMesh2));
            rock.addComponent(new ChildComponent(groundPlane));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(rock);
            */


            /*
            foreach (Vector2 spawn in spawnPoints)
            {
                Entity rock = new Entity("rock");
                float y = terrainGrid.BilinearInterpolate(spawn);
                //if (y < 0) continue;
                //if (y > 45) continue;
                Vector3 position = new Vector3(spawn.X, y - 0.1f, spawn.Y);
                Vector3 scale = new Vector3(new Vector3(0.5f) + MyMath.rng3D(2.0f));
                scale += new Vector3(1f - terrainSteepnessMap.BilinearInterpolate(position.Xz)) * 1f;

                rock.addComponent(new TransformationComponent(position, MyMath.rng3D(MathF.PI * 2f), scale));
                rock.addComponent(new ModelComponent(rockModel));
                rock.addComponent(new ChildComponent(groundPlane));
                rock.addComponent(new CollisionComponent(new SphereHitbox(scale.X)));
                eCSEngine.AddEnityToSystem<InstancedModelSystem>(rock);
                eCSEngine.AddEnityToSystem<CollidableSystem>(rock);
                RenderEngine._debugRenderer.circles.Add(new Circle(spawn, 1f));
            }
            */
        }

        /*
        private void spawnIndoorScene(ECSEngine eCSEngine)
        {
            Vector3 roomSize = new Vector3(20, 20, 50);
            float wallThickness = 0.05f;
            EntityOLD house = new EntityOLD("house");
            house.addComponent(new TransformationComponent(new Transformation(new Vector3(0, 0, 0), new Vector3(0), new Vector3(1))));
            Mesh houseMesh = new Mesh();
            Material wood = new Material(new Colour(115, 115, 95, 1), Engine.RenderEngine.textureGenerator.flat);
            Material floor = new Material(new Colour(155, 135, 111, 1), Engine.RenderEngine.textureGenerator.grain);
            Mesh plane = MeshGenerator.generateBox(wood);
            Mesh floorMesh = MeshGenerator.generateBox(floor);
            Mesh.scaleUV = true;
            floorMesh.scale(new Vector3(1f, 1f, wallThickness));
            floorMesh.translate(new Vector3(0f, 0f, 0.5f));
            Mesh.scaleUV = false;
            plane.scale(new Vector3(1f, 1f, wallThickness));
            plane.translate(new Vector3(0f, 0f, 0.5f));
            houseMesh += floorMesh.rotated(new Vector3(MathF.PI / 2f, 0f, 0f));
            houseMesh += plane.rotated(new Vector3(-MathF.PI / 2f, 0f, 0f));
            houseMesh += plane.rotated(new Vector3(0f, 0, 0f)).scaled(new Vector3(1f, 0.3f, 1f)).translated(new Vector3(0f, -0.7f/2f, 0f));
            houseMesh += plane.rotated(new Vector3(0f, 0, 0f)).scaled(new Vector3(1f, 0.3f, 1f)).translated(new Vector3(0f, 0.7f / 2f, 0f));
            houseMesh += plane.rotated(new Vector3(0f, MathF.PI / 2f, 0f));
            houseMesh += plane.rotated(new Vector3(0f, 2f*MathF.PI / 2f, 0f));
            houseMesh += plane.rotated(new Vector3(0f, 3f*MathF.PI / 2f, 0f));
            houseMesh.translate(new Vector3(0f, .5f- wallThickness / 2f, 0f));

            Mesh.scaleUV = true;
            houseMesh.scale(roomSize);
            Mesh.scaleUV = false;
            house.addComponent(new ModelComponent(houseMesh));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(house);


            TerrainGenerator terrainGenerator = new TerrainGenerator(104);
            Vector2 terrainSize = new Vector2(100, 140f);
            EntityOLD terrain = terrainGenerator.generateTerrainChunkEntity(new Vector2(0, 0f), terrainSize, 1.0f);
            terrain.getComponent<TransformationComponent>().SetLocalTransformation(new Vector3(-terrainSize.X/2f, -40, 20f));
            terrain.addComponent(new ChildComponent(house));


            EntityOLD roundTable = new EntityOLD("roundTable");
            roundTable.addComponent(new TransformationComponent(new Transformation(new Vector3(-7, 0, -21), new Vector3(0, 0, 0f), new Vector3(1))));
            roundTable.addComponent(new ChildComponent(house));
            roundTable.addComponent(new ModelComponent(FurnitureGenerator.GenerateRoundTable(out float roundTableSurfaceHeight))); ;
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(roundTable);

            EntityOLD candle2 = new EntityOLD("candle2");
            candle2.addComponent(new TransformationComponent(new Transformation(new Vector3(0.2f, roundTableSurfaceHeight, -0.2f), new Vector3(0f, 0f, 0f), new Vector3(1))));
            candle2.addComponent(new ModelComponent(FurnitureGenerator.GenerateCandle(out Vector3 candleLightPosition2)));
            candle2.addComponent(new ChildComponent(roundTable));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(candle2);

            EntityOLD candlePointLight2 = new EntityOLD("candlePointLight2");
            candlePointLight2.addComponent(new TransformationComponent(new Transformation(candleLightPosition2, new Vector3(0), new Vector3(1))));
            candlePointLight2.addComponent(new ChildComponent(candle2));
            candlePointLight2.addComponent(new AttunuationComponent(0.004f, 0.004f, 0.004f));
            candlePointLight2.addComponent(new ColourComponent(new Colour(1f, 0.7f, 0.5f, 0.2f)));
            eCSEngine.AddEnityToSystem<PointLightSystem>(candlePointLight2);

            EntityOLD table = new EntityOLD("table");
            table.addComponent(new TransformationComponent(new Transformation(new Vector3(6.4f, 0, 12), new Vector3(0, 0, 0f), new Vector3(1))));
            table.addComponent(new ModelComponent(FurnitureGenerator.GenerateTable(out float tableSurfaceHeight)));
            table.addComponent(new ChildComponent(house));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(table);

            EntityOLD chair = new EntityOLD("chair");
            chair.addComponent(new TransformationComponent(new Transformation(new Vector3(-3, 0, 0), new Vector3(0, -MathF.PI/2f, 0f), new Vector3(1))));
            chair.addComponent(new ChildComponent(table));
            chair.addComponent(new ModelComponent(FurnitureGenerator.GenerateChair())); ;
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(chair);

            EntityOLD lamp = new EntityOLD("lamp");
            lamp.addComponent(new TransformationComponent(new Transformation(new Vector3(0.8f, tableSurfaceHeight, 3), new Vector3(0f, 0f, 0f), new Vector3(1))));
            lamp.addComponent(new ModelComponent(FurnitureGenerator.GenerateLamp(out Vector3 lightPosition, out Vector3 lightDirection)));
            lamp.addComponent(new ChildComponent(table));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(lamp);

            EntityOLD candle = new EntityOLD("candle");
            candle.addComponent(new TransformationComponent(new Transformation(new Vector3(1.2f, tableSurfaceHeight, -3), new Vector3(0f, 0f, 0f), new Vector3(1))));
            candle.addComponent(new ModelComponent(FurnitureGenerator.GenerateCandle(out Vector3 candleLightPosition)));
            candle.addComponent(new ChildComponent(table));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(candle);

            EntityOLD candlePointLight = new EntityOLD("candlePointLight");
            candlePointLight.addComponent(new TransformationComponent(new Transformation(candleLightPosition, new Vector3(0), new Vector3(1))));
            candlePointLight.addComponent(new ChildComponent(candle));
            candlePointLight.addComponent(new AttunuationComponent(0.004f, 0.004f, 0.004f));
            candlePointLight.addComponent(new ColourComponent(new Colour(1f, 0.3f, 0.1f, 0.7f)));
            eCSEngine.AddEnityToSystem<PointLightSystem>(candlePointLight);

            EntityOLD spotLight = new EntityOLD("spotLight");
            spotLight.addComponent(new TransformationComponent(new Transformation(lightPosition, lightDirection, new Vector3(1))));
            spotLight.addComponent(new ChildComponent(lamp));
            spotLight.addComponent(new AttunuationComponent(0.006f, 0.006f, 0.006f));
            spotLight.addComponent(new ColourComponent(new Colour(1f, 0.7f, 0.5f, 1f)));
            eCSEngine.AddEnityToSystem<SpotLightSystem>(spotLight);


            EntityOLD ceilingLight = new EntityOLD("ceilingLight");
            ceilingLight.addComponent(new TransformationComponent(new Transformation(new Vector3(0, roomSize.Y-wallThickness*roomSize.Y*2, 0), new Vector3(0), new Vector3(1))));
            ceilingLight.addComponent(new ModelComponent(MeshGenerator.generateBox(Material.GLOW)));
            ceilingLight.addComponent(new AttunuationComponent(0.0008f, 0.0008f, 0.0008f));
            ceilingLight.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 1f)));
            ceilingLight.addComponent(new ChildComponent(house));
            eCSEngine.AddEnityToSystem<SpotLightSystem>(ceilingLight);
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(ceilingLight);

        }
        */

        
        private void spawnCity(ECSWorld world)
        {
            glModel houseModel = ModelGenerator.GenerateHouse();
            TreeGenerator treeGenerator = new TreeGenerator();
            StreetGenerator streetGenerator = new StreetGenerator();
            TerrainGenerator terrainGenerator = new TerrainGenerator();

            for (int x = 0; x < 0; x++)
            {
                for (int z = 0; z < 0; z++)
                {
                    var position = new PositionComponent(new Vector3(45 + 55 * x, 0, 45 + 55f * z));
                    var scale = new ScaleComponent(new Vector3(1f));
                    var rotation = new RotationComponent(new Quaternion(1f, 1f, 1f));
                    world.CreateEntity(position, scale, rotation, new ModelRenderTag(), new LocalToWorldMatrixComponent(), new ModelComponent(houseModel));
                }
            }
            Mesh houseGroundMesh = MeshGenerator.generateBox(new VertexMaterial(TextureGenerator.pineBark));
            //Mesh.scaleUV = true;
            //houseGroundMesh.scale(new Vector3(10f, 10f, 10f));
            Mesh.scaleUV = true;
            houseGroundMesh.scale(new Vector3(20f, 10f, 10f));
            houseGroundMesh.rotate(new Vector3(0f, -MathF.PI/2f, 0f));
            houseGroundMesh.scaleUVs(new Vector2(1.0f, 1.0f));
            houseGroundMesh.ProjectUVsWorldSpaceCube(0.25f);
            //Mesh.scaleUV = true;

            world.CreateEntity(
                new PositionComponent(new Vector3(-streetGenerator.TotalWidth / 2f - 50f, 0, -streetGenerator.TotalWidth / 2f - 50f)),
                new RotationComponent(new Vector3(0, -MathF.PI / 2f, 0)),
                new ScaleComponent(new Vector3(1)),
                new ModelComponent(glLoader.loadToVAO(houseGroundMesh)),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            world.CreateEntity(
                new PositionComponent(new Vector3(0)),
                new RotationComponent(new Vector3(0)),
                new ScaleComponent(new Vector3(1)),
                new ModelComponent(glLoader.loadToVAO(streetGenerator.GenerateCrossRoad())),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent()
            );

            int nr = 0;
            var carModel = glLoader.loadToVAO(CarGenerator.GenerateCar(out Vector3 leftLight, out Vector3 rightLight, out Vector3 exhaustPos));
            Engine.RenderEngine.textureGenerator.AddImposterToModel(carModel, 10);
            for (int i = 2; i < 17; i++)
            {
                for (int j = 0; j < streetGenerator.lanes; j++)
                {
                    float x = streetGenerator.laneWdith * j + streetGenerator.laneWdith * (0.5f + MyMath.rngMinusPlus(0.15f));
                    float z = 13f * i + MyMath.rngMinusPlus(4f);
                    Entity car = world.CreateEntity(
                        new PositionComponent(new Vector3(x, 0f, z)),
                        new RotationComponent(new Vector3(0f, MyMath.rngMinusPlus(0.03f), 0f)),
                        new ScaleComponent(new Vector3(1.0f + MyMath.rngMinusPlus(0.2f))),
                        new ModelComponent(carModel),
                        new ModelRenderTag(),
                        new LocalToWorldMatrixComponent()
                    );

                    for (int carLight = 0; carLight <2; carLight++)
                    {
                        Vector3 carLightPos = leftLight;
                        if (carLight == 1) carLightPos = rightLight;
                        float carLightFoV = MathF.PI / 2.0f;
                        var spotLight = world.CreateEntity(
                            new PositionComponent(carLightPos),
                            new DirectionNormalizedComponent(new Vector3(0f, -0.3f, -1f)),
                            new AttunuationComponent(0.001f, 0.001f, 0.001f),
                            new ColorComponent(new Colour(1f, 0.8f, 0.6f, 0.3f)),
                            new LocalToWorldMatrixComponent(),
                            new ParentComponent(car),
                            new SpotLightComponent(0.3f, carLightFoV)
                        );
                        if (i == 2)
                        {
                            //world.ApplyDeferredCommands();
                            //world.AddComponentToEntity(spotLight, new SpotlightShadowComponent(new Vector2i(256), carLightFoV));
                        }
                    }
                    
                    var emitterComponent = new ParticleEmitterComponent();
                    emitterComponent.particleSpeed = 0.2f;
                    emitterComponent.particlesPerSecond = 0.1f;
                    emitterComponent.particleSizeStart = 0.25f;
                    emitterComponent.particleWeight = -0.25f;
                    emitterComponent.particleSpeed = 6f;
                    emitterComponent.particleDuration = 1.4f;
                    emitterComponent.particleDirectionError = 0.3f;
                    emitterComponent.particlePositionError = 0.1f;
                    world.CreateEntity(
                        new PositionComponent( exhaustPos),
                        new DirectionNormalizedComponent(new Vector3(0f, 0f, 1f)),
                        emitterComponent,
                        new LocalToWorldMatrixComponent(),
                        new ParentComponent(car),
                        new LocalToWorldMatrixComponent()
                    );

                    nr++;
                }
            }

            Mesh streetModel = streetGenerator.GenerateStreet(30, out float streetLength);
            var glStreetModel = glLoader.loadToVAO(streetModel);
            for (int i = 0; i < 4; i++)
            {
                Vector3 position = (new Vector4(0, 0f, streetGenerator.TotalWidth * 0.5f, 1f) * MyMath.createRotationMatrix(new Vector3(0f, i * (MathF.PI / 2f), 0f))).Xyz;
                world.CreateEntity(
                    new PositionComponent(position),
                    new RotationComponent(new Vector3(0f, i * (MathF.PI / 2f), 0f)),
                    new ScaleComponent(new Vector3(1)),
                    new ModelComponent(glStreetModel),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent()
                );
            }


            var tree = glLoader.loadToVAO(TreeGenerator.GenerateBirchTree());
            var streeLight = UrbanPropGenerator.GenerateStreetLight(out Vector3 lightPosition);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 10; i++)
                {
                    var streetLightPos = new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 50 + 30 * i);
                    var streeLightEntity = world.CreateEntity(
                        new PositionComponent(streetLightPos),
                        new RotationComponent(new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f)),
                        new ScaleComponent(new Vector3(1f, 1f, 1f)),
                        new ModelComponent(streeLight),
                        new ModelRenderTag(),
                        new LocalToWorldMatrixComponent()
                    );

                    world.CreateEntity(
                        new PositionComponent(lightPosition),
                        new DirectionNormalizedComponent(new Vector3(0f, -1f, 0f)),
                        new AttunuationComponent(0.001f, 0.001f, 0.001f),
                        new ColorComponent(new Colour(1f, 0.8f, 0.6f, 20f)),
                        new LocalToWorldMatrixComponent(),
                        new ParentComponent(streeLightEntity),
                        new LocalToWorldMatrixComponent(),
                        new SpotLightComponent(0.3f, MathF.PI*0.4f)
                    );

                    world.CreateEntity(
                        new PositionComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 35 + 30 * i)),
                        new RotationComponent(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f)),
                        new ScaleComponent(new Vector3(1f) + MyMath.rng3DMinusPlus(0.25f)),
                        new ModelComponent(tree),
                        new ModelRenderTag(),
                        new LocalToWorldMatrixComponent()
                    );
                }
            }
            var roadConeModel = glLoader.loadToVAO(UrbanPropGenerator.GenerateStreetCone());
            for (int i = 0; i < 8; i++)
            {
                world.CreateEntity(
                    new PositionComponent(new Vector3(2 * i, 0f, 15f)),
                    new RotationComponent(new Vector3(0f, MathF.PI * 2f * MyMath.rng(), 0f)),
                    new ScaleComponent(new Vector3(2)),
                    new ModelComponent(roadConeModel),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent()
                );

            }
        }

        
    }
}
