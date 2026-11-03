#version 420

// Screen-space reflections into the multisampled scene. Blended with (source alpha, one minus source alpha), so each
// sample moves towards the reflection by its own metallic: mix(scene, reflection, metallic). Drawn per pixel and per
// sample (MsaaRenderer.DrawSplit).

#include AntiAliasing/Msaa.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D reflectionTexture;
uniform sampler2DMS gMaterials;

void main(void){

	vec3 reflectionColor = texture(reflectionTexture, textureCoords).rgb;
	float metalicness = texelFetch(gMaterials, ivec2(gl_FragCoord.xy), msaaShadingSample()).b;
	out_Colour = vec4(reflectionColor, metalicness);
}
