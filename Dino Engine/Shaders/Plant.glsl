#ifndef DINO_PLANT_GLSL
#define DINO_PLANT_GLSL

// How plants bend, in the manner of the joints of Ghost of Tsushima's plants: a plant is its PlantJoints, which its generator built along with it, and
// a vertex only says which joint holds it. Modelling/Model/PlantRig.cs is the CPU's copy of this: change them together.
//
// Each joint reads what's pushing it once, at one point of its own, so everything it holds moves as one and a sharp edge in what's around it can't tear
// a leaf apart. It then curves into an arc, which keeps the length of the line from its base to its tip, and carries its children whole, by where they sit
// on it. A trunk, its boughs and their leaves are the same thing at three sizes.

#include Wind.glsl

uniform samplerBuffer plantJoints;
uniform sampler2D bendMap;
uniform sampler2D canopyMap;
uniform sampler2D touchMap;
uniform sampler2D windMap;

const int PLANT_MOST_LEVELS = 6;
const float PLANT_FLOOR = 0.03;     // Metres above its own ground that a plant can be pressed to.
const float PLANT_FEEL = 0.6;       // How far along a joint it feels what's around it.
const float PLANT_STIR = 0.35;      // How much a joint shivers as it's let go, for each radian a second it's moving.

vec3 plantRotate(vec3 v, vec3 about, float sine, float cosine)
{
    return v * cosine + cross(about, v) * sine + about * (dot(about, v) * (1.0 - cosine));
}

// How a joint is pushed, in the model's space: the wind it takes from the plants around it, a shiver of its own, and what's walking through it.
vec3 plantLean(vec2 at, mat3 toModel, vec3 axis, float give, float touch, float seed, float flutter, float heavy, float lag)
{
    vec4 state = heavy > 0.5 ? canopyState(canopyMap, windMap, at) : vegetationState(bendMap, windMap, at);
    // A joint runs a little behind what it hangs from, so that a sway goes up a tree and out along its boughs like a whip.
    vec2 lean = (state.xy - lag * state.zw) * give;

    vec4 pushed = touch > 0.0 ? vegetationTouch(touchMap, at) : vec4(0.0);
    lean += pushed.xy * touch;

    if (flutter > 0.0)
    {
        // Rates a quarter apart, as the phase wraps at a whole number of flutters of each (WindField.FlutterCyclesPeriod).
        float kind = floor(seed * 4.0);
        float phase = WIND_TAU * (vegetationParams.w * (1.0 + 0.25 * kind) + seed);
        float windy = min(length(state.xy) / max(vegetationParams.x * 0.35, 1e-3), 1.5);
        vec2 way = vec2(cos(WIND_TAU * seed * 5.0), sin(WIND_TAU * seed * 5.0));
        lean += way * (vegetationParams.z * flutter * (windy + pushed.w * touch * PLANT_STIR) * sin(phase));
    }

    vec3 model = toModel * vec3(lean.x, 0.0, lean.y);

    // Pressed down, it goes over the way it already lies, or some way of its own if it stands straight up. A field read from where a foot is
    // would have no direction here at all, right under it.
    float press = pushed.z * touch;
    if (press > 1e-3)
    {
        vec3 fall = vec3(axis.x, 0.0, axis.z) * 2.0 + vec3(cos(WIND_TAU * seed), 0.0, sin(WIND_TAU * seed));
        model += fall * (press / max(length(fall), 1e-3)) + vec3(0.0, -press, 0.0);
    }
    return model;
}

// A little of its own colour, from where it stands, so that two plants off one mesh don't read as the same plant.
vec3 plantTint(vec2 at)
{
    float bright = fract(sin(dot(at, vec2(12.9898, 78.233))) * 43758.5453);
    float warm = fract(sin(dot(at, vec2(39.3462, 11.1351))) * 24634.6345) - 0.5;
    return (0.9 + 0.2 * bright) * vec3(1.0 + 0.16 * warm, 1.0, 1.0 - 0.22 * warm);
}

// Bends a point held by `joint`, and the directions of the surface there.
void plantPose(int joint, mat4 modelMatrix, inout vec3 position, inout vec3 normal, inout vec3 tangent)
{
    float restHeight = position.y;
    mat3 toModel = transpose(mat3(normalize(modelMatrix[0].xyz), normalize(modelMatrix[1].xyz), normalize(modelMatrix[2].xyz)));

    float along = -1.0;
    for (int level = 0; level < PLANT_MOST_LEVELS && joint >= 0; level++)
    {
        int at = joint * 4;
        vec4 baseLength = texelFetch(plantJoints, at);
        vec4 axisGive = texelFetch(plantJoints, at + 1);
        vec4 family = texelFetch(plantJoints, at + 2);     // parent, where it sits on the parent, touch, most bend
        vec4 nature = texelFetch(plantJoints, at + 3);     // seed, flutter, heavy, lag

        vec3 base = baseLength.xyz;
        float len = baseLength.w;
        vec3 axis = axisGive.xyz;
        if (along < 0.0) along = clamp(dot(position - base, axis) / len, 0.0, 1.0);

        vec2 feel = (modelMatrix * vec4(base + axis * (len * PLANT_FEEL), 1.0)).xz;
        vec3 lean = plantLean(feel, toModel, axis, axisGive.w, family.z, nature.x, nature.y, nature.z, nature.w);

        vec3 turn = cross(axis, lean);
        float amount = length(turn);
        if (amount > 1e-5)
        {
            // It comes to its limit gently: a hard stop would freeze it wherever the wind pushed it there.
            vec3 about = turn / amount;
            float bend = family.w * (1.0 - exp(-amount / family.w));
            float sine = sin(bend * along);
            float cosine = cos(bend * along);

            vec3 side = position - base - axis * (along * len);
            vec3 arc = (axis * sine + cross(about, axis) * (1.0 - cosine)) * (len / bend);
            position = base + arc + plantRotate(side, about, sine, cosine);
            normal = plantRotate(normal, about, sine, cosine);
            tangent = plantRotate(tangent, about, sine, cosine);
        }

        along = family.y;
        joint = int(family.x);
    }
    position.y = max(position.y, min(restHeight, PLANT_FLOOR));
}

#endif
