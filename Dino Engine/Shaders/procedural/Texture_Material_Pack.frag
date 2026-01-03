#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;
layout (location = 2) out float height_out;

uniform sampler2D albedoRead;
uniform sampler2D materialRead;
uniform sampler2D heightRead;

uniform float alphaCutoff;

void main(void)
{
    vec4 albedo_in = texture(albedoRead, textureCoords).rgba;
    vec4 material_in = texture(materialRead, textureCoords).rgba;
    float height_in = texture(heightRead, textureCoords).r;
 
    int alphaMask = 1;
    if (albedo_in.a < alphaCutoff) {
        alphaMask = 0;
    }
    int subSurface7 = int(round(material_in.a * 127.0));
    float packedData = float( (subSurface7 << 1) | alphaMask) / 255.0;


 
    albedo_out.rgb = albedo_in.rgb;
    albedo_out.a = packedData;
    material_out.rgb = material_in.rgb;
    material_out.a = height_in;
    height_out = height_in; // not needed...
}