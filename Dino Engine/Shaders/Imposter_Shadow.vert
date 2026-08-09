#version 420

#include globals.glsl

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 5) in float instanceModelID;
layout(location = 6) in vec3 instancePosition;
layout(location = 7) in vec3 instanceScale;
layout(location = 8) in float instanceRotY; 
layout(location = 9) in vec3 instanceBaseLength;

out vec2 fragUV;
out float textureIndex;

uniform mat4 viewpPojectionMatrix;
uniform mat4 lightViewpMatrix;
uniform int sliceCount;

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

void main() {
    // 1. DIRECTION TO LIGHT SOURCE (World Space)
    // Extract light forward direction from the light's view matrix
    vec3 lightForward = -normalize(vec3(lightViewpMatrix[0].z, lightViewpMatrix[1].z, lightViewpMatrix[2].z));
    
    // Vector pointing from instance position toward the light source
    vec3 toLight = -lightForward;

#ifdef LIGHT_POS_WORLD
    // Use explicit world space light position if defined in globals.glsl (for point/spot lights)
    toLight = normalize(lightPosWorld - instancePosition);
#endif

    // 2. TEXTURE SLICE SELECTION (From Light's Perspective)
    float lightAngle = atan(-toLight.x, toLight.z);
    
    float relativeAngle = lightAngle - instanceRotY;
    relativeAngle = mod(relativeAngle, TWO_PI);
    if (relativeAngle < 0.0) {
        relativeAngle += TWO_PI;
    }
    
    // Map angle [0, TWO_PI) -> [0, sliceCount)
    float sliceIndex = round((relativeAngle / TWO_PI) * float(sliceCount));
    int finalAngleIndex = int(sliceIndex) % sliceCount;

    // 3. LIGHT-FACING BILLBOARD ALIGNMENT
    // Project direction to light onto the XZ plane to keep the quad vertical (world Y)
    vec3 billboardNormal = vec3(toLight.x, 0.0, toLight.z);
    
    if (length(billboardNormal) < 0.0001) {
        billboardNormal = vec3(0.0, 0.0, 1.0);
    } else {
        billboardNormal = normalize(billboardNormal);
    }

    vec3 billboardUp = vec3(0.0, 1.0, 0.0);
    vec3 lightRight = normalize(cross(billboardUp, billboardNormal));

    // 4. SLICE FOOTPRINT & QUAD SCALING
    float sliceAngle = (float(finalAngleIndex) / float(sliceCount)) * TWO_PI;
    
    float cosA = cos(sliceAngle);
    float sinA = sin(sliceAngle);

    // Unscaled footprint width at light slice angle
    float unscaledWidth = sqrt(
        pow(instanceBaseLength.x * cosA, 2.0) +
        pow(instanceBaseLength.z * sinA, 2.0)
    );

    // Scaled footprint width at light slice angle
    float scaledWidth = sqrt(
        pow(instanceBaseLength.x * instanceScale.x * cosA, 2.0) +
        pow(instanceBaseLength.z * instanceScale.z * sinA, 2.0)
    );

    // Unscaled baking frustum width (maxXZ)
    float maxXZ = length(instanceBaseLength.xz);

    // Quad dimensions matching the slice visible to the light
    float effectiveWidth = maxXZ * (scaledWidth / max(unscaledWidth, 0.0001));
    float effectiveHeight = instanceBaseLength.y * instanceScale.y;

    // Apply scaling to unit quad [-0.5, 0.5]
    vec3 scaledPos = position * vec3(effectiveWidth, effectiveHeight, 1.0);

    // Build world position facing directly toward the light
    vec3 worldPos = instancePosition 
                  + (lightRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

    // 5. ROOT-ANCHORED SHADOW PUSH
    // position.y ranges from -0.5 (bottom) to +0.5 (top)
    float heightFactor = clamp(position.y + 0.5, 0.0, 1.0);

    // Push canopy shadow slightly along light forward direction to prevent self-shadowing
    float pushDistance = (effectiveWidth * 0.5) * 1.1 * heightFactor;
    worldPos += lightForward * pushDistance;

    gl_Position = viewpPojectionMatrix * vec4(worldPos, 1.0);

    // Quad position [-0.5, 0.5] mapped to UV [0, 1]
    fragUV = position.xy + vec2(0.5);

    textureIndex = (instanceModelID * float(sliceCount)) + float(finalAngleIndex);
}