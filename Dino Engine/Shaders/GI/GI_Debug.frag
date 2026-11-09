#version 430

// The voxels as GI holds them, seen from the camera in place of the picture (--render gidebug=1 or 2): each pixel marches its view ray
// through the finest level that holds it, so what's near the hero shows in its finest voxels. 1 shows their colours, shaded by the
// face struck so their shapes read; 2 the light leaving them, which is all a ray of GI ever sees.

#include Globals.glsl
#include GI/GI.glsl
#include GI/GI_Voxels.glsl
#include GI/GI_Radiance.glsl

in vec2 textureCoords;
layout(location = 0) out vec4 out_Colour;

uniform sampler3D giColour;
uniform int mode;

const vec3 FACE_SHADE = vec3(0.8, 1.0, 0.65);

void main()
{
    vec2 ndc = textureCoords * 2.0 - 1.0;
    vec4 far = invProjectionMatrix * vec4(ndc, 1.0, 1.0);
    vec3 dir = normalize(mat3(invViewMatrix) * (far.xyz / far.w));
    vec3 origin = viewPosWorld;
    vec3 inv = 1.0 / dir;

    for (int level = 0; level < GI_LEVELS; level++)
    {
        float size = giVoxel[level].x;
        vec3 low = vec3(giOrigin[level].xyz) * size;
        vec3 high = low + vec3(GI_DIM) * size;
        vec3 t0 = (low - origin) * inv, t1 = (high - origin) * inv;
        vec3 near = min(t0, t1), farT = max(t0, t1);
        float enter = max(max(near.x, near.y), near.z);
        float leave = min(min(farT.x, farT.y), farT.z);
        if (leave <= max(enter, 0.0)) continue;
        GiHit hit = giTrace(origin, dir, max(enter, 0.0) + 1e-3, leave, level, 0);
        if (!hit.hit || hit.level != level) continue;

        if (mode == 1)
        {
            vec3 albedo = texelFetch(giColour, giTexel(hit.voxel, hit.level), 0).rgb;
            float shade = hit.face < 0 ? 0.5 : FACE_SHADE[hit.face >> 1];
            float tint = 1.0 - 0.06 * float(level);
            out_Colour = vec4(albedo * shade * tint * 3.0, 1.0);
        }
        else
        {
            out_Colour = vec4(giHitRadiance(hit, dir), 1.0);
        }
        return;
    }
    out_Colour = vec4(0.0, 0.0, 0.0, 1.0);
}
