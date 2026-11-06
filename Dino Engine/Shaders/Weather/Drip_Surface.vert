#version 430

// What drips do to the ground under them, as a map around the focus that every surface reads (Weather/Wetness.glsl): soaked patches under each edge, even where the rain itself can't reach, and
// rings running out from each drop. Being read in the geometry pass, those rings are real ripples, and bend what a puddle reflects as rain's do. Each instance is one place that drips.

#include Globals.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/Drip.glsl

out vec2 fragMetres;        // From where the drops land.
flat out float fragRate;
flat out float fragAge;     // Seconds since the last one landed, or below 0 with no ring to draw.
flat out float fragSize;

const float REACH = 0.9;    // Metres around a landing that it wets. Drip_Surface.frag fades to nothing by here.

void main()
{
    DripSource source;
    if (!readDripSource(gl_InstanceID, source))
    {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        fragMetres = vec2(0.0);
        fragRate = 0.0;
        fragAge = -1.0;
        fragSize = 1.0;
        return;
    }

    float age;
    uint drop;
    bool landed = dripTurn(source, DRIP_HANG + source.falling, age, drop);
    fragAge = landed && age < DRIP_RING_LIFE ? age : -1.0;
    fragSize = 0.8 + 0.4 * dripRandom(drop);
    fragRate = source.rate;

    int corners = gl_VertexID & 3;
    vec2 corner = vec2((corners & 1) == 0 ? -1.0 : 1.0, (corners >> 1) == 0 ? -1.0 : 1.0);
    fragMetres = corner * REACH;
    vec2 place = (source.at + fragMetres - dripRegion.xy) / dripRegion.zw;
    gl_Position = vec4(place * 2.0 - 1.0, 0.0, 1.0);
}
