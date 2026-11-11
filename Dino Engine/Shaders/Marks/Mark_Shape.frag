#version 430

// Writes one shape into the map of marks. Distances go in with a minimum blend, and the minimum of two distances is the distance to both: so shapes that overlap become one shape, with
// one outline, however many there are. The rest goes in with a maximum, so that a hundred of them stacked up on the same spot are as bright as the brightest.
// The shapes are Inigo Quilez's. What's in here has to agree with GroundShape, which is what the server tests blows against.
// Whatever a line from the origin can't reach is cut out of it: each blocker's shadow (Sight.Shadow, worked out on the CPU from the very blockers the server tests against) is a convex polygon, and a round
// blocker's circle as well. Nobody in a shadow is hit, so nothing in one is marked, and the cut has an outline like any other edge.

layout (location = 0) out vec2 out_Distance;    // To what's hostile, and to what isn't.
layout (location = 1) out vec4 out_Fill;        // xy the same two, how far the fill has come. zw how strongly each shows.

uniform int kind;
uniform vec2 origin;
uniform vec2 facing;
uniform float len;
uniform float wid;
uniform float fill;
uniform float strength;
uniform float hostile;
uniform int shadowStart;
uniform int shadowCount;

// Five vec4s a shadow: eight corners two to a vec4, then the circle (xy middle, z radius, w how many corners).
layout (std430, binding = 12) readonly buffer MarkShadows { vec4 markShadows[]; };

in vec2 worldXZ;

const float Far = 1000.0;
const float FrontWidth = 0.05;

float sector(vec2 p, vec2 sinCos, float radius)
{
	p.x = abs(p.x);
	float toCircle = length(p) - radius;
	float toEdge = length(p - sinCos * clamp(dot(p, sinCos), 0.0, radius));
	return max(toCircle, toEdge * sign(sinCos.y * p.x - sinCos.x * p.y));
}

float box(vec2 p, vec2 halfSize)
{
	vec2 d = abs(p) - halfSize;
	return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

vec2 shadowCorner(int base, int i)
{
	vec4 pair = markShadows[base + (i >> 1)];
	return (i & 1) == 0 ? pair.xy : pair.zw;
}

// Inigo Quilez's distance to a polygon, signed: less than nothing inside.
float shadowDistance(int base, vec2 p)
{
	vec4 circle = markShadows[base + 4];
	int count = int(circle.w + 0.5);
	float result = Far;
	if (count >= 3)
	{
		vec2 first = shadowCorner(base, 0);
		float d = dot(p - first, p - first);
		float s = 1.0;
		for (int i = 0, j = count - 1; i < count; j = i, i++)
		{
			vec2 vi = shadowCorner(base, i);
			vec2 vj = shadowCorner(base, j);
			vec2 e = vj - vi;
			vec2 w = p - vi;
			vec2 b = w - e * clamp(dot(w, e) / dot(e, e), 0.0, 1.0);
			d = min(d, dot(b, b));
			bvec3 c = bvec3(p.y >= vi.y, p.y < vj.y, e.x * w.y > e.y * w.x);
			if (all(c) || all(not(c))) s = -s;
		}
		result = s * sqrt(d);
	}
	if (circle.z > 0.0) result = min(result, length(p - circle.xy) - circle.z);
	return result;
}

void main(void)
{
	vec2 offset = worldXZ - origin;
	vec2 p = vec2(dot(offset, vec2(facing.y, -facing.x)), dot(offset, facing));

	float distanceTo;
	float along;
	if (kind == 0)
	{
		distanceTo = length(p) - len;
		along = length(p) / len;
	}
	else if (kind == 1)
	{
		distanceTo = sector(p, vec2(sin(wid * 0.5), cos(wid * 0.5)), len);
		along = length(p) / len;
	}
	else
	{
		distanceTo = box(p - vec2(0.0, len * 0.5), vec2(wid, len * 0.5));
		along = p.y / len;
	}

	// What it can't reach isn't in it.
	float hidden = Far;
	for (int i = 0; i < shadowCount; i++) hidden = min(hidden, shadowDistance((shadowStart + i) * 5, worldXZ));
	distanceTo = max(distanceTo, -hidden);

	// Filled out from where it starts to the edge, with a bright front at the place it has got to: that's the whole of how anyone tells how long they have.
	float reached = 1.0 - smoothstep(fill - 0.015, fill + 0.015, along);
	float front = exp(-pow((along - fill) / FrontWidth, 2.0)) * step(0.001, fill) * (1.0 - step(0.999, fill));
	float filled = (reached * 0.75 + front) * strength;

	out_Distance = hostile > 0.5 ? vec2(distanceTo, Far) : vec2(Far, distanceTo);
	out_Fill = hostile > 0.5 ? vec4(filled, 0.0, strength, 0.0) : vec4(0.0, filled, 0.0, strength);
}
