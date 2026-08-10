#version 420
#include globals.glsl

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 5) in float instanceModelID;
layout(location = 6) in vec3 instancePosition;
layout(location = 7) in vec3 instanceScale;
layout(location = 8) in vec4 instanceRot;
layout(location = 9) in vec3 instanceBaseLength;

out vec2 fragUV;
out float textureIndex;

uniform mat4 viewpPojectionMatrix;
uniform mat4 lightViewpMatrix;
uniform int sliceCount;

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

vec3 rotateVectorByQuaternion(vec3 v, vec4 q) {
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

void main() {
    // 1. DIRECTION TO LIGHT SOURCE (World Space)
    vec3 lightForward = -normalize(vec3(lightViewpMatrix[0].z, lightViewpMatrix[1].z, lightViewpMatrix[2].z));
    vec3 toLight = -lightForward;

#ifdef LIGHT_POS_WORLD
    toLight = normalize(lightPosWorld - instancePosition);
#endif

    // Convert toLight direction into the instance's Local Space using the inverse quaternion
    vec4 invRot = vec4(-instanceRot.xyz, instanceRot.w);
    vec3 localToLight = rotateVectorByQuaternion(toLight, invRot);

    // 2. TEXTURE SLICE SELECTION (From Light's Perspective)
    // MAKE SURE THESE SIGNS MATCH WHATEVER CALIBRATION YOU DID IN IMPOSTER.VERT
    float lightAngle = atan(localToLight.x, localToLight.z); 
    
    float relativeAngle = mod(lightAngle, TWO_PI);
    if (relativeAngle < 0.0) {
        relativeAngle += TWO_PI;
    }
    
    // Map angle [0, TWO_PI) -> [0, sliceCount)
    float sliceIndex = round((relativeAngle / TWO_PI) * float(sliceCount));
    int finalAngleIndex = int(sliceIndex) % sliceCount;

    // 3. LIGHT-FACING BILLBOARD ALIGNMENT (Arbitrary Rotation)
    // The tree's "up" vector rotated into world space
    vec3 billboardUp = rotateVectorByQuaternion(vec3(0.0, 1.0, 0.0), instanceRot);
    
    // Billboard's right vector must be perpendicular to BOTH its tilted Up vector and the Light
    vec3 lightRight = cross(billboardUp, toLight);
    
    // Fallback if light is directly above the tilted tree
    if (length(lightRight) < 0.0001) {
        lightRight = rotateVectorByQuaternion(vec3(1.0, 0.0, 0.0), instanceRot);
    } else {
        lightRight = normalize(lightRight);
    }

    // 4. SLICE FOOTPRINT & QUAD SCALING
    float sliceAngle = (float(finalAngleIndex) / float(sliceCount)) * TWO_PI;
    
    float cosA = cos(sliceAngle);
    float sinA = sin(sliceAngle);

    float unscaledWidth = sqrt(pow(instanceBaseLength.x * cosA, 2.0) + pow(instanceBaseLength.z * sinA, 2.0));
    float scaledWidth = sqrt(pow(instanceBaseLength.x * instanceScale.x * cosA, 2.0) + pow(instanceBaseLength.z * instanceScale.z * sinA, 2.0));
    float maxXZ = length(instanceBaseLength.xz);

    float effectiveWidth = maxXZ * (scaledWidth / max(unscaledWidth, 0.0001));
    float effectiveHeight = instanceBaseLength.y * instanceScale.y;

    vec3 scaledPos = position * vec3(effectiveWidth, effectiveHeight, 1.0);

    // Build world position using the tilted light-facing vectors
    vec3 worldPos = instancePosition 
                  + (lightRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

    // 5. ROOT-ANCHORED SHADOW PUSH
    float heightFactor = clamp(position.y + 0.5, 0.0, 1.0);
    float pushDistance = (effectiveWidth * 0.5) * 1.1 * heightFactor;
    worldPos += lightForward * pushDistance;

    gl_Position = viewpPojectionMatrix * vec4(worldPos, 1.0);

    fragUV = position.xy + vec2(0.5);
    textureIndex = (instanceModelID * float(sliceCount)) + float(finalAngleIndex);
} 