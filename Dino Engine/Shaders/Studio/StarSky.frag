#version 330

// A night sky to think in: a few layers of stars that drift past one another as the view moves, clusters and a faint band where they gather, nebulae
// turning slowly in the dark, far galaxies, and now and then a falling star. It's all worked out from where the pixel is and the time, with nothing
// stored, and it's kept dim and calm on purpose: it's a backdrop, and what's drawn over it is the point.

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform vec2 resolution;
uniform vec2 offset;
uniform float zoom;
uniform float time;
uniform float seed;

float hash(vec2 p)
{
	p = fract(p * vec2(123.34, 456.21) + seed);
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

vec2 hash2(vec2 p)
{
	float h = hash(p);
	return vec2(h, hash(p + h * 17.13));
}

float noise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p)
{
	float sum = 0.0;
	float amplitude = 0.5;
	mat2 turn = mat2(0.8, -0.6, 0.6, 0.8);
	for (int i = 0; i < 5; i++)
	{
		sum += amplitude * noise(p);
		p = turn * p * 2.03 + 11.7;
		amplitude *= 0.5;
	}
	return sum;
}

// The colour of a star from how hot it is: deep orange through white to pale blue, and never saturated, as stars never are to the eye.
vec3 starColour(float t)
{
	vec3 cool = vec3(1.0, 0.72, 0.48);
	vec3 white = vec3(0.98, 0.97, 1.0);
	vec3 hot = vec3(0.62, 0.76, 1.0);
	return t < 0.5 ? mix(cool, white, t * 2.0) : mix(white, hot, t * 2.0 - 1.0);
}

// One layer of stars, at most one to a cell. Points are drawn a pixel or two across, crisp, with a faint halo for the bright ones, and most are faint.
// A star is only looked for in its own cell and the eight around it, so nothing of it may reach past them, or the cells show as squares.
vec3 stars(vec2 uv, float cells, float chance, float pixelsPerUnit, float bright, float layer)
{
	vec3 light = vec3(0.0);
	vec2 cell = floor(uv * cells);
	float cellPx = pixelsPerUnit / cells;
	for (int y = -1; y <= 1; y++)
	{
		for (int x = -1; x <= 1; x++)
		{
			vec2 id = cell + vec2(x, y);
			float h = hash(id + layer * 7.31);
			if (h > chance) continue;
			vec2 at = (id + 0.15 + 0.7 * hash2(id + layer)) / cells;
			float distancePx = length(uv - at) * pixelsPerUnit;
			float power = pow(hash(id + 3.7 + layer), 4.0);
			float size = 0.5 + power * 1.1 * bright;
			float twinkle = 0.84 + 0.16 * sin(time * (0.5 + hash(id + 9.1) * 1.5) + h * 40.0);
			float core = smoothstep(size + 0.8, size - 0.4, distancePx);
			float reach = min(2.5 + 9.0 * power, cellPx * 0.85);
			float halo = pow(max(1.0 - distancePx / reach, 0.0), 3.0) * power * 0.45;
			light += starColour(hash(id + 5.3)) * (core * (0.12 + 0.88 * power) + halo) * twinkle * bright;
		}
	}
	return light;
}

// A spiral galaxy, small and far, turning very slowly: a warm core and bluer arms, leaning at an angle of its own.
vec3 galaxy(vec2 uv, vec2 centre, float size, float lean, float tilt, float spin, float arms)
{
	vec2 d = uv - centre;
	float c = cos(lean), s = sin(lean);
	d = mat2(c, -s, s, c) * d;
	d.y /= tilt;
	float r = length(d) / size;
	if (r > 2.2) return vec3(0.0);
	float angle = atan(d.y, d.x);
	float spiral = cos(arms * angle - 5.5 * log(r + 0.05) - spin);
	float armLight = pow(max(spiral * 0.5 + 0.5, 0.0), 3.0) * exp(-r * 1.6) * smoothstep(0.05, 0.3, r);
	float core = exp(-r * r * 18.0);
	float dust = 0.75 + 0.25 * noise(d * 40.0 / size);
	return vec3(1.0, 0.86, 0.66) * core * 0.55 + vec3(0.62, 0.72, 1.0) * armLight * 0.32 * dust;
}

// A falling star: one at a time on each of a few slow clocks, crossing part of the sky in under a second and fading as it goes.
vec3 meteor(vec2 uv, float aspect, float period, float channel)
{
	float slot = floor((time + channel * 37.0) / period);
	float into = (time + channel * 37.0) - slot * period;
	float h = hash(vec2(slot, channel * 13.0));
	if (h > 0.62) return vec3(0.0);
	float life = 0.7 + hash(vec2(slot, 2.0 + channel)) * 0.6;
	if (into > life) return vec3(0.0);
	float t = into / life;

	vec2 from = vec2((hash(vec2(slot, 3.0 + channel)) - 0.5) * aspect * 1.6, hash(vec2(slot, 4.0 + channel)) * 0.9 - 0.15);
	float heading = 3.6 + hash(vec2(slot, 5.0 + channel)) * 1.2;
	vec2 way = vec2(cos(heading), sin(heading));
	float travel = 0.45 + hash(vec2(slot, 6.0 + channel)) * 0.35;
	vec2 head = from + way * travel * t;
	float tail = 0.12 + 0.1 * t;

	vec2 rel = uv - head;
	float along = dot(rel, -way);
	float across = abs(dot(rel, vec2(-way.y, way.x)));
	if (along < -0.01 || along > tail) return vec3(0.0);
	float fade = (1.0 - along / tail);
	float width = 0.0012 + 0.0016 * fade;
	float line = smoothstep(width, 0.0, across) * fade * fade;
	float envelope = smoothstep(0.0, 0.15, t) * (1.0 - smoothstep(0.65, 1.0, t));
	return vec3(0.85, 0.9, 1.0) * line * envelope * 1.4;
}

