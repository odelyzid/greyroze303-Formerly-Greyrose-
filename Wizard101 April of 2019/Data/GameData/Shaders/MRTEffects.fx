// EMERGENT GAME TECHNOLOGIES PROPRIETARY INFORMATION
//
// This software is supplied under the terms of a license agreement or
// nondisclosure agreement with Emergent Game Technologies and may not 
// be copied or disclosed except in accordance with the terms of that 
// agreement.
//
//      Copyright (c) 1996-2005 Emergent Game Technologies.
//      All Rights Reserved.
//
// Emergent Game Technologies, Chapel Hill, North Carolina 27514
// http://www.emergentgametech.com

//---------------------------------------------------------------------------
// MRTEffects.fx
// Implements a set of image post-processing effects that use multiple render
// targets.
// 
// The effects implemented here include:
// 
// MRT_NightVision - blur and depth of field with noise
// MRT_NormalEdgeDetect - edge detection achieved by comparing per-pixel
//  normals
// MRT_PassThrough - simply outputs the color texture with no modifications
//---------------------------------------------------------------------------

//---------------------------------------------------------------------------
// Textures and Samplers
//---------------------------------------------------------------------------
texture g_texSrcColor;
sampler2D g_samSrcColor = sampler_state
{
    Texture = (g_texSrcColor);
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Linear;
    MipFilter = Linear;
};

texture g_texSrcNormPos;
sampler2D g_samSrcNormPos = sampler_state
{
    Texture = (g_texSrcNormPos);
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Linear;
    MipFilter = Linear;
};

texture g_texSrcAux;
sampler2D g_samSrcAux = sampler_state
{
    Texture = (g_texSrcAux);
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Linear;
    MipFilter = Linear;
};

texture g_texIRRamp;
sampler1D g_samIRRamp = sampler_state
{
    Texture = (g_texIRRamp);
    AddressU = Clamp;
    MinFilter = Point;
    MagFilter = Linear;
    MipFilter = Linear;
};

texture g_texNoise
<
    string NTM = "Base";
>;
sampler2D g_samNoise = sampler_state
{
    Texture = (g_texNoise);
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Linear;
    MipFilter = Linear;
};

//---------------------------------------------------------------------------
// Constants
//---------------------------------------------------------------------------
static const int gs_iEdgeDetectKernelSize = 4;
float2 g_vEdgeDetectTexelKernel[gs_iEdgeDetectKernelSize] : GLOBAL; 

static const int gs_iBlurKernelSize = 13;
float2 g_vBlurTexelKernelX[gs_iBlurKernelSize] : GLOBAL;
float2 g_vBlurTexelKernelY[gs_iBlurKernelSize] : GLOBAL;
static const float gs_vBlurWeights[gs_iBlurKernelSize] = 
{
    0.002216,
    0.008764,
    0.026995,
    0.064759,
    0.120985,
    0.176033,
    0.199471,
    0.176033,
    0.120985,
    0.064759,
    0.026995,
    0.008764,
    0.002216
};

static const int gs_iBlurDiagonalKernelSize = 5;
float2 g_vBlurDiagonalOffsets[gs_iBlurDiagonalKernelSize] : GLOBAL;
float4 g_vBlurDiagonalWeights[gs_iBlurDiagonalKernelSize] : GLOBAL;

static const float3 gs_vLuminanceConv = {0.2125, 0.7154, 0.0721};
static const float4 gs_vNightVisionMainColor : GLOBAL = {0.5, 0.7, 0.5, 1};
static const float4 gs_vNightVisionAltColor : GLOBAL = {0.3, 0.3, 1, 1};

float4x4 g_matWorldViewProj : WORLDVIEWPROJECTION;

float g_fNearFarScale : GLOBAL = 5600;
float g_fPerturbationMultiplier : GLOBAL = 0.01;
float g_fTrailDistance : GLOBAL = 0.5;
float g_fTrailWidth : GLOBAL = 0.5;
float g_fBlurFactor : GLOBAL = 2;
float g_fDOFDistance : GLOBAL = 100;
float g_fDOFFalloff : GLOBAL = 300;
float g_fShimmerBrightness : GLOBAL = 1;
float g_fEdgeDepthThreshold : GLOBAL = 0.1;

