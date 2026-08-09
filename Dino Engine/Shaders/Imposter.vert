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
    // 1. DIRECTION TO CAMERA (World Space)
    vec3 toCamera = viewPosWorld - instancePosition;
    
    // Calculate angle from instance to camera for texture slice selection
    float camAngle = atan(-toCamera.x, toCamera.z);
    
    float relativeAngle = camAngle - instanceRotY;
    relativeAngle = mod(relativeAngle, TWO_PI);
    if (relativeAngle < 0.0) {
        relativeAngle += TWO_PI;
    }
    
    // Map angle [0, TWO_PI) -> [0, sliceCount)
    float sliceIndex = round((relativeAngle / TWO_PI) * float(sliceCount));
    int finalAngleIndex = int(sliceIndex) % sliceCount;

    // 2. VIEW-POINT ALIGNED BILLBOARDING (Facing Camera Position)
    // Project direction to camera onto the XZ plane so the billboard stays vertical (Y-up)
    vec3 billboardNormal = vec3(toCamera.x, 0.0, toCamera.z);
    
    // Fallback if camera is directly above/below the tree
    if (length(billboardNormal) < 0.0001) {
        billboardNormal = vec3(0.0, 0.0, 1.0);
    } else {
        billboardNormal = normalize(billboardNormal);
    }

    vec3 billboardUp = vec3(0.0, 1.0, 0.0); // Constrained upright along world Y axis
    
    // Compute world-space Right vector facing the camera position
    vec3 cameraRight = normalize(cross(billboardUp, billboardNormal));

    // Construct world-space TBN matrix
    mat3 worldTBN = mat3(cameraRight, billboardUp, billboardNormal);
    viewTBN = mat3(viewMatrix) * worldTBN;

    // 3. SLICE FOOTPRINT & QUAD SCALING
    float sliceAngle = (float(finalAngleIndex) / float(sliceCount)) * TWO_PI;
    
    float cosA = cos(sliceAngle);
    float sinA = sin(sliceAngle);

    // Unscaled footprint width at this slice angle
    float unscaledWidth = sqrt(
        pow(instanceBaseLength.x * cosA, 2.0) +
        pow(instanceBaseLength.z * sinA, 2.0)
    );

    // Scaled footprint width at this slice angle
    float scaledWidth = sqrt(
        pow(instanceBaseLength.x * instanceScale.x * cosA, 2.0) +
        pow(instanceBaseLength.z * instanceScale.z * sinA, 2.0)
    );

    // Unscaled baking frustum width (maxXZ)
    float maxXZ = length(instanceBaseLength.xz);

    // Quad dimensions accounting for baking padding and entity scale
    float effectiveWidth = maxXZ * (scaledWidth / max(unscaledWidth, 0.0001));
    float effectiveHeight = instanceBaseLength.y * instanceScale.y;

    // Apply scaling to unit quad [-0.5, 0.5]
    vec3 scaledPos = position * vec3(effectiveWidth, effectiveHeight, 1.0);

    // Build world position using view-point aligned vectors
    vec3 worldPos = instancePosition 
                  + (cameraRight * scaledPos.x) 
                  + (billboardUp * scaledPos.y);

    gl_Position = projectionMatrix * viewMatrix * vec4(worldPos, 1.0);

    // Quad position [-0.5, 0.5] mapped to UV [0, 1]
    fragUV = position.xy + vec2(0.5);

    textureIndex = (instanceModelID * float(sliceCount)) + float(finalAngleIndex);
}