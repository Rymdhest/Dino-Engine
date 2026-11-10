#version 430

// Everything done texel by texel (Field and Paint operators in Textures/Synthesis/Field.cs): up to three inputs, each a channel of an
// image, all of an image or a constant, combined by one operation. A field is a scalar in r, a paint a colour in rgb; every operation
// works on all four channels at once, so the same one does for both. The numbers of the operations are Op in Node.cs.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;
uniform sampler2D inputB;
uniform sampler2D inputC;
uniform int channelA;
uniform int channelB;
uniform int channelC;
uniform vec4 constantA;
uniform vec4 constantB;
uniform vec4 constantC;
uniform int op;
uniform vec4 parameters;

const vec3 LUMINANCE = vec3(0.2126, 0.7152, 0.0722);

vec4 smoothMin(vec4 a, vec4 b, float k)
{
    // Quilez's polynomial smooth minimum: within k of each other they round into one another, and further apart it's the minimum.
    vec4 h = max(vec4(k) - abs(a - b), 0.0) / max(k, 1e-6);
    return min(a, b) - h * h * k * 0.25;
}

// A rising edge, from what's 0 at a border to 1 at `width` in.
float profile(float distance, float width, int shape)
{
    float x = clamp(distance / max(width, 1e-6), 0.0, 1.0);
    if (shape == 0) return x;                               // Straight.
    if (shape == 1) return x * x * (3.0 - 2.0 * x);         // Smooth.
    if (shape == 2) return sqrt(1.0 - (1.0 - x) * (1.0 - x));   // Round: steep at the edge, flat on top, as a pillow or a worn stone.
    if (shape == 3) return 1.0 - sqrt(1.0 - x * x);         // Cove: hollowed, flat at the edge and steep at the top.
    return sin(x * 1.5707963);                              // Sine: between round and straight.
}

vec3 rgbToHsv(vec3 c)
{
    vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, k.wz), vec4(c.gb, k.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
}

vec3 hsvToRgb(vec3 c)
{
    vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

void main(void)
{
    ivec2 at = texelAt();
    vec4 a = readInput(inputA, channelA, constantA, at);
    vec4 b = readInput(inputB, channelB, constantB, at);
    vec4 c = readInput(inputC, channelC, constantC, at);
    vec4 p = parameters;

    vec4 r = a;
    switch (op)
    {
        case 0: r = a + b; break;
        case 1: r = a - b; break;
        case 2: r = a * b; break;
        case 3: r = a * sign(b) / max(abs(b), vec4(1e-6)); break;     // Nothing divided by nothing is nothing.
        case 4: r = min(a, b); break;
        case 5: r = max(a, b); break;
        case 6: r = mix(a, b, c); break;
        case 7: r = smoothMin(a, b, p.x); break;
        case 8: r = -smoothMin(-a, -b, p.x); break;
        case 9: r = (a - p.x) / (p.y - p.x) * (p.w - p.z) + p.z; break;
        case 10: r = clamp(a, p.x, p.y); break;
        case 11:
        {
            // Written out, as GLSL's smoothstep is undefined with its edges the wrong way round, and a falling edge is as useful as a rising one.
            vec4 t = clamp((a - p.x) / (p.y - p.x), 0.0, 1.0);
            r = t * t * (3.0 - 2.0 * t);
            break;
        }
        case 12: r = pow(max(a, 0.0), vec4(p.x)); break;
        case 13: r = abs(a); break;
        case 14: r = 1.0 - a; break;
        case 15: r = vec4(profile(a.x, p.x, int(p.y))); break;
        case 16: r = floor(a * p.x) / p.x; break;
        case 17: r = 0.5 + 0.5 * sin(6.2831853 * (a * p.x + p.y)); break;
        case 18: r = smoothstep(vec4(p.x - p.y), vec4(p.x + p.y + 1e-6), a); break;
        case 19: r = 1.0 - (1.0 - a) * (1.0 - b); break;
        case 20: r = abs(a - b); break;
        case 21: r = a * b + c; break;
        case 22: r = sqrt(max(a, 0.0)); break;
        case 23: r = fract(a); break;
        case 24: r = vec4(dot(a.rgb, LUMINANCE)); break;
        case 25: r = vec4(synthHashValue(a.x, uint(p.x))); break;
        case 26:
        {
            // A colour moved round the wheel, made more or less vivid and lighter or darker by a random that b holds for each
            // piece (its id), each by up to as much as asked: the difference between bricks of one batch.
            float id = b.x;
            vec3 hsv = rgbToHsv(max(a.rgb, 0.0));
            hsv.x = fract(hsv.x + (synthHashValue(id, 11u) * 2.0 - 1.0) * p.x);
            hsv.y = clamp(hsv.y * (1.0 + (synthHashValue(id, 23u) * 2.0 - 1.0) * p.y), 0.0, 1.0);
            hsv.z = hsv.z * max(1.0 + (synthHashValue(id, 37u) * 2.0 - 1.0) * p.z, 0.0);
            r = vec4(hsvToRgb(hsv), a.a);
            break;
        }
        case 27: r = vec4(mix(vec3(dot(a.rgb, LUMINANCE)), a.rgb, p.x), a.a); break;
        case 28: r = vec4(a.x, b.x, c.x, 1.0); break;
        case 29: r = vec4(a[int(p.x)]); break;
        case 30:
        {
            vec4 low = 2.0 * a * b;
            vec4 high = 1.0 - 2.0 * (1.0 - a) * (1.0 - b);
            r = mix(low, high, step(0.5, a));
            break;
        }
        case 31: r = vec4(a.rgb, b.x); break;
    }
    result = r;
}
