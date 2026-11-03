#version 420

// A still water body's surface into a top-down height capture (depth only), cut to the body's outline.

#include Water/WaterShape.glsl

in vec3 fragWorldPosition;
in vec2 fragLocalPosition;

uniform int bodyShape;
uniform vec2 bodyHalfExtents;
uniform float bodyEdgeSoftness;
uniform float bodyEdgeNoise;
uniform float bodyInvNoiseScale;

void main(void)
{
    if (waterShapeCoverage(bodyShape, fragLocalPosition, bodyHalfExtents, bodyEdgeSoftness, bodyEdgeNoise, bodyInvNoiseScale, fragWorldPosition.xz) < 0.5) discard;
}
