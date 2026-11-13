#version 420
#include Globals.glsl

// A ghost's skin of light (SaurianSpectral, CharacterRenderer.SpectralPass), added over the lit scene. Nearly nothing where it's seen face on and bright where it's seen edge on, as light in a
// thin shell is, with brighter light running up and back through it in its own frame and a slow breath through all of it. Its colour, and how it thins towards the ground, are in its
// vertices. It's emission, so it goes with emission (emissionScale), and the frame is still black with every light out.

in vec3 fragWorld;
in vec3 fragNormal;
in vec3 fragBind;
in vec3 fragColour;

uniform float emissionScale;
uniform vec4 hitFlash;
uniform vec4 characterSight;

layout (location = 0) out vec4 out_Colour;

const float FACE_ON = 0.2;      // What it gives off seen straight on,
const float EDGE_ON = 5.5;      // and edge on, as radiance for each unit of its colour: bright enough to read in sunlight, as only its outline is.
const float VEINS = 2.2;        // What the light running through it adds.

float spectralHash(vec3 p)
{
    p = fract(p * vec3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return fract((p.x + p.y) * p.z);
}

float spectralNoise(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = mix(spectralHash(i), spectralHash(i + vec3(1.0, 0.0, 0.0)), f.x);
    float b = mix(spectralHash(i + vec3(0.0, 1.0, 0.0)), spectralHash(i + vec3(1.0, 1.0, 0.0)), f.x);
    float c = mix(spectralHash(i + vec3(0.0, 0.0, 1.0)), spectralHash(i + vec3(1.0, 0.0, 1.0)), f.x);
    float d = mix(spectralHash(i + vec3(0.0, 1.0, 1.0)), spectralHash(i + vec3(1.0, 1.0, 1.0)), f.x);
    return mix(mix(a, b, f.y), mix(c, d, f.y), f.z);
}

void main()
{
    vec3 n = normalize(fragNormal);
    vec3 toEye = normalize(viewPosWorld - fragWorld);
    float facing = clamp(abs(dot(n, toEye)), 0.0, 1.0);
    float rim = pow(1.0 - facing, 2.2);

    // Light running up and back through it, slowly, in the body's own frame.
    float t = time;
    vec3 q = fragBind * 0.85;
    float flow = spectralNoise(q + vec3(0.0, -t * 0.55, t * 0.3)) * 0.65 + spectralNoise(q * 2.3 + vec3(t * 0.21, -t * 0.9, 0.0)) * 0.35;
    float veins = smoothstep(0.58, 0.86, flow);
    float breath = 0.82 + 0.18 * sin(t * 1.25 + fragBind.z * 0.35);

    float shine = (FACE_ON + EDGE_ON * rim + VEINS * veins * (0.35 + rim)) * breath;
    vec3 colour = fragColour * shine;

    // A blow goes through it as a flash, as it does any body, and coming or going it thins as anything does.
    colour += hitFlash.rgb * (hitFlash.a * 3.0) * (0.3 + rim) * max(fragColour.b, 0.2);
    colour *= 1.0 - clamp(characterSight.y, 0.0, 1.0);
    out_Colour = vec4(max(colour, vec3(0.0)) * emissionScale, 0.0);
}
