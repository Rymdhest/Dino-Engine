#version 330

uniform mat4 viewProjection;
uniform vec3 base;
uniform vec3 eye;
uniform vec2 size;

out vec2 along;

const vec2 corners[4] = vec2[](vec2(-1.0, 0.0), vec2(1.0, 0.0), vec2(-1.0, 1.0), vec2(1.0, 1.0));

void main(void){
	vec2 corner = corners[gl_VertexID];

	// Turned to face the eye about the upright, so that it stays a pillar from anywhere and never leans.
	vec2 toEye = eye.xz - base.xz;
	vec2 facing = dot(toEye, toEye) > 1e-4 ? normalize(toEye) : vec2(0.0, 1.0);
	vec3 side = vec3(-facing.y, 0.0, facing.x);

	vec3 world = base + side * (corner.x * size.x) + vec3(0.0, corner.y * size.y, 0.0);
	along = corner;
	gl_Position = viewProjection * vec4(world, 1.0);
}
