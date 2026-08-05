#version 420
#include globals.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=5) in float instanceModelID;
layout(location=6) in vec3 instancePosition;
layout(location=7) in vec3 instanceScale;
layout(location=8) in float instanceRotY; 
layout(location=9) in vec3 instanceBaseLength;

out vec2 fragUV;
out float textureIndex;

uniform mat4 viewpPojectionMatrix;
uniform mat4 lightViewpMatrix;
uniform int sliceCount;

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

void main() {
    // 1. TEXTURE ANGLE CALCULATION
    vec3 toCamera = normalize(viewPosWorld - instancePosition);
    
    // Calculates angle from instance to camera position
    float camAngle = atan(-toCamera.x, toCamera.z);
    
    float relativeAngle = camAngle - instanceRotY;
    relativeAngle = mod(relativeAngle, TWO_PI);
    if (relativeAngle < 0.0) {
        relativeAngle += TWO_PI;
    }
    
    // Map angle [0, TWO_PI) -> [0, sliceCount)
    float sliceIndex = round((relativeAngle / TWO_PI) * float(sliceCount));
    int finalAngleIndex = int(sliceIndex) % sliceCount;

    // 2. VIEW-PLANE ALIGNED BILLBOARDING (Screen-Parallel)
    // Extract camera's world-space Right vector directly from lightViewpMatrix.
    vec3 cameraRight = normalize(vec3(lightViewpMatrix[0].x, lightViewpMatrix[1].x, lightViewpMatrix[2].x));
    vec3 billboardUp = vec3(0.0, 1.0, 0.0); // Keep upright along Y axis

    float sliceAngle = (float(finalAngleIndex) / float(sliceCount)) * TWO_PI;
    
    float cosA = cos(sliceAngle);
    float sinA = sin(sliceAngle);

    // Unscaled footprint width at this angle
    float unscaledWidth = sqrt(
        pow(instanceBaseLength.x * cosA, 2.0) +
        pow(instanceBaseLength.z * sinA, 2.0)
    );

    // Scaled footprint width at this angle
    float scaledWidth = sqrt(
        pow(instanceBaseLength.x * instanceScale.x * cosA, 2.0) +
        pow(instanceBaseLength.z * instanceScale.z * sinA, 2.0)
    );

    // Unscaled baking frustum width (maxXZ)
    float maxXZ = length(instanceBaseLength.xz);

    // Quad width accounts for texture transparent padding + non-uniform entity scale
    float effectiveWidth = maxXZ * (scaledWidth / max(unscaledWidth, 0.0001));
    float effectiveHeight = instanceBaseLength.y * instanceScale.y;

    // Apply scaling to unit quad [-0.5, 0.5]
    vec3 scaledPos = position * vec3(effectiveWidth, effectiveHeight, 1.0);

    // Build world position using screen-parallel vectors
    vec3 worldPos = instancePosition 
                  + (cameraRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

// ------------------------------------------------------------------
    // ROOT-ANCHORED SHADOW PUSH:
    // ------------------------------------------------------------------
    vec3 lightForward = -normalize(vec3(lightViewpMatrix[0].z, lightViewpMatrix[1].z, lightViewpMatrix[2].z));

    // position.y ranges from -0.5 (bottom) to +0.5 (top).
    // heightFactor is 0.0 at the root and 1.0 at the canopy.
    float heightFactor = clamp(position.y + 0.5, 0.0, 1.0);

    // Scale push distance by height so the base stays anchored to the ground
    float pushDistance = (effectiveWidth * 0.5) * 1.1 * heightFactor;
    worldPos += lightForward * pushDistance;
    // ------------------------------------------------------------------

    gl_Position = viewpPojectionMatrix * vec4(worldPos, 1.0);

    // Quad position [-0.5, 0.5] mapped to UV [0, 1]
    fragUV = position.xy + vec2(0.5);

    textureIndex = (instanceModelID * float(sliceCount)) + float(finalAngleIndex);
}