#version 330

in vec2 textureCoords;

uniform sampler2D shadedColourTexture;
uniform sampler2D gPosition;

uniform float fogDensity;
uniform float heightFallOff;
uniform vec3 fogColor;
uniform float time;
uniform float noiseFactor;

uniform mat4 inverseViewMatrix;

uniform vec3 cameraPosWorldSpace;


layout (location = 0) out vec4 out_Color;


vec3 applyVolumetricFog( in vec3  col,  // color of pixel
               in float t,    // distnace to point
               in vec3  ro,   // camera position
               in vec3  rd,// camera to point vector
               in float b,//density
               in float a)  //height something
{

    float fogAmount = (a/b) * exp(-ro.y*b) * (1.0-exp(-t*rd.y*b))/rd.y;
    return mix( col, fogColor, clamp(fogAmount, 0f, 1f) );
}

vec3 applySimpleFog( in vec3  col, float depth)
{
    float fogAmount = 1f -exp(-fogDensity *fogDensity*depth*depth);
    return mix(col, fogColor, fogAmount);
}


float rand(in vec4 p) {
	return fract(sin(p.x*1234. + p.y*2345. + p.z*3456. + p.w*4567.) * 5678.);
}

float smoothnoise(in vec4 p) {
    const vec2 e = vec2(0.0, 1.0);
    vec4 i = floor(p);    // integer
    vec4 f = fract(p);    // fract
    
    f = f*f*(3. - 2.*f);
    
    return mix(mix(mix(mix(rand(i + e.xxxx),
                           rand(i + e.yxxx), f.x),
                       mix(rand(i + e.xyxx),
                           rand(i + e.yyxx), f.x), f.y),
                   mix(mix(rand(i + e.xxyx),
                           rand(i + e.yxyx), f.x),
                       mix(rand(i + e.xyyx),
                           rand(i + e.yyyx), f.x), f.y), f.z),
               mix(mix(mix(rand(i + e.xxxy),
                           rand(i + e.yxxy), f.x),
                       mix(rand(i + e.xyxy),
                           rand(i + e.yyxy), f.x), f.y),
                   mix(mix(rand(i + e.xxyy),
                           rand(i + e.yxyy), f.x),
                       mix(rand(i + e.xyyy),
                           rand(i + e.yyyy), f.x), f.y), f.z), f.w);
}

float noise(in vec4 p) {
    float s = 0.;
    float pow2 = 1.;
    for (int i = 0; i < 5; ++i) {
	    s += smoothnoise(p * pow2) / pow2;
        pow2 *= 2.;
    }
    return s / 2.;
}


void main() {
	float depth = texture(gPosition, textureCoords).z;
	vec3 viewPosition = texture(gPosition, textureCoords).xyz;
	vec3 baseColor = texture(shadedColourTexture, textureCoords).rgb;


    vec3 worldPosition = (vec4(viewPosition, 1.0)*inverseViewMatrix).xyz;

    float noiseValue = noise( vec4(worldPosition.xyz*0.3, time*0.99f));
	out_Color.rgb = applyVolumetricFog(baseColor, length(viewPosition), cameraPosWorldSpace,normalize(worldPosition-cameraPosWorldSpace), fogDensity, heightFallOff-heightFallOff*noiseValue*noiseFactor);
    
}