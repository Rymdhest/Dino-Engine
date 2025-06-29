﻿using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Util;
using Dino_Engine.Util.Noise;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Util.Noise;
using Dino_Engine.Rendering;
using Dino_Engine.Debug;
using System.Diagnostics;
using System.Security.Cryptography;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util.Data_Structures.Grids;

namespace Dino_Engine.ECS
{
    public class ECSEngine
    {

        private Dictionary<Type, ComponentSystem> _systems = new Dictionary<Type, ComponentSystem>();
        public Dictionary<Type, ComponentSystem> Systems { get => _systems; }

        private Entity _camera = null;
        public Entity Camera { get => _camera; }
        public List<Entity> Entities { get => getSystem<AllEntitySystem>().MemberEntities;}

        public ECSEngine()
        {
            AddSystem<AllEntitySystem>();
            AddSystem<ModelRenderSystem>();
            AddSystem<TerrainRenderSystem>();
            AddSystem<InstancedModelSystem>();
            AddSystem<DirectionalLightSystem>();
            AddSystem<SpotLightSystem>();
            AddSystem<PointLightSystem>();
            AddSystem<SelfDestroySystem>();
            AddSystem<ParticleEmitterSystem>();
            AddSystem<ParticleSystem>();
            AddSystem<TerrainSystem>();
            AddSystem<VelocitySystem>();
            AddSystem<GravitySystem>();
            AddSystem<CollidableSystem>();
            AddSystem<CollidingSystem>();
            AddSystem<GrassBlastSystem>();
            AddSystem<GrassInteractSystem>();
        }

        private void AddSystem<T>() where T : ComponentSystem, new()
        {
            _systems.Add(typeof(T), new T());
        }

        public void InitEntities()
        {
            Engine.PerformanceMonitor.clear();

            if (_camera == null)
            {
                _camera = new Entity("Camera");
                Camera.addComponent(new TransformationComponent(new Vector3(0, 130f, 200f), new Vector3(0.5f, 0, 0), new Vector3(1)));
                Camera.addComponent(new ProjectionComponent(MathF.PI / 3.5f));
                Camera.addComponent(new ColourComponent(new Colour(255, 255, 255, 1f)));
                Camera.addComponent(new AttunuationComponent(0.06f, 0.06f, 0.06f));
                AddEnityToSystem<PointLightSystem>(Camera);
            }

            RenderEngine._debugRenderer.circles.Clear();
            RenderEngine._debugRenderer.rings.Clear();
            RenderEngine._debugRenderer.lines.Clear();

            Entity sun = new Entity("Sun");
            Vector3 direction = new Vector3(-1f, 1.5f, 3.9f);
            Colour colour = new Colour(1.0f, 1.0f, 1.0f, 35.0f);   
            sun.addComponent(new ColourComponent(colour));
            sun.addComponent(new DirectionComponent(direction));
            sun.addComponent(new AmbientLightComponent(0.015f));
            sun.addComponent(new CascadingShadowComponent(new Vector2i(1024, 1024) * 3, 4, 1000));
            AddEnityToSystem<DirectionalLightSystem>(sun);

            Entity sky = new Entity("Sky");
            Vector3 skyDirection = new Vector3(0.0f, 1f, 0.0f);
            Colour skyColour = new Colour(SkyRenderer.SkyColour.Red, SkyRenderer.SkyColour.Green, SkyRenderer.SkyColour.Blue, .0080f);
            sky.addComponent(new ColourComponent(skyColour));
            sky.addComponent(new DirectionComponent(skyDirection));
            sky.addComponent(new AmbientLightComponent(0.6f));
            sky.addComponent(new CascadingShadowComponent(new Vector2i(512, 512) * 1, 3, 720));
            AddEnityToSystem<DirectionalLightSystem>(sky);

            Engine.PerformanceMonitor.StatusReportDump();
            Engine.PerformanceMonitor.clear();
        }
        public bool AddEnityToSystem<T>(Entity entity) where T : ComponentSystem
        {
            getSystem<T>().AddMember(entity);
            return true;
        }

