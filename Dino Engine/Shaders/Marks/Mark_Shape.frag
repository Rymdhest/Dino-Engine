#version 420

// Writes one shape into the map of marks. Distances go in with a minimum blend, and the minimum of two distances is the distance to both: so shapes that overlap become one shape, with
// one outline, however many there are. The rest goes in with a maximum, so that a hundred of them stacked up on the same spot are as bright as the brightest.
// The shapes are Inigo Quilez's. What's in here has to agree with GroundShape, which is what the server tests blows against.

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

	// Filled out from where it starts to the edge, with a bright front at the place it has got to: that's the whole of how anyone tells how long they have.
	float reached = 1.0 - smoothstep(fill - 0.015, fill + 0.015, along);
	float front = exp(-pow((along - fill) / FrontWidth, 2.0)) * step(0.001, fill) * (1.0 - step(0.999, fill));
	float filled = (reached * 0.75 + front) * strength;

	out_Distance = hostile > 0.5 ? vec2(distanceTo, Far) : vec2(Far, distanceTo);
	out_Fill = hostile > 0.5 ? vec4(filled, 0.0, strength, 0.0) : vec4(0.0, filled, 0.0, strength);
}