//---------------------------------------------------------------------------
// Functions
//---------------------------------------------------------------------------
float4 BlurColorX(float2 vTexCoord)
{
    // Blurs the texel at vTexCoord in the X direction.

    float4 vColor = 0;
    
    for (int i = 0; i < gs_iBlurKernelSize; i++)
    {
        vColor += tex2D(g_samSrcColor, vTexCoord + g_vBlurTexelKernelX[i] *
            g_fBlurFactor) * gs_vBlurWeights[i];
    }
    
    return vColor;
}
//---------------------------------------------------------------------------
float4 BlurColorY(float2 vTexCoord)
{
    // Blurs the texel at vTexCoord in the Y direction.

    float4 vColor = 0;
    
    for (int i = 0; i < gs_iBlurKernelSize; i++)
    {
        vColor += tex2D(g_samSrcColor, vTexCoord + g_vBlurTexelKernelY[i] *
            g_fBlurFactor) * gs_vBlurWeights[i];
    }
    
    return vColor;
}
//---------------------------------------------------------------------------
float4 BlurDiagonal(float2 vTexCoord)
{
    float4 vColor = 0;

	for (int i = 0; i < gs_iBlurDiagonalKernelSize; i++)
	{
		vColor += g_vBlurDiagonalWeights[i] * tex2D(g_samSrcColor,
            vTexCoord + g_vBlurDiagonalOffsets[i] * g_fBlurFactor);
	}

	return vColor;
}
//---------------------------------------------------------------------------
float GetZDepth(float2 vTexCoord)
{
    return tex2D(g_samSrcNormPos, vTexCoord).w;
}
//---------------------------------------------------------------------------
float4 ApplyDepthOfFieldX(float4 vOrigColor, float2 vTexCoord, float fZDepth)
{
    // Applies a depth of field effect to the texel at vTexCoord with blur
    // in the X direction.
    
    float fDepth = smoothstep(g_fDOFDistance, g_fDOFDistance + g_fDOFFalloff,
        fZDepth);

    return (fDepth * BlurColorX(vTexCoord)) + ((1 - fDepth) * vOrigColor);
}
//---------------------------------------------------------------------------
float4 ApplyDepthOfFieldY(float4 vOrigColor, float2 vTexCoord, float fZDepth)
{
    // Applies a depth of field effect to the texel at vTexCoord with blur
    // in the Y direction.
    
    float fDepth = smoothstep(g_fDOFDistance, g_fDOFDistance + g_fDOFFalloff,
        fZDepth);

    return (fDepth * BlurColorY(vTexCoord)) + ((1 - fDepth) * vOrigColor);
}
//---------------------------------------------------------------------------
float GetLuminance(float4 vOrigColor)
{
    return dot(vOrigColor, gs_vLuminanceConv);
}
//---------------------------------------------------------------------------
float4 GetNightVisionColor(float4 vOrigColor, float4 vTintColor)
{
    return GetLuminance(vOrigColor) * vTintColor;
}
//---------------------------------------------------------------------------
float4 ApplyNoise(float4 vOrigColor, float2 vTexCoord)
{
    return vOrigColor * tex2D(g_samNoise, vTexCoord);
}
//---------------------------------------------------------------------------

//---------------------------------------------------------------------------
// Vertex Shaders
//---------------------------------------------------------------------------
struct VS_OUTPUT
{
    float4 Pos      : POSITION;
    float2 Tex0     : TEXCOORD0;
};

VS_OUTPUT VSMain(float4 inPos: POSITION, float2 inTex0: TEXCOORD0)
{
    VS_OUTPUT Out;
    Out.Pos = mul(inPos, g_matWorldViewProj);
    Out.Tex0 = inTex0;
    return Out;
}

