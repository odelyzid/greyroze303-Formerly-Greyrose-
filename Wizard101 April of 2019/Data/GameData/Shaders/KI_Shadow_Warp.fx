// EMERGENT GAME TECHNOLOGIES PROPRIETARY INFORMATION
//
// This software is supplied under the terms of a license agreement or
// nondisclosure agreement with Emergent Game Technologies and may not 
// be copied or disclosed except in accordance with the terms of that 
// agreement.
//
//			Copyright (c) 1996-2008 Emergent Game Technologies.
//			All Rights Reserved.
//
// Emergent Game Technologies, Chapel Hill, North Carolina 27517
// http://www.emergent.net



// Constants
float4x4 WorldViewProj
<
	string VarType = "Predefined";
	string DefinedMapping = "WORLDVIEWPROJECTION";
>;

float4x4 World
<
	string VarType = "Predefined";
	string DefinedMapping = "WORLD";
>;

float4x4 ViewProj
<
    string VarType = "Predefined";
    string DefinedMapping = "VIEWPROJECTION";
>;

float4x4 worldInverseTranspose
<
    string VarType = "Predefined";
    string DefinedMapping = "WORLDINVERSETRANSPOSE";
>;

float4x4 viewInverse
<
    string VarType = "Predefined";
    string DefinedMapping = "VIEWINVERSE";
>;

static const int MAX_BONES = 32;
float4x3 Bone[MAX_BONES]
<
    string VarType = "Predefined";
    string DefinedMapping = "SKINBONEMATRIX3";
>;

float curTime : Time;
float sinTime : sin_time;


float Base_Alpha_Bias : ATTRIBUTE 
<
    float min = 0.0;
    float max = 100.0;
> = 0.5;

float Base_Alpha_Clamp : ATTRIBUTE 
<
    float min = 0.0;
    float max = 100.0;
> = 0.5;


float Fresnel_Power : ATTRIBUTE 
<
    float min = -100.0;
    float max = 100.0;
> = 1.0;

float Inner_Opacity : ATTRIBUTE 
<
    float min = 0.0;
    float max = 1.0;
> = 1.0;

float Outer_Opacity : ATTRIBUTE 
<
    float min = 0.0;
    float max = 1.0;
> = 0.2;

// textures
texture BaseMap
<
	string NTM = "Base";
>;


// Samplers
sampler BaseSampler = sampler_state
{
	Texture = (BaseMap);
    AddressU  = WRAP;        
    AddressV  = WRAP;
	MINFILTER = LINEAR;
	MIPFILTER = LINEAR;
	MAGFILTER = LINEAR;
};

// Structs
struct ColorPassOutput
{
	float4 Pos			: POSITION;
	float4 Col			: TEXCOORD3;
	float2 Tex			: TEXCOORD0;
	float3 FresnelCol	: TEXCOORD4;
};



// Vertex Shader
ColorPassOutput ColorPassVS(
	in float4 inPos : POSITION,
	in float3 inNrm : NORMAL,
	in float4 inCol : COLOR,
	in float2 inTex : TEXCOORD0,
	float3 BlendWeights : BLENDWEIGHT,
	int4 BlendIndices : BLENDINDICES)
{
	ColorPassOutput Out;

    // Calculate normalized fourth bone weight
    float weight4 = 1.0f 
        - BlendWeights[0] 
        - BlendWeights[1] 
        - BlendWeights[2];
        
    float4 weights = float4(
        BlendWeights[0], 
        BlendWeights[1], 
        BlendWeights[2], 
        weight4);

    // Calculate bone transform
    float4x3 BoneTransform;
    BoneTransform = weights[0] * Bone[BlendIndices[0]];
    BoneTransform += weights[1] * Bone[BlendIndices[1]];
    BoneTransform += weights[2] * Bone[BlendIndices[2]];
    BoneTransform += weights[3] * Bone[BlendIndices[3]];
    
    float3 wsVrt = mul(inPos, BoneTransform);
	float3 wsNrm = mul(float4(inNrm, 0.0), BoneTransform);

    Out.Pos = mul(float4(wsVrt, 1.0), ViewProj);
	//Out.Nrm = inNrm;
	Out.Tex = inTex;
	Out.Col = inCol;
	
	// Get the world-space vert position and normal, as well as the eye position and vector for lighting and/or Fresnel
	//float3 wsVrt = mul(inPos, World); // World vs. worldInverseTranspose?
	//float3 wsNrm = mul(inNrm, World); // World vs. worldInverseTranspose?
	float3 wsEye = viewInverse[3].xyz;
    float3 EyeVect = normalize(wsEye - wsVrt); //eye vector
	
	// Fresnel Color: dot(N,V)
	Out.FresnelCol = dot( normalize( wsNrm ), EyeVect );
	
	return Out;
}

