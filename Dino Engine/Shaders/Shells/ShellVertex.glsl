#ifndef DINO_SHELL_VERTEX_GLSL
#define DINO_SHELL_VERTEX_GLSL

// A layer of shells over whatever it is that grows something (SHELLS.md): the surface pushed out along its own way out by this layer's share of the length,
// with the tips leaning back along the comb, hanging down and going with the wind by the square of how far up they are, as a strand bends. One draw covers
// every layer of every instance: the instance number says both, and the layers go outermost first, so that what a layer hides is never shaded under it.
//
// SHELL_INSTANCED is what InstancedModelRenderer draws (stone, rock skin, trees, which bend as Plant.glsl says), SHELL_CHARACTER and SHELL_WILDLIFE what's
// skinned, and SHELL_TERRAIN the ground, its chunks drawn as Terrain.vert draws them. Each kind is a file of its own (Shell_Instanced.vert and the rest) that says
// which it is and declares only its own uniforms, as ShaderProgram looks up every uniform it's given, whatever an #ifdef says, and includes this.

#if defined(SHELL_CHARACTER) || defined(SHELL_WILDLIFE)
#define SHELL_SKINNED
#endif

#include Globals.glsl
#include Shells/ShellStyle.glsl
#include Geology.glsl
// Plant.glsl (for what bends) or Wind.glsl comes in from each kind's own file, with its uniforms.

#ifdef SHELL_TERRAIN
layout(location = 0) in vec3 position;
layout(location = 3) in vec3 instanceChunkPos;
layout(location = 4) in vec3 instanceChunkSize;
layout(location = 5) in float instanceHeightMapID;
#else
layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 12) in vec4 shellData;    // Which way it's pushed out, in the model's space, and how much grows here (glModel.ShellAttribute).
#endif
#ifdef SHELL_INSTANCED
layout(location = 6) in mat4 modelMatrix;   // Advanced once for every shellCount instances.
layout(location = 10) in float joint;
#endif
#if defined(SHELL_CHARACTER) || defined(SHELL_WILDLIFE)
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;
#endif

#ifdef SHELL_WILDLIFE
layout(std430, binding = 3) readonly buffer WildlifeBones {
    mat4 wildlifeBones[];
};
layout(std430, binding = 4) readonly buffer WildlifeTints {
    vec4 wildlifeTints[];
};
#endif

uniform int shellStyle;
uniform int shellCount;
uniform int numberOfMaterials;
uniform vec2 shellFadeRange;        // Metres from the camera at which it starts to lie down, and where it's gone.

// Only what each kind needs, as every one of these is interpolated for every pixel of every layer. The ground's anchor is where it's drawn and its own normal,
// and nothing that stands still is combed or turned.
#ifndef SHELL_TERRAIN
out vec3 shellAnchor;               // The point of the surface this stands over, in the space its pattern is fixed in (metres).
out vec3 shellAnchorNormal;         // The surface's normal there, in the same space.
out vec3 shellColour;               // The colour of what it grows from, as its vertices have it: its material's texture is read where it's drawn.
out vec2 shellUV;
flat out float shellMaterial;
#endif
#ifdef SHELL_SKINNED
out vec3 shellComb;                 // The way it's combed along the surface, in the same space.
out vec3 shellToWorldX;             // The anchor's space turned into the world's, a column at a time.
out vec3 shellToWorldY;
out vec3 shellToWorldZ;
#endif
out vec3 shellViewPos;
out vec3 shellNormal;               // The surface's normal as it's drawn, in the world.
out float shellAmount;              // How much grows here, by the vertices alone.
out float shellLength;              // Metres the layers span here.
out float shellRock;                // 1 on the world's rock.
flat out int shellLayer;
flat out float shellHeight;         // This layer's height, a share of the length.
#ifdef SHELL_INSTANCED
flat out float shellFocusOccluder;
#endif
#ifdef SHELL_WILDLIFE
flat out float shellFade;
#endif

// The cofactors are the inverse transpose times the determinant: a normal's matrix, up to a scale everything normalises away. Its sign matters on what's mirrored.
mat3 shellNormalMatrix(mat3 m)
{
    mat3 c = mat3(cross(m[1], m[2]), cross(m[2], m[0]), cross(m[0], m[1]));
    return dot(m[0], c[0]) < 0.0 ? -c : c;
}

