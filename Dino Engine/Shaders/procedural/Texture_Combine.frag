#version 330

#include procedural/hash.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;

uniform sampler2D writeTextureAlbedo;
uniform sampler2D writeTextureMaterial;

uniform sampler2D readTextureAlbedo;
uniform sampler2D readTextureMaterial;

uniform int filterMode;

uniform int materialOperation;
uniform int heightOperation;

uniform float weight;
uniform float smoothness;

// Abstract core logic into reusable helper functions
vec4 performVec4Operation(int operation, vec4 write, vec4 read, float writeValue, float readValue);
vec3 performVec3Operation(int operation, vec3 write, vec3 read, float writeValue, float readValue);
float performFloatOperation(int operation, float write, float read, float writeValue, float readValue);

float hash(float p) { return hash1D(p);}
vec2 hash(vec2 p) { return hash2D(p);}
vec3 hash(vec3 p) { return hash3D(p);}
vec4 hash(vec4 p) { return hash4D(p);}

// Performs the same logic for all types
#define OPERATION_IMPL(TYPE, FUNC_NAME)                                  \
    TYPE FUNC_NAME(int operation, TYPE write, TYPE read,                 \
                   float writeValue, float readValue) {                  \
        if (weight > -0.1) {                                            \
            if (operation == 0 || operation == 1) {                     \
                write *= 1.0-weight;                               \
                read *= weight;                                    \
            }                                                           \
            if (operation == 2) {                                       \
                float writeWeight = min((1.0-weight)*2.0, 1.0);           \
                float readWeight = min(weight*2.0, 1.0);                 \
                write = (1.0 - writeWeight + write * writeWeight); \
                read = (1.0 - readWeight + read * readWeight);    \
            }                                                           \
        }                                                               \
        if (operation == 0) return write + read;                         \
        if (operation == 1) return write - read;                         \
        if (operation == 2) return write * read;                         \
        if (operation == 3) return mix(write, read,                      \
                                       smoothstep(0, smoothness,         \
                                                  abs(writeValue - readValue))); \
        if (operation == 4) return pow(write, read);                 \
        if (operation == 5) return read;                                 \
        if (operation == 6) return write;                                 \
        if (operation == 7) return hash(read);                                 \
        if (operation == 8) return mix(write, read, abs(writeValue - readValue)); \
    }

OPERATION_IMPL(vec4, performVec4Operation)
OPERATION_IMPL(vec3, performVec3Operation)
OPERATION_IMPL(float, performFloatOperation)

bool filterTest(float write, float read) {
    if (filterMode == 0) return true;
    if (filterMode == 1 && write < read) return true;
    if (filterMode == 2 && write > read) return true;
    return false;
}

void main(void) {
    vec4 writeAlbedo = texture(writeTextureAlbedo, textureCoords);
    vec4 writeMaterials = texture(writeTextureMaterial, textureCoords);

    vec4 readAlbedo = texture(readTextureAlbedo, textureCoords);
    vec4 readMaterials = texture(readTextureMaterial, textureCoords);

    float writeValue = writeMaterials.w;
    float readValue = readMaterials.w;
    
    if (!filterTest(writeValue, readValue)) {
        albedo_out = writeAlbedo;
        material_out = writeMaterials;
        return;
    }

    albedo_out.rgba = performVec4Operation(materialOperation, writeAlbedo.rgba, readAlbedo.rgba, writeValue, readValue);
    material_out.rgb = performVec3Operation(materialOperation, writeMaterials.rgb, readMaterials.rgb, writeValue, readValue);
    material_out.a = performFloatOperation(heightOperation, writeMaterials.w, readMaterials.w, writeValue, readValue);
}