float4 mod289(float4 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 perm(float4 x)
{
    return mod289(((x * 34.0) + 1.0) * x);
}

float noise3d(float3 p)
{
    float3 a = floor(p);
    float3 d = p - a;
    d = d * d * (3.0 - 2.0 * d);

    float4 b = a.xxyy + float4(0.0, 1.0, 0.0, 1.0);
    float4 k1 = perm(b.xyxy);
    float4 k2 = perm(k1.xyxy + b.zzww);

    float4 c = k2 + a.zzzz;
    float4 k3 = perm(c);
    float4 k4 = perm(c + 1.0);

    float4 o1 = frac(k3 * (1.0 / 41.0));
    float4 o2 = frac(k4 * (1.0 / 41.0));

    float4 o3 = o2 * d.z + o1 * (1.0 - d.z);
    float2 o4 = o3.yw * d.x + o3.xz * (1.0 - d.x);

    return o4.y * d.y + o4.x * (1.0 - d.y);
}

// Pixel Shader
float4 ColorPassPS(
	in float3 inTex : TEXCOORD0, 
	in float4 inCol : TEXCOORD3,
	in float3 inFresnelCol : TEXCOORD4) : COLOR0
{
	const float3 desat = {0.3, 0.59, 0.11};
	
	float2 texCoord = inTex.xy + sinTime * 0.1;
	
	float v1 = noise3d( float3(texCoord, 0.0) * 20.0);

	float4 BaseTex = tex2D(BaseSampler, inTex + float2(v1, v1) * 0.05);
	
	//desaturate the color from the base texture to greyscale
	BaseTex.rgb = dot(desat, BaseTex.rgb) * 0.5;
	
	float Fresnel = (pow (inFresnelCol, Fresnel_Power));
	//float3 outCol = (BaseTex.rgb * inCol.rgb);
	float outA = saturate (smoothstep ((Base_Alpha_Clamp * Base_Alpha_Bias), (Base_Alpha_Bias + (Base_Alpha_Bias * (1.01 - Base_Alpha_Clamp))), (inCol.a * BaseTex.a * (lerp (Outer_Opacity, Inner_Opacity, Fresnel)))));
	return float4( BaseTex.rgb, outA * (1.0 - (v1 * 0.5)) );
}



// Techniques
// As of December 2006 DX SDK, compilation of HLSL code to ps_1_x is no longer supported.
technique KI_Shadow_Warp
<
	string description = "Unlit FX shader which "
		"clamps the alpha of the base texture, "
		"Warps the base texture using the normal map, and "
		"applies inner/outer Fresnel opacicy.";
    
	int BonesPerPartition = MAX_BONES;
	bool UsesNiRenderState = true;
	bool UsesNiLightState = false;
	int implementation = 0;
>
{
	pass ColorPass
	{
		VertexShader = compile vs_1_1 ColorPassVS();
		PixelShader = compile ps_2_0 ColorPassPS();

		CullMode = CW;
		AlphaBlendEnable = true;
		ZEnable = true;
	}
}
