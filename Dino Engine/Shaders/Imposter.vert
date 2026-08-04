#version 420
#include globals.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;

layout(location=6) in vec3 instancePosition;
layout(location=7) in float instanceScale;
layout(location=8) in float instanceRotY; 

out vec2 fragUV;
out float textureIndex;
out mat3 viewTBN;

uniform int sliceCount;

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

void main() {
    // 1. VIEW-PLANE ALIGNED BILLBOARDING (Screen-Parallel)
    // Extract camera's world-space Right vector directly from viewMatrix.
    // This vector is CONSTANT for all objects during strafing (no physical quad spinning).
    vec3 cameraRight = normalize(vec3(viewMatrix[0].x, viewMatrix[1].x, viewMatrix[2].x));
    vec3 billboardUp = vec3(0.0, 1.0, 0.0); // Keep upright along Y axis
    vec3 billboardNormal = normalize(cross(cameraRight, billboardUp));

    mat3 worldTBN = mat3(cameraRight, billboardUp, billboardNormal);
    viewTBN = mat3(viewMatrix) * worldTBN;

    // Apply model dimensions & scale
    vec3 scaledPos = position * vec3(instanceScale, instanceScale, 1.0);

    // Build world position using screen-parallel vectors
    vec3 worldPos = instancePosition 
                  + (cameraRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

    gl_Position = projectionMatrix * viewMatrix * vec4(worldPos, 1.0);

    // 2. TEXTURE ANGLE CALCULATION
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

    // Quad position [-0.5, 0.5] mapped to UV [0, 1]
    fragUV = position.xy + vec2(0.5, 0.0);

    float modelID = 0.0;
    textureIndex = (modelID * float(sliceCount)) + float(finalAngleIndex);
}