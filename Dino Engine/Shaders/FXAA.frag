#version 420

#include globals.glsl

uniform sampler2D l_tex;

//layout( location = 0 ) out vec4 def_e; //the manipulated color output
out vec4 out_Colour;

uniform float reduceMin;
uniform float reduceMul;
uniform float spanMax;

//optimized version for mobile, where dependent
//texture reads can be a bottleneck
vec4 fxaa(sampler2D tex, vec2 fragCoord, vec2 resolution) {
    vec4 color;

  vec2 inverseVP = 1.0 / resolution.xy;

  vec2 v_rgbNW = (fragCoord + vec2(-1.0, -1.0)) * inverseVP;
  vec2 v_rgbNE = (fragCoord + vec2(1.0, -1.0)) * inverseVP;
  vec2 v_rgbSW = (fragCoord + vec2(-1.0, 1.0)) * inverseVP;
  vec2 v_rgbSE = (fragCoord + vec2(1.0, 1.0)) * inverseVP;
  vec2 v_rgbM = vec2(fragCoord * inverseVP);

  vec3 rgbNW = texture2D(tex, v_rgbNW).xyz;
  vec3 rgbNE = texture2D(tex, v_rgbNE).xyz;
  vec3 rgbSW = texture2D(tex, v_rgbSW).xyz;
  vec3 rgbSE = texture2D(tex, v_rgbSE).xyz;
  vec4 texColor = texture2D(tex, v_rgbM);
  vec3 rgbM  = texColor.xyz;
  vec3 luma = vec3(0.299, 0.587, 0.114);
  float lumaNW = dot(rgbNW, luma);
  float lumaNE = dot(rgbNE, luma);
  float lumaSW = dot(rgbSW, luma);
  float lumaSE = dot(rgbSE, luma);
  float lumaM  = dot(rgbM,  luma);
  float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
  float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
  float FXAA_REDUCE_MIN = (1.0/ reduceMin);
  float FXAA_REDUCE_MUL = (1.0 / reduceMul);
  float FXAA_SPAN_MAX = spanMax;

  mediump vec2 dir;
  dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
  dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

  float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) *
                        (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);

  float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
  dir = min(vec2(FXAA_SPAN_MAX, FXAA_SPAN_MAX),
            max(vec2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX),
            dir * rcpDirMin)) * inverseVP;

  vec3 rgbA = 0.5 * (
      texture2D(tex, fragCoord * inverseVP + dir * (1.0 / 3.0 - 0.5)).xyz +
      texture2D(tex, fragCoord * inverseVP + dir * (2.0 / 3.0 - 0.5)).xyz);
  vec3 rgbB = rgbA * 0.5 + 0.25 * (
      texture2D(tex, fragCoord * inverseVP + dir * -0.5).xyz +
      texture2D(tex, fragCoord * inverseVP + dir * 0.5).xyz);
 
  float lumaB = dot(rgbB, luma);
  if ((lumaB < lumaMin) || (lumaB > lumaMax))
      color = vec4(rgbA, texColor.a);
  else
      color = vec4(rgbB, texColor.a);
  return color;
}

void main () {
	out_Colour.rgb = fxaa(l_tex,gl_FragCoord.st,resolution).rgb;
	out_Colour.a = 1.0f;
}