void main(void)
{
	vec2 frag = textureCoords * resolution;
	float aspect = resolution.x / resolution.y;
	vec2 screen = (frag - resolution * 0.5) / resolution.y;
	screen.y = -screen.y;
	float depth = pow(max(zoom, 0.01), 0.12);
	vec2 view = vec2(offset.x, -offset.y);	// Given as the interface counts, downwards.

	// The deep, a little bluer at the top, and darker towards the corners.
	vec3 colour = mix(vec3(0.010, 0.012, 0.024), vec3(0.016, 0.020, 0.040), textureCoords.y);
	colour *= 1.0 - 0.45 * pow(length(screen / vec2(aspect, 1.0)) * 1.15, 2.2);

	// Nebulae: two veils of warped cloud drifting at their own pace, far behind everything, shaded from teal to violet to rose.
	vec2 neb = screen / depth * 1.4 + view * 0.00002 + vec2(time * 0.0016, time * 0.0009);
	vec2 warp = vec2(fbm(neb * 1.3 + 3.1), fbm(neb * 1.3 + 7.7));
	float cloud = fbm(neb * 1.1 + warp * 1.6 + time * 0.0025);
	float blot = smoothstep(0.36, 0.7, fbm(neb * 0.45 + 21.0));
	float lane = 1.0 - smoothstep(0.08, 0.32, abs(fbm(neb * 2.4 + warp) - 0.5));
	float veil = smoothstep(0.32, 0.8, cloud) * blot * (1.0 - 0.55 * lane);
	vec3 tint = mix(vec3(0.10, 0.42, 0.52), vec3(0.42, 0.18, 0.62), smoothstep(0.3, 0.75, warp.x));
	tint = mix(tint, vec3(0.62, 0.22, 0.38), smoothstep(0.62, 0.9, warp.y) * 0.6);
	colour += tint * veil * 0.34;

	// Where stars gather: slow clusters, and a faint band across the sky.
	vec2 field = screen / depth + view * 0.00004 + vec2(time * 0.0022, time * 0.0007);
	float band = exp(-pow((field.y * 0.9 + field.x * 0.35 + 0.1 * sin(field.x * 1.7)), 2.0) / 0.06);
	float clusters = smoothstep(0.55, 0.85, fbm(field * 1.6 + 40.0));
	float gather = 1.0 + band * 1.6 + clusters * 2.2;
	colour += vec3(0.30, 0.32, 0.42) * band * fbm(field * 9.0) * 0.07;

	float pixelsPerUnit = resolution.y * depth;
	colour += stars(field, 140.0, 0.03 * gather, pixelsPerUnit, 0.5, 1.0);
	vec2 mid = screen / depth + view * 0.00007 + vec2(time * 0.003, time * 0.001);
	colour += stars(mid, 70.0, 0.035 * gather, pixelsPerUnit, 0.7, 2.0);
	vec2 near = screen / depth + view * 0.00012 + vec2(time * 0.0045, time * 0.0014);
	colour += stars(near, 26.0, 0.04, pixelsPerUnit, 0.9, 3.0);

	// Far galaxies, one to a large cell where there's one at all.
	vec2 far = screen / depth * 0.9 + view * 0.00003 + vec2(time * 0.0012, 0.0);
	vec2 big = floor(far * 1.2);
	for (int y = -1; y <= 1; y++)
	{
		for (int x = -1; x <= 1; x++)
		{
			vec2 id = big + vec2(x, y);
			if (hash(id + 71.0) > 0.42) continue;
			vec2 centre = (id + 0.2 + 0.6 * hash2(id + 72.0)) / 1.2;
			float size = 0.018 + 0.03 * hash(id + 73.0);
			colour += galaxy(far, centre, size, hash(id + 74.0) * 6.28, 0.3 + 0.6 * hash(id + 75.0), time * 0.02 * (0.5 + hash(id + 76.0)), hash(id + 77.0) > 0.5 ? 2.0 : 3.0) * 1.1;
		}
	}

	colour += meteor(screen, aspect, 11.0, 0.0);
	colour += meteor(screen, aspect, 17.0, 1.0);

	// Half a step of dither, as the dark gradients would band in eight bits.
	colour += (hash(frag) - 0.5) / 255.0;
	out_Colour = vec4(max(colour, 0.0), 1.0);
}