#ifdef SHELL_TERRAIN
// A slot's share of everything the ground is made of here, as TerrainRenderer.ShellCoverOf weighs it: the weights aren't normalised.
float shellGroundShare(int slot, vec4 a, vec4 b, vec4 c, float total)
{
    if (slot < 0 || total <= 1e-4) return 0.0;
    vec4 w = slot < 4 ? a : slot < 8 ? b : c;
    return w[slot & 3] / total;
}
#endif

// Fur lies back along a body and down its flanks and legs: the way backwards and down, laid on the surface. Model space is +z forward and +y up.
vec3 shellCombOf(vec3 n)
{
    vec3 back = vec3(0.0, -0.55, -1.0);
    vec3 c = back - n * dot(back, n);
    if (dot(c, c) < 0.01)
    {
        vec3 down = vec3(0.0, -1.0, 0.0);
        c = down - n * dot(down, n);
        if (dot(c, c) < 0.01) c = vec3(1.0, 0.0, 0.0) - n * n.x;
    }
    return normalize(c);
}

void main()
{
    vec4 shape = shellRow(shellStyle, 0);
    vec4 motion = shellRow(shellStyle, 5);
    bool fur = shellRow(shellStyle, 2).w > 0.5;

    // Outermost first.
#if defined(SHELL_INSTANCED) || defined(SHELL_WILDLIFE) || defined(SHELL_TERRAIN)
    int object = gl_InstanceID / shellCount;
    int layer = gl_InstanceID - object * shellCount;
#else
    int layer = gl_InstanceID;
#endif
    // The layers go up from the style's floor: below it they'd only ever be inside what grows, hidden by the layers over them.
    int k = shellCount - 1 - layer;
    float floor_ = shellRow(shellStyle, 6).z;
    float h = floor_ + (1.0 - floor_) * float(k + 1) / float(shellCount);
    shellLayer = k;
    shellHeight = h;

    vec3 world;             // Where the surface is drawn.
    vec3 outward;           // Which way the layers go from it, in the world, as long as a crease needs.
    vec3 worldNormal;
    mat3 toWorld = mat3(1.0);
    vec3 anchor;
    vec3 anchorNormal;
    vec3 colour;
    float amount;
    shellRock = 0.0;

#ifdef SHELL_TERRAIN
    vec3 worldPos = position * instanceChunkSize + instanceChunkPos;
    vec3 chunkCoord = vec3(position.xz * (1.0 - textureMapOffset) + vec2(textureMapOffset / 2.0), instanceHeightMapID);
    vec4 texel = textureLod(normalHeightTextureArray, chunkCoord, 0.0);
    worldPos.y += (texel.a + 0.15) * instanceChunkSize.y;   // Terrain.vert's drawn ground.
    world = worldPos;
    worldNormal = normalize(vec3(texel.x, sqrt(max(0.0, 1.0 - (texel.x * texel.x + texel.y * texel.y))), texel.y));
    outward = worldNormal;
    anchor = world;
    anchorNormal = worldNormal;
    colour = vec3(1.0);
    // What the ground is made of here, read where the vertex is, which is a texel's middle: the chunk's maps have a texel to a vertex.
    vec4 wa = textureLod(surfaceWeightsA, chunkCoord, 0.0);
    vec4 wb = textureLod(surfaceWeightsB, chunkCoord, 0.0);
    vec4 wc = textureLod(surfaceWeightsC, chunkCoord, 0.0);
    float total = dot(wa, vec4(1.0)) + dot(wb, vec4(1.0)) + dot(wc, vec4(1.0));
    amount = shellGroundShare(groundSlots.x, wa, wb, wc, total);
    shellRock = shellGroundShare(groundSlots.y, wa, wb, wc, total);
    // Where something walks, it's pressed down, and comes up again as what's walking through plants lets go (Wind_Vegetation.frag).
    vec4 touch = vegetationTouch(touchMap, world.xz);
    amount *= 1.0 - 0.55 * smoothstep(0.05, 0.6, touch.z);
#else
    vec3 dir = dot(shellData.xyz, shellData.xyz) > 1e-6 ? shellData.xyz : normal;
    amount = shellData.w;
    bool card = int(materialIndex + 0.5) >= numberOfMaterials;
    colour = color;
    if (card) amount = 0.0;
    shellRock = isRock(materialIndex) ? 1.0 : 0.0;
    shellUV = uv;
    shellMaterial = materialIndex;
#endif

#ifdef SHELL_INSTANCED
    vec3 pose = position;
    vec3 N = normal;
    vec3 D = dir;
    if (plant == 1 && joint > -0.5) plantPose(int(joint + 0.5), modelMatrix, pose, N, D);
    mat3 m = mat3(modelMatrix);
    mat3 normals = shellNormalMatrix(m);
    world = (modelMatrix * vec4(pose, 1.0)).xyz;
    worldNormal = normalize(normals * N);
    outward = normalize(normals * D) * length(D);
    // The pattern is fixed where the surface rests, in the world: a stone's moss is the size moss is, whatever the stone's.
    anchor = (modelMatrix * vec4(position, 1.0)).xyz;
    anchorNormal = normalize(normals * normal);
    if (plant == 1) colour *= plantTint(modelMatrix[3].xz);
    shellFocusOccluder = focusOpens > 0.5 ? focusOccluder(modelSize, modelMatrix) : 0.0;
#endif

#if defined(SHELL_CHARACTER) || defined(SHELL_WILDLIFE)
    float total = boneWeights.x + boneWeights.y;
    vec2 weights = total > 0.001 ? boneWeights / total : vec2(1.0, 0.0);
#ifdef SHELL_CHARACTER
    int a = int(boneIndices.x + 0.5);
    int b = int(boneIndices.y + 0.5);
    mat4 skin = weights.y > 0.0 ? u_Bones[a] * weights.x + u_Bones[b] * weights.y : u_Bones[a];
    mat4 place = modelMatrix * skin;
#else
    int first = firstBone + object * bonesEach;
    int a = first + int(boneIndices.x + 0.5);
    int b = first + int(boneIndices.y + 0.5);
    mat4 place = weights.y > 0.0 ? wildlifeBones[a] * weights.x + wildlifeBones[b] * weights.y : wildlifeBones[a];
    vec4 tint = wildlifeTints[firstTint + object];
    colour *= tint.rgb;
    shellFade = tint.a;
#endif
    mat3 m = mat3(place);
    mat3 normals = shellNormalMatrix(m);
    world = (place * vec4(position, 1.0)).xyz;
    worldNormal = normalize(normals * normal);
    outward = normalize(normals * dir) * length(dir);
    // The pattern is fixed to the body as it was built, at the size it's drawn: it goes wherever the body goes. The size is the instance's, never a blend of two
    // bones', which is shorter than either where they're turned apart and would stretch the pattern over every joint.
#ifdef SHELL_CHARACTER
    float size = length(modelMatrix[0].xyz);
#else
    float size = length(wildlifeBones[first][0].xyz);
#endif
    anchor = position * size;
    anchorNormal = normalize(normal);
    toWorld = mat3(normalize(m[0]), normalize(m[1]), normalize(m[2]));
#endif

    // Lies down to nothing as it goes far off, where nothing could be seen of it anyway.
    float fade = 1.0 - smoothstep(shellFadeRange.x, shellFadeRange.y, distance(world, viewPosWorld));
    amount *= fade;
    // What has nothing growing on it stays where it is, so what's around it is the only part of the layer drawn.
#ifdef SHELL_TERRAIN
    float len = shape.x * (amount + shellRock > 0.0 ? fade : 0.0);
    shellRock *= fade;
#else
    float len = shape.x * (amount > 0.0 ? fade : 0.0);
#endif
    // Moss only grows where Rock.glsl's rule can let it, on whatever faces at all up.
    int mask = int(motion.w + 0.5);
    if (mask == SHELL_MASK_ROCK && (shellRock < 0.5 || anchorNormal.y < 0.15)) len = 0.0;

    vec3 comb = fur ? shellCombOf(anchorNormal) : vec3(0.0);
    vec3 worldComb = toWorld * comb;
    vec3 lift = outward * h;
    // A strand bends from its root, so how far its tip has gone goes with the square of how far up it is.
    float bend = h * h;
    lift += worldComb * (motion.x * bend);
    lift += vec3(0.0, -motion.y * bend, 0.0);
    if (motion.z > 0.0)
    {
        vec2 sway = vegetationBend(bendMap, windMap, world.xz);
        lift += vec3(sway.x, 0.0, sway.y) * (motion.z * bend);
    }
    world += lift * len;

    vec4 view = viewMatrix * vec4(world, 1.0);
    gl_Position = projectionMatrix * view;

#ifndef SHELL_TERRAIN
    shellAnchor = anchor;
    shellAnchorNormal = anchorNormal;
    shellColour = colour;
#endif
#ifdef SHELL_SKINNED
    shellComb = comb;
    shellToWorldX = toWorld[0];
    shellToWorldY = toWorld[1];
    shellToWorldZ = toWorld[2];
#endif
    shellViewPos = view.xyz;
    shellNormal = worldNormal;
    shellAmount = amount;
    shellLength = len;
}

#endif
