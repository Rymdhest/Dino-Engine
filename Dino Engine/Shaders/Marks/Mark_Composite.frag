#version 420

// Lays the map of marks over the scene. It doesn't paint what's in front of the camera: each pixel goes along its ray to the ground, and shows what's marked there. So a mark is exactly where it means
// whatever's in the way: it follows the land, it doesn't smear up a wall, and something under a tree can still be seen coming. Characters are kept out by the stencil, so that they stand on it.
// Two colours and no more. Whatever's in a player's way is one, whatever is theirs is the other, and that's all it ever takes to read: what kind of damage it is doesn't matter to whether you should move.

#include Globals.glsl

layout (location = 0) out vec4 out_Colour;

uniform sampler2D distances;
uniform sampler2D fills;
uniform sampler2D terrainDepth;

uniform vec4 mapRegion;
uniform vec4 terrainRegion;
uniform vec2 terrainHeightRange;
uniform float groundFallback;
uniform float seaLevel;
uniform vec3 hostileColour;
uniform vec3 friendlyColour;

in vec2 textureCoords;

const float Outline = 0.13;     // Metres either side of the edge.
const float Rim = 1.1;          // How far in from the edge the shape is tinted more strongly.

// mapRegion: xy the corner of the map in the world, zw one over how big it is. terrainRegion: xy the corner of what's known of the ground, z how big it is, and nothing is known if that's 0.
float groundAt(vec2 xz)
{
	if (terrainRegion.z <= 0.0) return groundFallback;
	vec2 uv = (xz - terrainRegion.xy) / terrainRegion.z;
	if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return groundFallback;
	float depth = texture(terrainDepth, uv).r;
	if (depth >= 1.0) return groundFallback;
	return max(terrainHeightRange.x - depth * terrainHeightRange.y, seaLevel);
}

// How a mark looks, as premultiplied colour, from how far inside its edge this is.
vec4 mark(float distanceTo, float filled, float strength, vec3 colour, float pixel)
{
	if (distanceTo > Outline + pixel * 2.0 || strength <= 0.003) return vec4(0.0);

	float inside = 1.0 - smoothstep(-pixel, pixel, distanceTo);
	float line = 1.0 - smoothstep(Outline - pixel, Outline + pixel, abs(distanceTo));
	float rim = exp(distanceTo / Rim * 2.2) * inside;

	float body = inside * (0.05 * min(strength, 1.0) + 0.36 * filled + 0.12 * rim * min(strength, 1.0));
	float edge = line * min(0.32 + 0.6 * strength, 1.0);
	float alpha = clamp(max(body, edge), 0.0, 0.92);

	// What's filled is hotter than the rest, and goes towards white where it's hottest, so it can be told from the ground in any light.
	vec3 hot = mix(colour, vec3(1.0), clamp(filled - 0.75, 0.0, 1.0) * 0.5);
	return vec4(hot * (1.0 + filled * 1.2) * alpha, alpha);
}

void main(void)
{
	// Where this pixel's ray meets the ground. The ground is high and low, so it's found by going back and forth: the view is steep enough for that to settle at once.
	vec4 far = invViewMatrix * (invProjectionMatrix * vec4(textureCoords * 2.0 - 1.0, 1.0, 1.0));
	vec3 way = normalize(far.xyz / far.w - viewPosWorld);
	if (way.y > -0.02) discard;

	float height = groundFallback;
	vec3 point = viewPosWorld;
	for (int i = 0; i < 3; i++)
	{
		point = viewPosWorld + way * ((height - viewPosWorld.y) / way.y);
		height = groundAt(point.xz);
	}
	point = viewPosWorld + way * ((height - viewPosWorld.y) / way.y);

	vec2 uv = (point.xz - mapRegion.xy) * mapRegion.zw;
	if (uv.x <= 0.0 || uv.y <= 0.0 || uv.x >= 1.0 || uv.y >= 1.0) discard;

	vec2 toEdge = texture(distances, uv).rg;
	vec4 fill = texture(fills, uv);

	// However far apart texels of the map are, an edge stays as sharp as this pixel can show.
	float pixel = max(length(vec2(dFdx(point.x), dFdx(point.z))), length(vec2(dFdy(point.x), dFdy(point.z)))) * 0.75;

	vec4 friendly = mark(toEdge.y, fill.y, fill.w, friendlyColour, pixel);
	vec4 hostile = mark(toEdge.x, fill.x, fill.z, hostileColour, pixel);

	// What can hurt you goes over what you've done.
	vec4 both = hostile + friendly * (1.0 - hostile.a);
	if (both.a <= 0.002) discard;
	out_Colour = both;
}
