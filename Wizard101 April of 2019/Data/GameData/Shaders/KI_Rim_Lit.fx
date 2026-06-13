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

float4x4 worldInverseTranspose
<
    string VarType = "Predefined";
    string DefinedMapping = "WORLDINVERSETRANSPOSE";
>;

float4x4 View
<
    string VarType = "Predefined";
    string DefinedMapping = "VIEW";
>;

float4x4 viewInverse
<
    string VarType = "Predefined";
    string DefinedMapping = "VIEWINVERSE";
>;

float4x4 InvView
<
    string VarType = "Predefined";
    string DefinedMapping = "INVVIEW";
>;

float4x4 ViewProj
<
    string VarType = "Predefined";
    string DefinedMapping = "VIEWPROJ";
>;


// Ambient light (does not work)
//float4 globalAmbient : SceneAmbient;

// Default light?
float3 MSLightPos : Position
<
    string Object = "PointLight";
    string Space = "Model";
>;


// Light 0
float3 LightPos0 : POSITION
<
    string Object = "PointLight";
    int ObjectIndex = 0;
> = {0.0f, 0.0f, 0.0f};

float4 LightDiff0 : DIFFUSE
<
    string Object = "PointLight";
    int ObjectIndex = 0;
> = {0.0f, 0.0f, 0.0f, 0.0f};

float3 LightAtten0 : ATTENUATION
<
    string Object = "PointLight";
    int ObjectIndex = 0;
> = {1.0f, 0.0f, 0.0f};


// Light 1
float3 LightPos1 : POSITION
<
    string Object = "PointLight";
    int ObjectIndex = 1;
> = {0.0f, 0.0f, 0.0f};

float4 LightDiff1 : DIFFUSE
<
    string Object = "PointLight";
    int ObjectIndex = 1;
> = {0.0f, 0.0f, 0.0f, 0.0f};

float3 LightAtten1 : ATTENUATION
<
    string Object = "PointLight";
    int ObjectIndex = 1;
> = {1.0f, 0.0f, 0.0f};


// Light 2
float3 LightPos2 : POSITION
<
    string Object = "PointLight";
    int ObjectIndex = 2;
> = {0.0f, 0.0f, 0.0f};

float4 LightDiff2 : DIFFUSE
<
    string Object = "PointLight";
    int ObjectIndex = 2;
> = {0.0f, 0.0f, 0.0f, 0.0f};

float3 LightAtten2 : ATTENUATION
<
    string Object = "PointLight";
    int ObjectIndex = 2;
> = {1.0f, 0.0f, 0.0f};


// Light 3
float3 LightPos3 : POSITION
<
    string Object = "PointLight";
    int ObjectIndex = 3;
> = {0.0f, 0.0f, 0.0f};

float4 LightDiff3 : DIFFUSE
<
    string Object = "PointLight";
    int ObjectIndex = 3;
> = {0.0f, 0.0f, 0.0f, 0.0f};

float3 LightAtten3 : ATTENUATION
<
    string Object = "PointLight";
    int ObjectIndex = 3;
> = {1.0f, 0.0f, 0.0f};


// Light 4
float3 LightPos4 : POSITION
<
    string Object = "PointLight";
    int ObjectIndex = 4;
> = {0.0f, 0.0f, 0.0f};

float4 LightDiff4 : DIFFUSE
<
    string Object = "PointLight";
    int ObjectIndex = 4;
> = {0.0f, 0.0f, 0.0f, 0.0f};

float3 LightAtten4 : ATTENUATION
<
    string Object = "PointLight";
    int ObjectIndex = 4;
> = {1.0f, 0.0f, 0.0f};


// Material Colors
float3 MaterialAmbient
<
    string VarType = "Predefined";
    string DefinedMapping = "MaterialAmbient";
>;

float3 MaterialDiffuse // Unused
<
    string VarType = "Predefined";
    string DefinedMapping = "MaterialDiffuse";
>;

float3 MaterialEmittance
<
    string VarType = "Predefined";
    string DefinedMapping = "MaterialEmissive";
>;


// Attributes
float Fresnel_Power : ATTRIBUTE 
<
    float min = -100.0;
    float max = 100.0;
> = 0.5;

float Fresnel_Intensity : ATTRIBUTE 
<
    float min = -100.0;
    float max = 100.0;
> = 1.0;



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
	float3 Nrm			: TEXCOORD1;
	float4 Col			: TEXCOORD3;
	float2 Tex			: TEXCOORD0;
	float3 FresnelCol	: TEXCOORD4;
};



// Vertex Shader DEFAULT LIGHT
ColorPassOutput ColorPassVS(
	in float4 inPos : POSITION,
	in float3 inNrm : NORMAL,
	in float4 inCol : COLOR,
	in float2 inTex : TEXCOORD0)
{
	ColorPassOutput Out;
	
	Out.Pos = mul(inPos, WorldViewProj);
	Out.Nrm = inNrm;
	Out.Tex = inTex;
	
	// Get the world-space vert position and normal, as well as the eye position and vector for lighting and/or Fresnel
	float3 wsVrt = mul(inPos, World); // World vs. worldInverseTranspose?
	float3 wsNrm = mul(inNrm, World); // World vs. worldInverseTranspose?
	float3 wsEye = viewInverse[3].xyz;
    float3 EyeVect = normalize(wsEye - wsVrt); //eye vector	
	
    // Calculate dynamic lighting
//	float4 LightColor = (1.0, 1.0, 1.0, 1.0);
	
	// Ambient
//	LightColor.xyz = MaterialAmbient.xyz;
	
	// Light 0
//	float3 LightVec0 = LightPos0 - wsVrt;
//	float LightDist0 = length(LightVec0);
//	float3 LightDir0 = normalize(LightVec0);
//	float Atten0 = 1.0 / (LightAtten0.x + LightAtten0.y * LightDist0 + LightAtten0.z * LightDist0 * LightDist0);
//	LightColor.xyz += max(0, dot(LightDir0, wsNrm)) * Atten0 * LightDiff0;
		
//	Out.Col = inCol * LightColor;

	float4 testCol = (1.0, 0.0, 0.0, 1.0);
	Out.Col = testCol;
	
	// Fresnel Color: dot(N,V)
	Out.FresnelCol = dot( normalize( wsNrm ), EyeVect );
	
	return Out;
}


