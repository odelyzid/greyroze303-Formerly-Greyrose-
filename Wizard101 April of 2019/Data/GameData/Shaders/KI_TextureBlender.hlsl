// EMERGENT GAME TECHNOLOGIES PROPRIETARY INFORMATION
//
// This software is supplied under the terms of a license agreement or
// nondisclosure agreement with Emergent Game Technologies and may not 
// be copied or disclosed except in accordance with the terms of that 
// agreement.
//
//	Copyright (c) 1996-2008 Emergent Game Technologies.
//	All Rights Reserved.
//
// Emergent Game Technologies, Chapel Hill, North Carolina 27517
// http://www.emergent.net

sampler BaseSampler : register(s0);
sampler DecalSampler : register(s1);

float alphaClamp;
float alphaMidpoint;
static const float2 stepVals = float2(alphaClamp * alphaMidpoint, alphaMidpoint + alphaMidpoint * (1.01 - alphaClamp));

float shadowAmount;

float4x4 WorldViewProj;
float4x4 World;

float4 AmbientLightColor;
float4 MaterialDiffuse;
float4 MaterialAmbient;
float4 MaterialEmissive;

float4x4 UVSet0_TexTransform;
float4x4 UVSet1_TexTransform;

float4 PLight0WPos;
float4 PLight0Atten;
float4 PLight0Diffuse;

float4 PLight1WPos;
float4 PLight1Atten;
float4 PLight1Diffuse;

float4 PLight2WPos;
float4 PLight2Atten;
float4 PLight2Diffuse;

float4 PLight3WPos;
float4 PLight3Atten;
float4 PLight3Diffuse;

float4 DLight0WDir;
float4 DLight0Diffuse;

float4 DLight1WDir;
float4 DLight1Diffuse;

float4 DLight2WDir;
float4 DLight2Diffuse;

float4 DLight3WDir;
float4 DLight3Diffuse;

struct VS_OUTPUT
{
	float4	Pos			: POSITION;
	float2	TexCoord0	: TEXCOORD0;
	float2	TexCoord1	: TEXCOORD1;
	float4	Col 		: TEXCOORD2;
};

VS_OUTPUT VS(
	float3	Pos			: POSITION,
	float3	Normal		: NORMAL,
	float4	Col			: COLOR0,
	float2	TexCoord0	: TEXCOORD0,
	float2	TexCoord1	: TEXCOORD1)
{
	VS_OUTPUT Out = (VS_OUTPUT) 0;
	Out.Pos = mul(float4(Pos, 1), WorldViewProj);
	
	// Texture coords
	float2 tc0 = mul(float4(TexCoord0.x, TexCoord0.y, 0.0, 1.0), UVSet0_TexTransform);
	Out.TexCoord0 = tc0.xy;

	float2 tc1 = mul(float4(TexCoord1.x, TexCoord1.y, 0.0, 1.0), UVSet1_TexTransform);
	Out.TexCoord1 = tc1.xy;

	// Color/lighting
	float3 posW = (float3)mul(float4(Pos, 1), World);
	float3 normalW = mul(Normal, (float3x3)World);
	float3 outLight = float3(0,0,0);

	float3 deltaL;
	float deltaLenL;
	float3 normalL;
	float attenL;

	// Point0
	deltaL = (float3)PLight0WPos - posW;
	deltaLenL = length(deltaL);
	normalL = deltaL / deltaLenL;
	attenL = 1.0 / (PLight0Atten.x + (PLight0Atten.y * deltaLenL) + (PLight0Atten.z * deltaLenL * deltaLenL));
	outLight += (float3)PLight0Diffuse * max(0.0, dot(normalW, normalL)) * attenL;

	// Point1
	deltaL = (float3)PLight1WPos - posW;
	deltaLenL = length(deltaL);
	normalL = deltaL / deltaLenL;
	attenL = 1.0 / (PLight1Atten.x + (PLight1Atten.y * deltaLenL) + (PLight1Atten.z * deltaLenL * deltaLenL));
	outLight += (float3)PLight1Diffuse * max(0.0, dot(normalW, normalL)) * attenL;

	// Point2
	deltaL = (float3)PLight2WPos - posW;
	deltaLenL = length(deltaL);
	normalL = deltaL / deltaLenL;
	attenL = 1.0 / (PLight2Atten.x + (PLight2Atten.y * deltaLenL) + (PLight2Atten.z * deltaLenL * deltaLenL));
	outLight += (float3)PLight2Diffuse * max(0.0, dot(normalW, normalL)) * attenL;

	// Point3
	deltaL = (float3)PLight3WPos - posW;
	deltaLenL = length(deltaL);
	normalL = deltaL / deltaLenL;
	attenL = 1.0 / (PLight3Atten.x + (PLight3Atten.y * deltaLenL) + (PLight3Atten.z * deltaLenL * deltaLenL));
	outLight += (float3)PLight3Diffuse * max(0.0, dot(normalW, normalL)) * attenL;

	float dpL;

	// Dir0
	dpL = max(0.0, dot(normalW, -(float3)DLight0WDir));
	outLight += (float3)DLight0Diffuse * dpL;

	// Dir1
	dpL = max(0.0, dot(normalW, -(float3)DLight1WDir));
	outLight += (float3)DLight1Diffuse * dpL;

	// Dir2
	dpL = max(0.0, dot(normalW, -(float3)DLight2WDir));
	outLight += (float3)DLight2Diffuse * dpL;

	// Dir3
	dpL = max(0.0, dot(normalW, -(float3)DLight3WDir));
	outLight += (float3)DLight3Diffuse * dpL;
	
	// Final Color
	Out.Col.xyz = Col.xyz * (outLight.xyz * MaterialDiffuse.xyz + AmbientLightColor.xyz) + MaterialEmissive.xyz; // vert colors ON (no material ambient)
//	Out.Col.xyz = outLight.xyz * MaterialDiffuse.xyz + float3(AmbientLightColor.xyz * MaterialAmbient.xyz) + MaterialEmissive.xyz; // Vert colors OFF
	Out.Col.w = Col.w;

	return Out;
}

float4 PS(VS_OUTPUT In) : COLOR
{
	float4 baseMap = tex2D(BaseSampler, In.TexCoord0);
	float4 decalMap = tex2D(DecalSampler, In.TexCoord1);

	float clampAlpha = (In.Col.w * 2.0 * decalMap.w) + (clamp ((In.Col.w - 0.5), 0, 1) * 0.5);
	float lerp1to2 = saturate(smoothstep (stepVals.x, stepVals.y, clampAlpha));
	float shadow = lerp(1.0, abs(lerp1to2 * 2.0 - 1.0), shadowAmount);
	float3 OutCol = (float3)lerp(baseMap.xyz, decalMap.xyz, lerp1to2) * shadow;

	OutCol *= In.Col.xyz;
	float OutA = max(lerp1to2, baseMap.w);

	return float4(OutCol, OutA);
}
