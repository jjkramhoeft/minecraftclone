// Terrain shader for greedy-meshed chunks. The one thing BasicEffect can't
// do: tile an atlas texture across a merged quad. Vertices carry an unbounded
// local UV (block units) plus the tile's atlas origin; the pixel shader wraps
// with frac() so one big quad repeats its 16px tile per block. Fog is radial
// from the camera in the sky color, diffuse tint matches BasicEffect.

#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 ViewProjection;
float3 DiffuseColor;
float3 FogColor;
float FogStart;
float FogEnd;
float3 CameraPosition;
float2 TileSpan; // atlas UV extent of one tile's sampled area

Texture2D AtlasTexture;
sampler2D AtlasSampler = sampler_state
{
    Texture = <AtlasTexture>;
    MipFilter = Point;
    MagFilter = Point;
    MinFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 LocalUV : TEXCOORD0;
    float2 TileOrigin : TEXCOORD1;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 LocalUV : TEXCOORD0;
    float2 TileOrigin : TEXCOORD1;
    float Fog : TEXCOORD2;
};

VSOutput MainVS(VSInput input)
{
    VSOutput output;
    float4 worldPos = mul(input.Position, World);
    output.Position = mul(worldPos, ViewProjection);
    output.Color = input.Color;
    output.LocalUV = input.LocalUV;
    output.TileOrigin = input.TileOrigin;
    output.Fog = saturate((distance(worldPos.xyz, CameraPosition) - FogStart) / (FogEnd - FogStart));
    return output;
}

float4 MainPS(VSOutput input) : COLOR
{
    float2 uv = input.TileOrigin + frac(input.LocalUV) * TileSpan;
    float4 tex = tex2D(AtlasSampler, uv);
    float3 color = tex.rgb * input.Color.rgb * DiffuseColor;
    color = lerp(color, FogColor, input.Fog);
    return float4(color, tex.a * input.Color.a);
}

technique Terrain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
