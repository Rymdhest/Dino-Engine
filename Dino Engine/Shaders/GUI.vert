#version 330 core

// One instance per GUI quad, expanded to a 4 vertex triangle strip using gl_VertexID.
layout(location = 0) in vec2 iCenter;
layout(location = 1) in vec2 iHalfSize;
layout(location = 2) in vec2 iAxis;
layout(location = 3) in vec4 iUV;
layout(location = 4) in vec4 iColor;
layout(location = 5) in vec4 iBorderColor;
layout(location = 6) in vec4 iParams;
layout(location = 7) in float iExpand;

uniform vec2 uScreenSize;

out vec2 vUV;
out vec2 vLocal;
flat out vec4 vColor;
flat out vec4 vBorderColor;
flat out vec2 vHalfSize;
flat out vec4 vParams;

void main(void){
	// strip order: (-1,-1) (1,-1) (-1,1) (1,1)
	vec2 corner = vec2((gl_VertexID & 1) != 0 ? 1.0 : -1.0, (gl_VertexID & 2) != 0 ? 1.0 : -1.0);
	vec2 local = corner * (iHalfSize + vec2(iExpand));
	vec2 axisY = vec2(-iAxis.y, iAxis.x);
	vec2 position = iCenter + iAxis * local.x + axisY * local.y;

	// GUI units, origin top-left, y down -> normalized device coordinates
	vec2 ndc = position / uScreenSize * 2.0 - 1.0;
	gl_Position = vec4(ndc.x, -ndc.y, 0.0, 1.0);
	vUV = mix(iUV.xy, iUV.zw, corner * 0.5 + 0.5);
	vLocal = local;
	vColor = iColor;
	vBorderColor = iBorderColor;
	vHalfSize = iHalfSize;
	vParams = iParams;
}
