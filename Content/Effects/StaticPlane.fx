#include "BaseEffect.fxh"

float Fullbright;       // boolean
float AlphaIsEmissive;  // boolean
float IgnoreFog;        // boolean
float SewerHax;         // boolean

DECLARE_TEXTURE(BaseTexture);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float Fog : TEXCOORD2;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 worldPos = TransformPositionToWorld(input.Position);
    worldPos = ApplyEyeParallax(worldPos);
    float4 worldViewPos = TransformWorldToClip(worldPos);
    output.Position = ApplyTexelOffset(worldViewPos);

    output.TexCoord = TransformTexCoord(input.TexCoord);
    output.Normal = TransformNormalToWorld(input.Normal);
    output.Fog = (IgnoreFog != 0.0) ? 0.0 : saturate(1.0 - ApplyFog(output.Position.w));

    return output;
}

float4 PS_Pre(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);

    float emissive = Fullbright;
    if (AlphaIsEmissive != 0.0)
    {
        emissive = texColor.a;
    }

    float alpha = Material_Opacity;
    if (AlphaIsEmissive == 0.0)
    {
        alpha *= texColor.a;
        emissive *= alpha;
    }

    float3 light = ComputeLight(input.Normal, emissive);
    
    float3 color = lerp(light, 1.0, input.Fog);
    if (SewerHax != 0.0 && AlphaIsEmissive == 0.0)
    {
        color = texColor.r > 0.75 ? 1.0 : 0.0;
    }

    return float4(color * 0.5, 1.0);
}

float4 PS_Main(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);

    float alpha = Material_Opacity;
    if (AlphaIsEmissive == 0.0)
    {
        alpha *= texColor.a;
    }

    float3 color = texColor.rgb * Material_Diffuse;
    color = lerp(color, Fog_Color, input.Fog);

    return float4(color, alpha);
}

technique TSM2
{
    pass Pre
    {
        VertexShader = compile vs_2_0 VS();
        PixelShader = compile ps_2_0 PS_Pre();
    }

    pass Main
    {
        VertexShader = compile vs_2_0 VS();
        PixelShader = compile ps_2_0 PS_Main();
    }
}
