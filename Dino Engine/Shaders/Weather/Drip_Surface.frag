#version 430

// One drip's mark on the map of them: xy the slope of its ring, z how wet it keeps the ground. Added up, as they overlap along an eave. The ring is a short train of waves, as rain's are (Rain_Ripples.frag).

in vec2 fragMetres;
flat in float fragRate;
flat in float fragAge;
flat in float fragSize;

layout (location = 0) out vec4 out_Surface;

const float REACH = 0.9;            // Drip_Surface.vert.
const float RING_LIFE = 0.7;        // DRIP_RING_LIFE.
const float RING_SPEED = 0.85;      // Metres a second.

void main(void)
{
    float dist = length(fragMetres);
    float soaked = 1.0 - min(dist / REACH, 1.0);
    vec2 slope = vec2(0.0);
    if (fragAge >= 0.0)
    {
        float life = fragAge / RING_LIFE;
        float front = (dist - fragAge * RING_SPEED * fragSize) / (0.05 + 0.1 * life);
        float rings = sin(front * 3.6) * exp(-front * front);
        slope = fragMetres / max(dist, 1e-3) * (rings * (1.0 - life) * (1.0 - life) * smoothstep(0.0, 0.04, life) * 1.6);
    }
    // Rings are cut off by the reach they're drawn in, so they fade out before it.
    slope *= smoothstep(0.0, 0.2, soaked);
    out_Surface = vec4(slope, soaked * soaked * fragRate * 1.4, 0.0);
}
