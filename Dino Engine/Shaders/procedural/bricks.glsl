vec3 bricks(vec2 uv, vec2 numBricks, float smoothness, float spacing)
{
    vec2 uBrickSize = vec2(1.0) / numBricks;      // Size of a single brick

    float ratio = numBricks.x / numBricks.y;
    vec2 uMortarThickness = vec2(spacing, spacing / ratio);
    if (numBricks.x > numBricks.y) {
        ratio = numBricks.y / numBricks.x;
        uMortarThickness = vec2(spacing / ratio, spacing);
    }

    // Scale the texture coordinates for tiling
    vec2 gridUV = uv / uBrickSize;

    // Separate into integer and fractional parts
    vec2 brickCoords = floor(gridUV);   // Integer part: grid cell position
    vec2 mortarCoords = fract(gridUV);   // Fractional part: inside the brick

    // Offset every other row to create the brick-laying pattern
    float rowOffset = mod(brickCoords.y, 2.0) * 0.5;
    mortarCoords.x = fract(mortarCoords.x + rowOffset);

    brickCoords = floor(gridUV + vec2(rowOffset, 0.0));   // Integer part: grid cell position
    brickCoords.x = fract(brickCoords.x / numBricks.x);

    // Calculate the distances to the mortar edges
    float mortarDistX = min(mortarCoords.x, 1.0 - mortarCoords.x); // Distance to vertical edges
    float mortarDistY = min(mortarCoords.y, 1.0 - mortarCoords.y); // Distance to horizontal edges

    float mortarBlendX = smoothstep(uMortarThickness.x - smoothness, uMortarThickness.x + smoothness, mortarDistX);
    float mortarBlendY = smoothstep(uMortarThickness.y - smoothness, uMortarThickness.y + smoothness, mortarDistY);

    float mortarBlend = 1.0 - (mortarBlendX * mortarBlendY);

    float value = mix(1.0, 0.0, mortarBlend);

    return vec3(value, brickCoords);
}