// Vertex Shader LIT
ColorPassOutput ColorPassVSLit(
	in float4 inPos : POSITION,
	in float3 inNrm : NORMAL,
	in float4 inCol : COLOR,
	in float2 inTex : TEXCOORD0)
{
	ColorPassOutput Out;
	
	Out.Pos = mul(inPos, WorldViewProj);
	Out.Nrm = inNrm;
	Out.Tex = inTex;
	
	// Get the world-space vert position and normal, as well as the eye position and vector for lighting and/or Fresnel
	float3 wsVrt = mul(inPos, World); // World vs. worldInverseTranspose?
	float3 wsNrm = mul(inNrm, World); // World vs. worldInverseTranspose?
	float3 wsEye = viewInverse[3].xyz;
    float3 EyeVect = normalize(wsEye - wsVrt); //eye vector	
	
    // Calculate dynamic lighting
	float4 LightColor = (1.0, 1.0, 1.0, 1.0);
	
	// Ambient
	LightColor.xyz = MaterialAmbient.xyz;
	
	// Light 0
	float3 LightVec0 = LightPos0 - wsVrt;
	float LightDist0 = length(LightVec0);
	float3 LightDir0 = normalize(LightVec0);
	float Atten0 = 1.0 / (LightAtten0.x + LightAtten0.y * LightDist0 + LightAtten0.z * LightDist0 * LightDist0);
	LightColor.xyz += max(0, dot(LightDir0, wsNrm)) * Atten0 * LightDiff0;
	
	// Light 1
	float3 LightVec1 = LightPos1 - wsVrt;
	float LightDist1 = length(LightVec1);
	float3 LightDir1 = normalize(LightVec1);
	float Atten1 = 1.0 / (LightAtten1.x + LightAtten1.y * LightDist1 + LightAtten1.z * LightDist1 * LightDist1);
	LightColor.xyz += max(0, dot(LightDir1, wsNrm)) * Atten1 * LightDiff1;
	
	// Light 2
	float3 LightVec2 = LightPos2 - wsVrt;
	float LightDist2 = length(LightVec2);
	float3 LightDir2 = normalize(LightVec2);
	float Atten2 = 1.0 / (LightAtten2.x + LightAtten2.y * LightDist2 + LightAtten2.z * LightDist2 * LightDist2);
	LightColor.xyz += max(0, dot(LightDir2, wsNrm)) * Atten2 * LightDiff2;
	
	// Light 3
	float3 LightVec3 = LightPos3 - wsVrt;
	float LightDist3 = length(LightVec3);
	float3 LightDir3 = normalize(LightVec3);
	float Atten3 = 1.0 / (LightAtten3.x + LightAtten3.y * LightDist3 + LightAtten3.z * LightDist3 * LightDist3);
	LightColor.xyz += max(0, dot(LightDir3, wsNrm)) * Atten3 * LightDiff3;
	
	// Light 4
	float3 LightVec4 = LightPos4 - wsVrt;
	float LightDist4 = length(LightVec4);
	float3 LightDir4 = normalize(LightVec4);
	float Atten4 = 1.0 / (LightAtten4.x + LightAtten4.y * LightDist4 + LightAtten4.z * LightDist4 * LightDist4);
	LightColor.xyz += max(0, dot(LightDir4, wsNrm)) * Atten4 * LightDiff4;
	
	Out.Col = inCol * LightColor;
	
	// Fresnel Color: dot(N,V)
	Out.FresnelCol = dot( normalize( wsNrm ), EyeVect );
	
	return Out;
}


// Pixel Shader
float4 ColorPassPS(
	in float3 inPos : TEXCOORD0, 
	in float4 inCol : TEXCOORD3,
	in float3 inFresnelCol : TEXCOORD4) : COLOR0
{
	float4 BaseTex = tex2D(BaseSampler, inPos);
	float4 outCol = (BaseTex * inCol);
	float Fresnel = 1.0 - (pow (inFresnelCol, Fresnel_Power));
	outCol.xyz += (Fresnel * MaterialEmittance * 20.0 * Fresnel_Intensity); // Rim
	return outCol;
}



// Techniques
// As of December 2006 DX SDK, compilation of HLSL code to ps_1_x is no longer supported.
technique KI_Rim_Lit
<
	string description = "Lit shader which "
		"Applies a rim using the emittance color.";
	bool UsesNiRenderState = true;
	bool UsesNiLightState = true;
	int implementation = 0;
>
{
	pass ColorPass
	{
		#if defined(LightDiff0)
			VertexShader = compile vs_1_1 ColorPassVSLit();
		#else
			VertexShader = compile vs_1_1 ColorPassVS();
		#endif
		
		PixelShader = compile ps_2_0 ColorPassPS();

		CullMode = NONE;  // NONE - CW - CCW
	}
}