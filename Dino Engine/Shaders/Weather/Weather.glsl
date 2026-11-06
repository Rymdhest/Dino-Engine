#ifndef DINO_WEATHER_GLSL
#define DINO_WEATHER_GLSL

// The engine's weather as shaders read it. What the weather is doing comes from the world's WeatherField: WeatherSystem bakes it into a map of the whole world, and the block
// below holds what's happening at the camera's focus and how it should look. weatherKind.z is 0 while no weather is on at all, and everything in here costs nothing then.

// Written each frame by WeatherRenderer.UploadWeatherData (binding WeatherRenderer.UniformBinding); the layout must match it.
layout(std140, binding = 9) uniform WeatherData {
    vec4 weatherAir;        // At the focus: x cloud, y storm, z mist, w dust
    vec4 weatherKind;       // x share of what falls that's snow, y that's ash, z 1 while any weather is on, w 1 while anything is wet or falling anywhere
    vec4 weatherMapRegion;  // xy world corner of the weather maps, zw their size in metres
    vec4 weatherFlash;      // rgb the light of the lightning at this moment
    vec4 weatherFalling;    // xyz unit vector things fall along (the wind leans it), w metres per second
    vec4 weatherSun;        // xyz unit vector towards the sun, w how much of it the thickest cloud lets through
    vec4 weatherSunColour;  // rgb the sun's radiance by this hour, w strength of the drawn sky
    vec4 cloudShape;        // x altitude of the base (m), y thickness (m), z 1 / size (1/m), w shape phase
    vec4 cloudMotion;       // xy pattern offset (tiles), zw unused
    vec4 cloudRegion;       // xy world corner of the cloud shadow map, zw its size in metres
    vec4 wetLook;           // x what a soaked surface darkens to, y gloss, z puddle level, w 1 / ripple tile (1/m)
    vec4 rippleLook;        // x strength of the rain rings, yzw the sky's colour with no weather at all
    vec4 dripLook;          // x seconds on the drips' clock, y how freely things drip, 0..1
    vec4 dripRegion;        // xy world corner of the map of what drips do to the ground, zw its size in metres
    mat4 rainShadowMatrix;  // World space to the rain shadow's clip space
    vec4 rainShadowInfo;    // x 1 once captured, y metres of depth it covers
    vec4 weatherMaterials[32];  // How thirsty each ground and model material is, four to a vec4; see weatherPorosity()
};

const int WEATHER_MATERIALS = 128;
const float WEATHER_FOLIAGE_POROSITY = 0.12;    // Leaves are waxy: water beads on them and they shine, and they hardly darken.

vec2 weatherMapCoord(vec2 xz)
{
    return (xz - weatherMapRegion.xy) / weatherMapRegion.zw;
}

// How much water a material takes in, 0..1 (TextureGenerator.Porosity). Models past the world's own materials are the cards leaves and twigs are drawn on.
float weatherPorosity(float materialIndex, int groundMaterials)
{
    int index = int(materialIndex + 0.5);
    if (index >= groundMaterials || index >= WEATHER_MATERIALS) return WEATHER_FOLIAGE_POROSITY;
    return weatherMaterials[index >> 2][index & 3];
}

#endif
