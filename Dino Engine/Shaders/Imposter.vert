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
out mat3 viewTBN;

uniform int sliceCount;

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

void main() {
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

    // 1. VIEW-PLANE ALIGNED BILLBOARDING (Screen-Parallel)
    // Extract camera's world-space Right vector directly from viewMatrix.
    // This vector is CONSTANT for all objects during strafing (no physical quad spinning).
    vec3 cameraRight = normalize(vec3(viewMatrix[0].x, viewMatrix[1].x, viewMatrix[2].x));
    vec3 billboardUp = vec3(0.0, 1.0, 0.0); // Keep upright along Y axis
    vec3 billboardNormal = normalize(cross(cameraRight, billboardUp));

    mat3 worldTBN = mat3(cameraRight, billboardUp, billboardNormal);
    viewTBN = mat3(viewMatrix) * worldTBN;

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
    float maxXZ = length(instanceBaseLength.xz); // sqrt(Lx^2 + Lz^2)

    // Quad width accounts for texture transparent padding + non-uniform entity scale
    float effectiveWidth = maxXZ * (scaledWidth / max(unscaledWidth, 0.0001));
    float effectiveHeight = instanceBaseLength.y * instanceScale.y;

    // Apply scaling to unit quad [-0.5, 0.5]
    vec3 scaledPos = position * vec3(effectiveWidth, effectiveHeight, 1.0);

    // Build world position using screen-parallel vectors
    vec3 worldPos = instancePosition 
                  + (cameraRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

    gl_Position = projectionMatrix * viewMatrix * vec4(worldPos, 1.0);



    // Quad position [-0.5, 0.5] mapped to UV [0, 1]
    fragUV = position.xy + vec2(0.5);

    textureIndex = (instanceModelID * float(sliceCount)) + float(finalAngleIndex);
}