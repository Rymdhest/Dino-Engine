float hash11(float p) {
    p = fract(p * 0.1031);
    p += dot(vec2(p, p), vec2(19.19, 13.13));
    return fract(p * p);
}
vec2 hash12(float p) {
    vec2 p2 = fract(vec2(p * 0.1031, p * 0.1030));
    p2 += dot(p2, p2.yx + 19.19);
    return fract(vec2(p2.x * p2.y, p2.y * p2.x));
}
vec2 hash22(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 19.19);
    return fract((p3.xx + p3.yz) * p3.zy);
}
float hash21(vec2 p) {
    p = fract(p * vec2(0.1031, 0.1030));
    p += dot(p, p + vec2(19.19, 19.19));
    p = fract(vec2(p.x * p.y, p.y * p.x) * 19.19);
    return fract(p.x + p.y);
}
vec3 hash13(float t) {
    float x = hash11(t);
    float y = fract(sin(113.1 * x) * 6652.0);
    float z = fract(sin(1755.1 * y) * 51336.0);

    return vec3(x, y, z);
}

vec3 hash23(vec2 p) {
    vec3 p3 = fract(vec3(p.x * 0.1031, p.y * 0.1030, p.x * p.y * 0.0973));
    p3 += dot(p3, p3.yzx + 19.19);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}