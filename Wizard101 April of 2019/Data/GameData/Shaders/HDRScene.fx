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
// HDRScene.fx
// Implements all the techniques necessary to perform "HDR Lighting".  
// 1) Average scene luminance calculation
// 2) Bright-pass filtering
// 3) "Bloom" operations (via downsizing/gauss blurring)
// 4) Final scene compositing
//
// Based directly on concepts from the Microsoft DirectX9 SDK "HDR Lighting"
// sample.
// 
//---------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// **GLOBALS AND CONSTANTS**
//-----------------------------------------------------------------------------

static const int MAX_SAMPLES = 16;

// The per-color weighting to be used for luminance calculations in RGB order.
static const float3 LUMINANCE_VECTOR = float3(0.2125f, 0.7154f, 0.0721f);

// Clamp for maximum luminance -- sqrt(MAX_HALF), based on D3DX_16F_MAX
static const float SQRT_MAX_HALF = 255.9375f;

//-----------------------------------------------------------------------------

// Threshold for BrightPass filter
float gfBrightPassThreshold : GLOBAL = 1.0f;  
// Offset for BrightPass filter
float gfBrightPassOffset : GLOBAL = 40.0f;

// Tone mapping variables
// The middle gray key value
float gfMiddleGray : GLOBAL = 0.30f; 

// Bloom scale
float gfBloomScale : GLOBAL = 5.0f;

// Time in seconds since the last calculation
float gfElapsedTime : GLOBAL;
float gfAssumedHz : GLOBAL = 30.0f;
// 0 to 1
float gfAdaptationScale : GLOBAL = 0.9999f;

float2 gakSampleOffsets[MAX_SAMPLES] : GLOBAL;
float4 gakSampleWeights[MAX_SAMPLES] : GLOBAL;

//-----------------------------------------------------------------------------
// **TEXTURES AND SAMPLERS**
//-----------------------------------------------------------------------------
texture BaseTex
< 
    string NTM = "base";
>;

texture Shader0Tex
< 
    bool hidden = true;
    string NTM = "shader";
    int NTMIndex = 0;
>;

texture Shader1Tex
< 
    bool hidden = true;
    string NTM = "shader";
    int NTMIndex = 1;
>;

