#version 330

// One press into the snow trail (SnowTrailRenderer): a square about it, wide enough for whatever it throws ahead, drawn into the repeating texture where its place in the
// world falls. Four corners an instance, with no vertex data, and everything about the stamp handed on flat.

const int STAMPS = 32;  // As SnowTrailRenderer.StampsPerPass.

uniform vec4 stampPlace[32];    // xy centre on the xz plane, z yaw, w kind (SnowPrint).
uniform vec4 stampSize[32];     // x half width, y half length, z depth (of SNOW_TRAIL_DEEPEST), w how much of the surface its floor replaces.
uniform vec4 stampMore[32];     // xy the way it was going, up to 1 for a run, z rim (of SNOW_TRAIL_HIGHEST), w how thickly it throws clumps.
uniform vec4 stampWrap[32];     // xy what puts it in the texture's 0..1, z metres out it reaches.
uniform float trailSize;

out vec2 world;                 // Where this is, on the xz plane.
flat out vec4 place;
flat out vec4 size;
flat out vec4 more;
flat out float reach;           // Metres out from its centre the square it's drawn on goes.

const vec2 corners[4] = vec2[](vec2(-1.0, -1.0), vec2(1.0, -1.0), vec2(-1.0, 1.0), vec2(1.0, 1.0));

void main(void)
{
    int i = gl_InstanceID;
    place = stampPlace[i];
    size = stampSize[i];
    more = stampMore[i];
    vec4 wrap = stampWrap[i];
    reach = wrap.z;

    world = place.xy + corners[gl_VertexID] * wrap.z;
    vec2 uv = world / trailSize + wrap.xy;
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}
