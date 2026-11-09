#ifndef DINO_GI_GROUND_GLSL
#define DINO_GI_GROUND_GLSL

// The colour of the ground at a point, as GI sees it: Terrain.frag's, from the same chunk textures, with each material taken at its
// mean. Needs GI/GI.glsl, GI/GI_Height.glsl, Grass/GrassField.glsl and GI/GI_Materials.glsl.

uniform sampler2DArray giSurfaceWeightsA;
uniform sampler2DArray giSurfaceWeightsB;
uniform sampler2DArray giSurfaceWeightsC;
uniform sampler2DArray giGrassMap;
uniform sampler2DArray giGrassColourMap;
uniform sampler2DArray giGroundMap;
uniform isampler2D giChunkLayers;       // The texture layer of each live chunk around the camera, -1 for none.
uniform vec4 giSurfaceMaterials[3];
uniform ivec2 giChunkOrigin;            // The chunk at texel 0 of giChunkLayers.
uniform float giChunkSize;
uniform float giMapOffset;              // Terrain.frag's textureMapOffset.
uniform vec3 giFallbackGround;          // Where a chunk's textures aren't up.

const float GI_GROUND_LIFT = 0.15;      // TerrainRenderer.SurfaceOffset.

vec3 giMaterialMean(float material, vec3 at)
{
    if (isRock(material)) return geologyToneAt(geologyLayerAt(at)).rgb;
    vec3 mean = textureLod(albedoMapTextureArray, vec3(0.5, 0.5, material), 16.0).rgb;
    if (isScree(material)) mean *= geologyColour.rgb;
    return mean;
}

vec3 giGroundAlbedo(vec2 xz, float height, float size, out bool live)
{
    ivec2 chunk = ivec2(floor(xz / giChunkSize));
    ivec2 at = chunk - giChunkOrigin;
    ivec2 extent = textureSize(giChunkLayers, 0);
    int layer = -1;
    if (all(greaterThanEqual(at, ivec2(0))) && all(lessThan(at, extent))) layer = texelFetch(giChunkLayers, at, 0).r;
    live = layer >= 0;
    if (!live) return giFallbackGround;

    vec2 local = clamp((xz - vec2(chunk) * giChunkSize) / giChunkSize, 0.0, 1.0);
    vec3 coord = vec3(local * (1.0 - giMapOffset) + vec2(giMapOffset * 0.5), float(layer));
    vec4 a = textureLod(giSurfaceWeightsA, coord, 0.0);
    vec4 b = textureLod(giSurfaceWeightsB, coord, 0.0);
    vec4 c = textureLod(giSurfaceWeightsC, coord, 0.0);
    float weights[12] = float[](a.x, a.y, a.z, a.w, b.x, b.y, b.z, b.w, c.x, c.y, c.z, c.w);
    float materials[12] = float[](giSurfaceMaterials[0].x, giSurfaceMaterials[0].y, giSurfaceMaterials[0].z, giSurfaceMaterials[0].w,
                                  giSurfaceMaterials[1].x, giSurfaceMaterials[1].y, giSurfaceMaterials[1].z, giSurfaceMaterials[1].w,
                                  giSurfaceMaterials[2].x, giSurfaceMaterials[2].y, giSurfaceMaterials[2].z, giSurfaceMaterials[2].w);

    vec3 point = vec3(xz.x, height, xz.y);
    vec3 sum = vec3(0.0);
    float total = 0.0;
    float rock = 0.0;
    for (int i = 0; i < 12; i++)
    {
        if (weights[i] <= 0.0) continue;
        sum += giMaterialMean(materials[i], point) * weights[i];
        total += weights[i];
        if (isRock(materials[i])) rock += weights[i];
    }
    vec3 ground = total > 0.0 ? sum / total : giFallbackGround;

    // Bare rock is darkest in its cracks and palest on its edges, as Terrain.frag has it.
    float relief = textureLod(giGroundMap, coord, 0.0).z;
    if (total > 0.0 && rock > 0.0)
    {
        vec3 shaded = ground * mix(0.4, 1.0, smoothstep(-0.72, 0.0, relief)) * mix(1.0, 1.32, smoothstep(0.04, 0.72, relief));
        ground = mix(ground, shaded, rock / total);
    }

    float step_ = max(size, 1.0);
    float east = giGroundHeight(xz + vec2(step_, 0.0)), west = giGroundHeight(xz - vec2(step_, 0.0));
    float north = giGroundHeight(xz + vec2(0.0, step_)), south = giGroundHeight(xz - vec2(0.0, step_));
    float normalY = normalize(vec3(west - east, 2.0 * step_, south - north)).y;

    vec4 grass = textureLod(giGrassMap, coord, 0.0);
    vec3 grassColour = textureLod(giGrassColourMap, coord, 0.0).rgb;
    float fertility = grassFertility(grass.r, normalY, relief);
    return grassGroundTint(ground, xz, fertility, 1e5, grassColour, grass.b);
}

#endif
