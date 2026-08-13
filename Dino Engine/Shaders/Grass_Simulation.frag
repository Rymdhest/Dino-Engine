#version 330
layout (location = 0) out vec4 out_Colour;
in vec2 textureCoords;

uniform sampler2D lastTexture;
uniform float delta;
uniform float regenTime;
uniform float time;
uniform vec2 simulationWorldSize;
uniform vec2 simulationWorldPosition;
uniform vec2 deltaPosition;

uniform float globalWindStrength;
uniform vec2 windDirection;

vec2 calcWind(vec2 uv) {
    vec2 worldPos = simulationWorldPosition + uv * simulationWorldSize;
    
    // 1. Orient the waves along the wind direction
    float sweep = dot(worldPos, windDirection);
    
    // 2. Your exact original math, applied to the sweep coordinate
    float force = 0.15f * sin(sweep * 0.77f + time);
    //force += 0.05317f * sin(sweep * 0.124f + time * 2.412);
    force += 0.0539973f * sin(sweep * 0.7567f + time * 3.328995);
    //force += 0.0411973f * sin(sweep * 0.01567f + time * 18.128995);
    
    // 3. Multiply by direction, power, and delta
    return windDirection * (force * globalWindStrength) * delta;
}

void main(void) {
    // If deltaPosition is (0,0), translatedTextureCoords == textureCoords
    // This keeps the texture static in world space.
    vec2 translatedTextureCoords = textureCoords + (deltaPosition / simulationWorldSize);

    vec2 lastBend = texture(lastTexture, translatedTextureCoords).xy;
    
    // Apply decay
    vec2 nextBend = lastBend * (1.0 - min(delta / regenTime, 1.0));
    
    vec2 wind = calcWind(textureCoords);
    out_Colour.xy = nextBend + wind;
}