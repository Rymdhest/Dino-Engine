#version 420
#include Globals.glsl

// A ghost's skin of light (CharacterRenderer.SpectralPass, Spectral.frag), skinned as any body is (Model_Character.vert).

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;

out vec3 fragWorld;
out vec3 fragNormal;
out vec3 fragBind;      // Where it is on the body as it was built, which is what its light runs through, so that it moves with the body and never swims.
out vec3 fragColour;

uniform mat4 modelMatrix;
uniform mat4 u_Bones[48];

void main()
{
    float total = boneWeights.x + boneWeights.y;
    int a = int(floor(boneIndices.x + 0.5));
    int b = int(floor(boneIndices.y + 0.5));
    mat4 skin = total < 0.001 ? u_Bones[a] : (boneWeights.x / total) * u_Bones[a] + (boneWeights.y / total) * u_Bones[b];

    vec4 world = modelMatrix * (skin * vec4(position, 1.0));
    fragWorld = world.xyz;
    fragNormal = normalize(mat3(modelMatrix) * (transpose(inverse(mat3(skin))) * normal));
    fragBind = position;
    fragColour = color;
    gl_Position = projectionMatrix * (viewMatrix * world);
}