        public void HandleInput()
        {
            float delta = Engine.Delta;
            var transformationComponent = Camera.getComponent<TransformationComponent>();
            Transformation transformation = transformationComponent.Transformation;
            WindowHandler windowHandler = Engine.WindowHandler;

            float moveAmount = 20f * delta;
            float turnAmount = 2.5f * delta;
            float mouseTurnAmount = 0.001f;

            if (windowHandler.IsKeyDown(Keys.LeftShift))
            {
                moveAmount *= 10f;
            }
            if (windowHandler.IsKeyDown(Keys.LeftControl))
            {
                moveAmount *= 0.1f;
            }

            if (windowHandler.IsMouseButtonDown(MouseButton.Right))
            {
                transformation.addRotation(new Vector3(0f, mouseTurnAmount * windowHandler.MouseState.Delta.X, 0f));
                transformation.addRotation(new Vector3(mouseTurnAmount * windowHandler.MouseState.Delta.Y, 0, 0f));
                windowHandler.setMouseGrabbed(true);
                if (windowHandler.IsKeyDown(Keys.A))
                {
                    transformation.move(new Vector3(-moveAmount, 0f, 0f));
                }
                if (windowHandler.IsKeyDown(Keys.D))
                {
                    transformation.move(new Vector3(moveAmount, 0f, 0f));
                }
            }
            else
            {
                windowHandler.setMouseGrabbed(false);
                if (windowHandler.IsKeyDown(Keys.A))
                {
                    transformation.addRotation(new Vector3(0f, -turnAmount, 0f));
                }
                if (windowHandler.IsKeyDown(Keys.D))
                {
                    transformation.addRotation(new Vector3(0f, turnAmount, 0f));
                }
            }


            if (windowHandler.IsKeyDown(Keys.W))
            {
                transformation.move(new Vector3(0f, 0f, -moveAmount));
            }
            if (windowHandler.IsKeyDown(Keys.S))
            {
                transformation.move(new Vector3(0f, 0f, moveAmount));
            }
            if (windowHandler.IsKeyDown(Keys.Q))
            {
                transformation.translate(new Vector3(0f, -moveAmount, 0f));
            }
            if (windowHandler.IsKeyDown(Keys.E))
            {
                transformation.translate(new Vector3(0f, moveAmount, 0f));
            }
            if (windowHandler.IsKeyDown(Keys.R))
            {
                transformation.addRotation(new Vector3(-turnAmount, 0f, 0f));
            }
            if (windowHandler.IsKeyDown(Keys.F))
            {
                transformation.addRotation(new Vector3(turnAmount, 0f, 0f));
            }


            if (windowHandler.IsKeyPressed(Keys.F5))
            {
                foreach (Entity entity in Entities)
                {
                    Console.WriteLine(entity.GetFullInformationString());
                }
            }

            if (windowHandler.IsKeyPressed(Keys.F12))
            {
                Engine.PerformanceMonitor.StatusReportDump();
            }

            transformationComponent.Transformation = transformation;
        }

        public void ClearAllEntitiesExcept(params Entity[] exceptions)
        {
            var entities = Entities;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                Entity entity = entities[i];
                if (!exceptions.Contains(entity))
                {
                    entity.CleanUp();
                }
            }
        }

        public void update()
        {
            HandleInput();
            foreach (Entity entity in Entities)
            {
                entity.updateComponents();
            }

            foreach (ComponentSystem system in _systems.Values)
            {
                system.Update();
            }
        }

        public T getSystem<T>() where T : ComponentSystem
        {
            if (_systems.TryGetValue(typeof(T), out ComponentSystem system))
            {
                return (T)system;
            } else
            {
                return null;
            }
        }

        public  void OnResize(ResizeEventArgs eventArgs)
        {
            foreach (Entity entity in Entities)
            {
                entity.OnResize(eventArgs);
            }
        }
    }
}
