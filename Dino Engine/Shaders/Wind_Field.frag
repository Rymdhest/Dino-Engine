#version 420

// Bakes the wind field (Wind.glsl) into the wind map: (velocity.x, velocity.y, velocity.z, gust intensity).

#include Wind.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Wind;

void main(void)
{
    // textureCoords sits on texel centres, which is where consumers' bilinear lookups expect the values.
    vec2 xz = windMapRegion.xy + textureCoords * windMapRegion.zw;
    out_Wind = windVelocityAt(xz);
}
