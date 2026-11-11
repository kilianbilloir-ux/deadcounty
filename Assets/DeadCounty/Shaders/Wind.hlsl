#ifndef DEADCOUNTY_WIND_INCLUDED
#define DEADCOUNTY_WIND_INCLUDED

// Set by WindDirector. xy: unit direction on XZ the wind blows to, z: strength from 0 to 1, w: its clock (s).
float4 _DcWind;

// weights.x: how far the vertex leans downwind at full strength (m), weights.y: how far it flutters (m).
// Phases come from the world position, so gusts roll across the map and no two plants move together; vertices
// sharing a position move alike, which keeps flat-shaded triangles joined.
float3 WindDisplace(float3 positionOS, float2 weights)
{
    float3 world = TransformObjectToWorld(positionOS);
    float2 dir = _DcWind.xy;
    float2 side = float2(-dir.y, dir.x);
    float t = _DcWind.w;

    float gust = 0.5 + 0.5 * sin(dot(world.xz, dir) * 0.15 - t * 1.1);
    float lean = 0.3 + 0.7 * gust * gust + 0.25 * sin(t * 1.9 + dot(world.xz, float2(0.23, 0.31)));
    float drift = 0.2 * sin(t * 1.3 + dot(world.xz, float2(0.19, 0.11)));
    float3 phase = world * float3(2.1, 1.7, 2.3);
    float3 flutter = float3(sin(t * 6.1 + phase.z + phase.y), sin(t * 5.3 + phase.x), sin(t * 6.7 + phase.x + phase.y));

    world.xz += (dir * lean + side * drift) * (weights.x * _DcWind.z);
    world += flutter * (weights.y * _DcWind.z * (0.4 + 0.6 * gust));
    return TransformWorldToObject(world);
}

// Wraps a URP vertex function: same inputs plus the weights in UV3, which meshes built without them read as zero.
#define DC_WIND_VERTEX(positionField, stockVertex) \
    struct WindAttributes \
    { \
        Attributes body; \
        float2 wind : TEXCOORD3; \
    }; \
    Varyings WindVertex(WindAttributes input) \
    { \
        UNITY_SETUP_INSTANCE_ID(input.body); \
        input.body.positionField.xyz = WindDisplace(input.body.positionField.xyz, input.wind); \
        return stockVertex(input.body); \
    }

#endif
