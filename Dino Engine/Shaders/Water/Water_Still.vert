#version 420

// A still water body's surface: a flat quad at its level, turned by its yaw (WaterRenderer, one draw per body).

layout (location = 0) in vec2 corner;

out vec3 fragWorldPosition;
out vec2 fragLocalPosition;

uniform mat4 viewProjection;
uniform vec3 bodyCentre;
uniform vec2 bodyAxis;
uniform vec2 bodyQuadExtents;

void main(void)
{
    vec2 local = (corner * 2.0 - 1.0) * bodyQuadExtents;
    vec2 xz = bodyCentre.xz + bodyAxis * local.x + vec2(-bodyAxis.y, bodyAxis.x) * local.y;
    fragWorldPosition = vec3(xz.x, bodyCentre.y, xz.y);
    fragLocalPosition = local;
    gl_Position = viewProjection * vec4(fragWorldPosition, 1.0);
}
