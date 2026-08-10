#version 420
#include globals.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=5) in float instanceModelID;
layout(location=6) in vec3 instancePosition;
layout(location=7) in vec3 instanceScale;
layout(location=8) in vec4 instanceRot;
layout(location=9) in vec3 instanceBaseLength;

out vec2 fragUV;
out float textureIndex;
out mat3 viewTBN;

uniform int sliceCount;
const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

// Helper to rotate a vector by a quaternion
vec3 rotateVectorByQuaternion(vec3 v, vec4 q) {
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

void main() {
    // 1. DIRECTION TO CAMERA (World Space)
    vec3 toCameraWorld = viewPosWorld - instancePosition;
    
    // Convert camera direction into the instance's Local Space using the inverse quaternion
    vec4 invRot = vec4(-instanceRot.xyz, instanceRot.w);
    vec3 localToCamera = rotateVectorByQuaternion(toCameraWorld, invRot);
    
    // Calculate slice angle based on Local Space camera direction
    // (We no longer need to subtract instanceRotY because localToCamera is already relative to the tree's rotation)
    float camAngle = atan(localToCamera.x, localToCamera.z);
    
    float relativeAngle = mod(camAngle, TWO_PI);
    if (relativeAngle < 0.0) {
        relativeAngle += TWO_PI;
    }
    
    // Map angle [0, TWO_PI) -> [0, sliceCount)
    float sliceIndex = round((relativeAngle / TWO_PI) * float(sliceCount));
    int finalAngleIndex = int(sliceIndex) % sliceCount;

    // 2. VIEW-POINT ALIGNED BILLBOARDING (Arbitrary Rotation)
    // The tree's "up" vector rotated into world space
    vec3 billboardUp = rotateVectorByQuaternion(vec3(0.0, 1.0, 0.0), instanceRot);
    
    // The billboard's right vector must be perpendicular to BOTH its tilted Up vector and the Camera
    vec3 cameraRight = cross(billboardUp, toCameraWorld);
    
    // Fallback if camera is looking straight down the trunk (parallel vectors)
    if (length(cameraRight) < 0.0001) {
        cameraRight = rotateVectorByQuaternion(vec3(1.0, 0.0, 0.0), instanceRot);
    } else {
        cameraRight = normalize(cameraRight);
    }

    vec3 billboardNormal = normalize(cross(cameraRight, billboardUp));

    // Construct world-space TBN matrix
    mat3 worldTBN = mat3(cameraRight, billboardUp, billboardNormal);
    viewTBN = mat3(viewMatrix) * worldTBN;

    // 3. SLICE FOOTPRINT & QUAD SCALING
    float sliceAngle = (float(finalAngleIndex) / float(sliceCount)) * TWO_PI;
    
    float cosA = cos(sliceAngle);
    float sinA = sin(sliceAngle);

    // Scaled & Unscaled footprints
    float unscaledWidth = sqrt(pow(instanceBaseLength.x * cosA, 2.0) + pow(instanceBaseLength.z * sinA, 2.0));
    float scaledWidth = sqrt(pow(instanceBaseLength.x * instanceScale.x * cosA, 2.0) + pow(instanceBaseLength.z * instanceScale.z * sinA, 2.0));
    float maxXZ = length(instanceBaseLength.xz);

    float effectiveWidth = maxXZ * (scaledWidth / max(unscaledWidth, 0.0001));
    float effectiveHeight = instanceBaseLength.y * instanceScale.y;

    vec3 scaledPos = position * vec3(effectiveWidth, effectiveHeight, 1.0);

    // Build world position using the tilted viewpoint-aligned vectors
    vec3 worldPos = instancePosition 
                  + (cameraRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

    gl_Position = projectionMatrix * viewMatrix * vec4(worldPos, 1.0);

    fragUV = position.xy + vec2(0.5);
    textureIndex = (instanceModelID * float(sliceCount)) + float(finalAngleIndex);
}