//---------------------------------------------------------------------------
// Pixel Shaders
//---------------------------------------------------------------------------
float4 NightVision_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    float4 vColor = tex2D(g_samSrcColor, vTexCoord);

    float fMask = tex2D(g_samSrcAux, vTexCoord).b;
    if (any(fMask))
    {
        // Night vision conversion based on luminance.
        vColor = GetNightVisionColor(vColor, gs_vNightVisionAltColor);
    }
    else
    {
        // Diagonal Gaussian blur.
        vColor = BlurDiagonal(vTexCoord);

        // Night vision conversion based on luminance.
        vColor = GetNightVisionColor(vColor, gs_vNightVisionMainColor);
    }

    // Noise.
    vColor = ApplyNoise(vColor, vTexCoord);

    return vColor;
}
//---------------------------------------------------------------------------
float4 Infrared_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    float4 vColor = tex2D(g_samSrcColor, vTexCoord);
    float fLuminance = GetLuminance(vColor);

    float fMask = tex2D(g_samSrcAux, vTexCoord).b;
    if (any(fMask))
    {
        // Infrared conversion based on luminance.
        vColor = tex1D(g_samIRRamp, fLuminance);
    }
    else
    {
        // Multiply luminance to darken and invert.
        vColor = fLuminance * (1 - fLuminance);
    }

    // Noise.
    vColor = ApplyNoise(vColor, vTexCoord);

    return vColor;
}
//---------------------------------------------------------------------------
float4 EdgeDetect_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    float4 vOrigNormPos = tex2D(g_samSrcNormPos, vTexCoord);
    float4 vSumNormPos = 0;

    for(int i = 0; i < gs_iEdgeDetectKernelSize; i++)
    {
        float4 vKernelNormPos = tex2D(g_samSrcNormPos, vTexCoord +
            g_vEdgeDetectTexelKernel[i]);
        vSumNormPos.xyz += 1 - dot(vOrigNormPos.xyz, vKernelNormPos.xyz);
        vSumNormPos.w += abs(vOrigNormPos.w - vKernelNormPos.w) /
            vOrigNormPos.w;
    }
    
    vSumNormPos.w = step(g_fEdgeDepthThreshold, vSumNormPos.w);
    vSumNormPos.xyz += vSumNormPos.w;

    float fMask = tex2D(g_samSrcAux, vTexCoord).b;
    if (any(fMask))
    {
        vSumNormPos = 1 - vSumNormPos;
    }

    return vSumNormPos;
}
//---------------------------------------------------------------------------
float4 PassThrough_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
//float4 col = tex2D(g_samSrcColor, vTexCoord);
//col.r = 1.0;
//return col;
return float4(0,0,0,0);
    //return tex2D(g_samSrcColor, vTexCoord);
}
//---------------------------------------------------------------------------
float4 HeatShimmer_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    float2 vNewTexCoord = vTexCoord;
    float4 vHeatBright = tex2D(g_samSrcAux, vTexCoord);
    float2 vHeat = vHeatBright.rg;
    if (any(vHeat - 0.5))
    {
        vHeat = vHeat * 2 - 1;
        float2 vOffsetTexCoord = vTexCoord + vHeat.xy 
            * g_fPerturbationMultiplier;

        if (any(tex2D(g_samSrcAux, vOffsetTexCoord).rg - 0.5))
        {
            vNewTexCoord = vOffsetTexCoord;
        }
    }

    return tex2D(g_samSrcColor, vNewTexCoord) * (1 
        + g_fShimmerBrightness * vHeatBright.b);
}
//---------------------------------------------------------------------------
float4 DepthOfFieldX_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    return ApplyDepthOfFieldX(tex2D(g_samSrcColor, vTexCoord), vTexCoord,
        GetZDepth(vTexCoord));
}
//---------------------------------------------------------------------------
float4 DepthOfFieldY_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    return ApplyDepthOfFieldY(tex2D(g_samSrcColor, vTexCoord), vTexCoord,
        GetZDepth(vTexCoord));
}
//---------------------------------------------------------------------------
float4 GaussBlurX_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    return BlurColorX(vTexCoord);
}
//---------------------------------------------------------------------------
float4 GaussBlurY_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    return BlurColorY(vTexCoord);
}
//---------------------------------------------------------------------------
float4 Magnify_PS(float2 vTexCoord : TEXCOORD0) : COLOR0
{
    float2 vOffset = vTexCoord * 2 - 1;
    vOffset.y *= 0.75; 
    float fDot = dot(vOffset, vOffset) * 7;
    if (fDot > 0.5)
    {
        fDot = 0.0f;
    }
    vOffset *= fDot;
    
    float3 vMask = tex2D(g_samSrcAux, vTexCoord).rgb;
    if (any(vMask.b))
    {
        vTexCoord += vOffset;
    }

    vMask.rg = vMask.rg * 2 - 1;
    vTexCoord += vMask.rg * g_fPerturbationMultiplier;

    return tex2D(g_samSrcColor, vTexCoord);
}
//---------------------------------------------------------------------------

//---------------------------------------------------------------------------
// Techniques
//---------------------------------------------------------------------------
technique MRT_NightVision
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 NightVision_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_Infrared
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 Infrared_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_EdgeDetect
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 EdgeDetect_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_PassThrough
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 PassThrough_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_HeatShimmer
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 HeatShimmer_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_DepthOfFieldX
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 DepthOfFieldX_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_DepthOfFieldY
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 DepthOfFieldY_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_GaussBlurX
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 GaussBlurX_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_GaussBlurY
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 GaussBlurY_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
technique MRT_Magnify
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 Magnify_PS();
        ZEnable = false;
        AlphaBlendEnable = false;
    }
}
//---------------------------------------------------------------------------
