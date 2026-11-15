#version 430
// The finished picture, drawn at RenderEngine.RenderScale of the window, put on the whole of it (RenderEngine.Upscale). Read bilinearly, then sharpened by its own
// neighbours a texel of the source away, as in AMD's contrast adaptive sharpening: what's added is clamped between the least and most of them, so an edge is
// steepened and never rings, and nothing that was flat changes. It reads the tone mapped, gamma encoded picture, as it will be seen.

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D source;
uniform vec2 sourceTexel;   // One texel of the source, in texture coordinates.
uniform float sharpness;    // 0 for none, 1 for a full unsharp mask.

void main(void)
{
    vec2 uv = textureCoords;
    vec3 centre = texture(source, uv).rgb;
    vec3 up = texture(source, uv + vec2(0.0, sourceTexel.y)).rgb;
    vec3 down = texture(source, uv - vec2(0.0, sourceTexel.y)).rgb;
    vec3 right = texture(source, uv + vec2(sourceTexel.x, 0.0)).rgb;
    vec3 left = texture(source, uv - vec2(sourceTexel.x, 0.0)).rgb;

    vec3 least = min(centre, min(min(up, down), min(left, right)));
    vec3 most = max(centre, max(max(up, down), max(left, right)));
    vec3 sharpened = centre + sharpness * (centre - 0.25 * (up + down + left + right));
    out_Colour = vec4(clamp(sharpened, least, most), 1.0);
}