sampler2D BaseLinearClampSampler =
sampler_state
{
    Texture = <BaseTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D BasePointClampSampler =
sampler_state
{
    Texture = <BaseTex>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D Shader0PointClampSampler =
sampler_state
{
    Texture = <Shader0Tex>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D Shader0LinearClampSampler =
sampler_state
{
    Texture = <Shader0Tex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D Shader1PointClampSampler =
sampler_state
{
    Texture = <Shader1Tex>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//-----------------------------------------------------------------------------
// **SHADER PROGRAMS**
//-----------------------------------------------------------------------------
//---------------------------------------------------------------------------
// Vertex Shaders
//---------------------------------------------------------------------------
float4x4 WorldViewProj : WORLDVIEWPROJECTION;

struct VS_OUTPUT
{
    float4 Pos      : POSITION;
    float2 Tex0     : TEXCOORD0;
};

VS_OUTPUT VSMain(float4 inPos: POSITION, float2 inTex0: TEXCOORD0)
{
    VS_OUTPUT Out;
    Out.Pos = mul(inPos, WorldViewProj);
    Out.Tex0 = inTex0;
    return Out;
}

//-----------------------------------------------------------------------------
// Pixel Shader: DownScale4x4
// Desc: Scale the source texture down to 1/16 scale
//-----------------------------------------------------------------------------
float4 DownScale4x4(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float4 kSample = 0.0f;

	for (int i = 0; i < 16; i++)
	{
		kSample += tex2D(BasePointClampSampler, kScreenPosition + 
            gakSampleOffsets[i]);
	}
    
	return kSample / 16;
}
//-----------------------------------------------------------------------------
// Pixel Shader: SampleLumInitial
// Desc: Sample the luminance of the source image using a kernal of sample
//       points, and return a scaled image containing the log() of averages
//-----------------------------------------------------------------------------
float4 SampleLumInitial(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float3 kSample = 0.0f;
    float  fLogLumSum = 0.0f;

    for (int iSample = 0; iSample < 9; iSample++)
    {
        // Compute the sum of log(luminance) throughout the sample points
        kSample = tex2D(BaseLinearClampSampler, 
            kScreenPosition + gakSampleOffsets[iSample]);
        fLogLumSum += log(dot(kSample, LUMINANCE_VECTOR) + 0.0001f);
    }
    
    // Divide the sum to complete the average
    fLogLumSum /= 9;

    return float4(fLogLumSum, fLogLumSum, fLogLumSum, 1.0f);
}
//-----------------------------------------------------------------------------
// Pixel Shader: SampleLumIterative
// Desc: Scale down the luminance texture by blending sample points
//-----------------------------------------------------------------------------
float4 SampleLumIterative(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float fResampleSum = 0.0f; 
    
    for (int iSample = 0; iSample < 16; iSample++)
    {
        // Compute the sum of luminance throughout the sample points
        fResampleSum += tex2D(BasePointClampSampler, 
            kScreenPosition + gakSampleOffsets[iSample]);
    }
    
    // Divide the sum to complete the average
    fResampleSum /= 16;

    return float4(fResampleSum, fResampleSum, fResampleSum, 1.0f);
}
//-----------------------------------------------------------------------------
// Pixel Shader: SampleLumFinal
// Desc: Extract the average luminance of the image by completing the averaging
//       and taking the exp() of the result
//-----------------------------------------------------------------------------
float4 SampleLumFinal(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float fResampleSum = 0.0f;
    
    for (int iSample = 0; iSample < 16; iSample++)
    {
        // Compute the sum of luminance throughout the sample points
        fResampleSum += tex2D(BasePointClampSampler, 
            kScreenPosition + gakSampleOffsets[iSample]);
    }

    // Divide the sum to complete the average, and perform an exp() to complete
    // the average luminance calculation
    fResampleSum = exp(fResampleSum / 16);
    
    fResampleSum = min(SQRT_MAX_HALF, fResampleSum);
    fResampleSum = max(0.0f, fResampleSum);

    return float4(fResampleSum, fResampleSum, fResampleSum, 1.0f);
}
//-----------------------------------------------------------------------------
// Pixel Shader: CalculateAdaptedLum
// Desc: Calculate the luminance that the camera is current adapted to, using
//       the most recented adaptation level, the current scene luminance, and
//       the time elapsed since last calculated
//-----------------------------------------------------------------------------
float4 CalculateAdaptedLum(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float fAdaptedLum = tex2D(BasePointClampSampler, float2(0.5f, 0.5f));
    float fCurrentLum = tex2D(Shader0PointClampSampler, float2(0.5f, 0.5f));
    
    // The user's adapted luminance level is simulated by closing 
    // the gap between adapted luminance and current luminance by 
    // 2% every frame, based on an assumed frame rate. This is not an accurate 
    // model of human adaptation, which can take longer than half an hour.
    float fNewAdaptation = fAdaptedLum + (fCurrentLum - fAdaptedLum) * 
        (1 - pow(gfAdaptationScale, gfAssumedHz * gfElapsedTime));

    return float4(fNewAdaptation, fNewAdaptation, fNewAdaptation, 1.0f);
}

//-----------------------------------------------------------------------------
// Pixel Shader: BrightPassFilter
// Desc: Perform a high-pass filter on the source texture
//-----------------------------------------------------------------------------
float4 BrightPassFilter(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
	float4 kSample = tex2D(BasePointClampSampler, kScreenPosition );
	float  fAdaptedLum = tex2D(Shader0PointClampSampler, float2(0.5f, 0.5f) );
	
	// Determine what the pixel's value will be after tone-mapping occurs
	kSample.rgb *= gfMiddleGray / (fAdaptedLum + 0.001f);
	
	// Subtract out dark pixels
	kSample.rgb -= gfBrightPassThreshold;
	
	// Clamp to 0
	kSample = max(kSample, 0.0f);
	
	// Map the resulting value into the 0 to 1 range. Higher values for
	// gfBrightPassOffset will isolate lights from illuminated scene objects.
	kSample.rgb /= (gfBrightPassOffset + kSample);
    
	return kSample;
}

//-----------------------------------------------------------------------------
// Pixel Shader: GaussBlur5x5
// Desc: Simulate a 5x5 kernel gaussian blur by sampling the 13 points closest
//       to the center point.
//-----------------------------------------------------------------------------
float4 GaussBlur5x5(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float4 kSample = 0.0f;

	for (int i = 0; i <= 12; i++)
	{
		kSample += gakSampleWeights[i] * tex2D(BasePointClampSampler, 
            kScreenPosition + gakSampleOffsets[i] );
	}

	return kSample;
}

//-----------------------------------------------------------------------------
// Pixel Shader: DownScale2x2
// Desc: Scale the source texture down to 1/4 scale
//-----------------------------------------------------------------------------
float4 DownScale2x2(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float4 kSample = 0.0f;

	for (int i = 0; i < 4; i++)
	{
		kSample += tex2D(BasePointClampSampler, 
            kScreenPosition + gakSampleOffsets[i] );
	}
    
	return kSample / 4;
}

//-----------------------------------------------------------------------------
// Pixel Shader: Bloom
// Desc: Blur the source image along one axis using a gaussian
//       distribution. Since gaussian blurs are separable, this shader is 
//       called twice; first along the horizontal axis, then along the 
//       vertical axis.
//-----------------------------------------------------------------------------
float4 Bloom(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float4 kSample = 0.0f;
    float4 kColor = 0.0f;
        
    float2 kSamplePosition;
    
    // Perform a one-directional gaussian blur
    for (int iSample = 0; iSample < 15; iSample++)
    {
        kSamplePosition = kScreenPosition + gakSampleOffsets[iSample];
        kColor = tex2D(BasePointClampSampler, kSamplePosition);
        kSample += gakSampleWeights[iSample] * kColor;
    }
    
    return kSample;
}

//-----------------------------------------------------------------------------
// Pixel Shader: FinalScenePass
// Desc: Tone map the scene and add post-processed light effects
//-----------------------------------------------------------------------------
float4 FinalScenePass(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float4 kSample = tex2D(BasePointClampSampler, kScreenPosition);
    float4 kBloom = tex2D(Shader0LinearClampSampler, kScreenPosition);
	float fAdaptedLum = tex2D(Shader1PointClampSampler, float2(0.5f, 0.5f));

    // Map the high range of color values into a range appropriate for
    // display, taking into account the user's adaptation level, and selected
    // values for for middle gray and white cutoff.
	kSample.rgb *= (gfMiddleGray / (fAdaptedLum + 0.001f));
	kSample.rgb /= (1.0f + kSample);
    
    // Add the bloom post processing effect
    kSample += gfBloomScale * kBloom;
    
    return kSample;
}

//-----------------------------------------------------------------------------
// Pixel Shader: DebugLum
// Desc: 
//-----------------------------------------------------------------------------
float4 DebugLum(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float kSample = tex2D(BasePointClampSampler, kScreenPosition);

    kSample = kSample * 4.0f;
    
    return float4(kSample, kSample, kSample, 1.0f);
}

//-----------------------------------------------------------------------------
// Pixel Shader: DebugLumLog
// Desc: 
//-----------------------------------------------------------------------------
float4 DebugLumLog(in float2 kScreenPosition : TEXCOORD0) : COLOR
{
    float kSample = tex2D(BasePointClampSampler, kScreenPosition);

    kSample = exp(kSample)*4.0f;
    
    return float4(kSample, kSample, kSample, 1.0f);
}

//-----------------------------------------------------------------------------
// **TECHNIQUES**
//-----------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// Technique: HDRScene_DownScale4x4
// Desc: 
//-----------------------------------------------------------------------------
technique HDRScene_DownScale4x4
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 DownScale4x4();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_SampleAvgLum
// Desc: For luminance measure
//-----------------------------------------------------------------------------
technique HDRScene_SampleAvgLum
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 SampleLumInitial();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_ResampleAvgLum
// Desc: For luminance measure
//-----------------------------------------------------------------------------
technique HDRScene_ResampleAvgLum
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 SampleLumIterative();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_ResampleAvgLumExp
// Desc: For luminance measure
//-----------------------------------------------------------------------------
technique HDRScene_ResampleAvgLumExp
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 SampleLumFinal();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_CalculateAdaptedLum
// Desc: Determines the level of the user's simulated light adaptation level
//       using the last adapted level, the current scene luminance, and the
//       time since last calculation
//-----------------------------------------------------------------------------
technique HDRScene_CalculateAdaptedLum
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 CalculateAdaptedLum();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_BrightPassFilter
// Desc: High pass filter
//-----------------------------------------------------------------------------
technique HDRScene_BrightPassFilter
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 BrightPassFilter();
    }
}


//-----------------------------------------------------------------------------
// Technique: HDRScene_GaussBlur5x5
// Desc: Simulate a 5x5 kernel gaussian blur by sampling the 13 points closest
//       to the center point.
//-----------------------------------------------------------------------------
technique HDRScene_GaussBlur5x5
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 GaussBlur5x5();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_DownScale2x2
// Desc: Scale the source texture down to 1/4 scale
//-----------------------------------------------------------------------------
technique HDRScene_DownScale2x2
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 DownScale2x2();
    }
}
//-----------------------------------------------------------------------------
// Technique: HDRScene_Bloom
// Desc: Performs a single horizontal or vertical pass of the blooming filter
//-----------------------------------------------------------------------------
technique HDRScene_Bloom
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {        
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 Bloom();
    }

}

//-----------------------------------------------------------------------------
// Technique: HDRScene_FinalScenePass
// Desc: Minimally transform and texture the incoming geometry
//-----------------------------------------------------------------------------
technique HDRScene_FinalScenePass
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 FinalScenePass();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_DebugLum
// Desc: Write luminance values to RGB
//-----------------------------------------------------------------------------
technique HDRScene_DebugLum
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 DebugLum();
    }
}

//-----------------------------------------------------------------------------
// Technique: HDRScene_DebugLum
// Desc: Write luminance values to RGB
//-----------------------------------------------------------------------------
technique HDRScene_DebugLumLog
<
    bool UsesNiRenderState = true;
    bool UsesNiLightState = false;
>
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 DebugLumLog();
    }
}

//-----------------------------------------------------------------------------
