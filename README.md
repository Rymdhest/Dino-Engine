# Dino Engine

**Language:** C# | **Rendering:** OpenGL (via OpenTK) | **Status:** In active development  

## Showcase


<img width="1907" height="1079" alt="engine grass" src="https://github.com/user-attachments/assets/4e0510b4-1bb6-4804-9bf7-9107c65fa2a1" />

---

<img width="1907" height="1074" alt="engine 2" src="https://github.com/user-attachments/assets/4ccadb53-51c7-4a29-9544-47a7f78602a3" />

This is a fully **procedural game engine** built from scratch in C# using OpenGL and OpenTK. The engine does **not use any external libraries** beyond OpenTK. Every asset, from textures and models to terrain and effects, is generated **procedurally at runtime**, making it a showcase of graphics programming and system design skills.

---

## Key Features

### Core Systems
- **Entity-Component-System (ECS)** architecture for flexible and efficient scene management

### Rendering
- **Deferred shading pipeline**
- **Physically Based Rendering (PBR)**
- **Normal mapping**
- **parallax mapping**
- **Screen Space Ambient Occlusion**
- **Screen Space Reflections**
- **animated, height-based fog**

### Lighting
- **Directional lights** with **cascaded shadow mapping**
- **Point and spot lights with shadow mapping**
- **Light rays** for volumetric effects
- **Geometric subsurface scattering**
- **Emissive materials**

### Procedural Content
- **Procedural terrain** and landscape generation
- **Procedural modeling** for all objects
- **Procedural textures** for all textures
- **Grass shader** lightweigth zero per instance data
- **Water shader** with procedural waves and reflections
- **Cloud and sky shaders** for dynamic environments

### Other Post-processing Effects
- **FXAA anti-aliasing**
- **Depth of Field**
-  **bloom** (up/down-sampling)
- **HDR tone-mapping**
---

