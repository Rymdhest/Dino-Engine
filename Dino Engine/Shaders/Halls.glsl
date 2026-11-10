#ifndef DINO_HALLS_GLSL
#define DINO_HALLS_GLSL

// The halls near the camera: the buildings that can be walked into (HALLS.md, HallShaderData). Needs Globals.glsl.
// When someone stands in one, its roof and the walls facing the camera go for the camera alone (cutByHall), which the sun's shadows and global illumination
// never see, so the light comes in by its windows as it would. And no light crosses a hall's walls (hallsLetThrough): a candle lights its room, and a lantern
// in the street outside doesn't.

// A point on the ground in a hall's own frame.
vec2 hallLocal(int i, vec2 xz)
{
    vec2 offset = xz - hallBox[i].xy;
    float c = hallFrame[i].x, s = hallFrame[i].y;
    return vec2(dot(offset, vec2(c, -s)), dot(offset, vec2(s, c)));
}

// Whether a point is in a hall's room: inside its walls, over its floor and under where its walls end.
bool insideHall(int i, vec3 p)
{
    vec2 l = hallLocal(i, p.xz);
    vec2 inner = hallBox[i].zw - vec2(hallCut[i].x - 0.05);
    float floorY = hallFrame[i].z;
    return abs(l.x) < inner.x && abs(l.y) < inner.y && p.y > floorY - 2.0 && p.y < floorY + hallFrame[i].w + 0.4;
}

// Whether a point of the ground is under a hall's floor, where the ground isn't drawn: the floor is laid where people stand, and the ground is drawn a little above
// that (TerrainRenderer.SurfaceOffset), so it would cover it. Its sills stand over the ground in its doorways, which is still drawn.
bool underHallFloor(vec3 p)
{
    int count = int(hallCutaway.z + 0.5);
    for (int i = 0; i < count; i++)
    {
        if (insideHall(i, p) && p.y < hallFrame[i].z + 0.5) return true;
    }
    return false;
}

// 0 where a hall's wall stands between a light and a point, one of them in its room and the other not; 1 otherwise.
float hallsLetThrough(vec3 light, vec3 p)
{
    int count = int(hallCutaway.z + 0.5);
    for (int i = 0; i < count; i++)
    {
        if (insideHall(i, light) != insideHall(i, p)) return 0.0;
    }
    return 1.0;
}

// How much of the open sky reaches a point: much less in a hall's room, where it comes in only by the doors and windows. For the flat sky that stands in where
// global illumination has no answer.
float hallShelter(vec3 p)
{
    int count = int(hallCutaway.z + 0.5);
    for (int i = 0; i < count; i++)
    {
        if (insideHall(i, p)) return 0.22;
    }
    return 1.0;
}

// What a hall's room gives back of the light that comes in by its doors and windows, which global illumination gathers too faithfully for a camera that can't
// adapt the way an eye does: a share of the sky and of the sun, warmed by the wood and lime it falls on. Nothing at all with the sky and sun out.
vec3 hallRoomLight(vec3 p, vec3 sky, vec3 sun)
{
    if (hallShelter(p) >= 1.0) return vec3(0.0);
    return (sky * 0.3 + sun * 0.018) * vec3(1.0, 0.86, 0.68);
}

// The height above which the hall the camera looks into is cut away at a point, or a very large number where nothing is: above its walls over all of it and
// a little past them, where its roof is, and above its front cut in the walls facing the camera and just in front of them.
float hallCutHeight(vec3 p)
{
    if (hallCutaway.x < -0.5 || hallCutaway.y <= 0.0) return 1e9;
    int i = int(hallCutaway.x + 0.5);
    vec2 l = hallLocal(i, p.xz);
    vec2 h = hallBox[i].zw;
    float eaves = hallCut[i].z;
    float reach = hallCut[i].w;
    float band = hallCut[i].x + 0.6;
    float floorY = hallFrame[i].z;
    float c = hallFrame[i].x, s = hallFrame[i].y;

    bool roof = abs(l.x) < h.x + eaves && abs(l.y) < h.y + eaves;
    // A wall faces the camera, which looks along +z, when its outward normal has a z below -0.3. The +x wall's is -s, the -x wall's s, the front's c and the back's -c.
    bool front = false;
    if (s > 0.3) front = front || (h.x - l.x < band && h.x - l.x > -reach && abs(l.y) < h.y + reach);
    if (s < -0.3) front = front || (h.x + l.x < band && h.x + l.x > -reach && abs(l.y) < h.y + reach);
    if (c < -0.3) front = front || (h.y - l.y < band && h.y - l.y > -reach && abs(l.x) < h.x + reach);
    if (c > 0.3) front = front || (h.y + l.y < band && h.y + l.y > -reach && abs(l.x) < h.x + reach);
    if (front) return floorY + hallCut[i].y;
    if (roof) return floorY + hallFrame[i].w;
    return 1e9;
}

// 1 where the hall the camera looks into is cut away at a point, 0 where it stands.
float cutByHall(vec3 p)
{
    return p.y > hallCutHeight(p) + 0.02 ? 1.0 : 0.0;
}

// How far a point is in what the open hall has cut away, softened over `soft` metres at the cut's edges and its height, for what's soft at its edges anyway:
// smoke from a chimney the cut took away goes with it, and doesn't pop as it drifts out.
float cutByHallSoft(vec3 p, float soft)
{
    if (hallCutaway.x < -0.5 || hallCutaway.y <= 0.0) return 0.0;
    int i = int(hallCutaway.x + 0.5);
    vec2 l = hallLocal(i, p.xz);
    vec2 reach = abs(l) - (hallBox[i].zw + vec2(hallCut[i].z));
    float inside = 1.0 - smoothstep(-soft, soft, max(reach.x, reach.y));
    float above = smoothstep(-soft, soft, p.y - (hallFrame[i].z + hallFrame[i].w));
    return inside * above * hallCutaway.y;
}

// Whether the back of a face seen here is the inside of something the cut has opened, which is drawn as the cut's own face: solid, flat and lit from above.
bool hallCapped(vec3 p)
{
    float cut = hallCutHeight(p);
    return cut < 1e8 && p.y > cut - 4.0;
}

#endif
