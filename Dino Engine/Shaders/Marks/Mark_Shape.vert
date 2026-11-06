#version 420

// One shape's corner of the map of marks. The four corners are worked out here, so nothing needs to be bound but a vertex array.

uniform vec4 box;
uniform vec4 mapRegion;

out vec2 worldXZ;

void main(void)
{
	// box: xy the middle of what this shape covers in the world, zw half its size. mapRegion: xy the corner of the map in the world, zw one over how big it is.
	vec2 corner = vec2((gl_VertexID & 1) * 2 - 1, (gl_VertexID >> 1) * 2 - 1);
	worldXZ = box.xy + corner * box.zw;
	gl_Position = vec4((worldXZ - mapRegion.xy) * mapRegion.zw * 2.0 - 1.0, 0.0, 1.0);
}
