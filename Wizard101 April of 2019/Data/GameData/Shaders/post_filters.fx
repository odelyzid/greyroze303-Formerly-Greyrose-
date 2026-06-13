//**********************************************************************************************************************
/**
 *  post-process filter effects
 *
 *  @author: mmilliger
 *
 *  created: 6/14/2006
 *
 **/
//**********************************************************************************************************************
#include <include\\noise_2d.fxh>

#define DOTS_PER_BIT 8.0
#define IMG_DIVS 8.0
#define DOWNSIZE (1.0/IMG_DIVS)

float Timer : TIME;

//scratch
float Speed : GLOBAL = 0.03f; //Speed (Slower=Longer Scratches)
float Speed2 : GLOBAL = 0.01f; //scratch speed
float ScratchIntensity : GLOBAL = 0.65f; //Scratch Cutoff
float IS : GLOBAL = 0.01f; //scratch width

//sepia
float Desat : GLOBAL = 0.5f;
float Toned : GLOBAL = 1.0f;
float3 LightColor : GLOBAL = {1,0.9,0.5};
float3 DarkColor : GLOBAL = {0.2,0.05,0};

//tv
float ScanLines : GLOBAL = 486;
float SpeedVHold : GLOBAL = 0.05f; //V.Hold

//**********************************************************************************************************************
// vertex constants
//
float4x4 g_matWorldViewProj : WORLDVIEWPROJECTION;

//**********************************************************************************************************************
// Structure returned by the vert shader
//
struct VS_OUTPUT
{
	float4 Pos      : POSITION;
	float2 Tex0     : TEXCOORD0;
};

struct VS_QUADOUT
{
	float4 Position    : POSITION;
	float2 TexCoordA   : TEXCOORD0;
	float4 ScanFlash   : TEXCOORD1;
    float4 TexCoordB   : TEXCOORD2;
    float4 TexCoordC   : TEXCOORD3;
    float4 TexCoordD   : TEXCOORD4;
    float4 TexCoordE   : TEXCOORD5;
    float4 TexCoordF   : TEXCOORD6;
};

//**********************************************************************************************************************
/**
 * Main vert shader
 *
 * @param POSITION      input coord for the vert
 * @param TEXCOORD0     input texture coord for the veret
 *
 * @return     transformed vert & texture coord
**/
VS_OUTPUT VS_Main(float4 inPos: POSITION, float2 inTex0: TEXCOORD0)
{
   VS_OUTPUT Out;
   Out.Pos = mul(inPos, g_matWorldViewProj);
   Out.Tex0 = inTex0;
   return Out;

}  // end of VS_Simple_Main


VS_QUADOUT VS_Quad(float3 Position : POSITION, float3 TexCoord : TEXCOORD0)
{
    VS_QUADOUT OUT;
    OUT.Position = float4(Position, 1);
	float tx = TexCoord.x + (1+sin(Timer/2))*0.002;
	float ty = TexCoord.y + (1+sin(frac(Timer*2)))*0.002;
    float4 baseTC = float4(tx,ty,TexCoord.z, 1); 
    OUT.TexCoordA = baseTC;
    OUT.TexCoordB = (baseTC+Timer) * 11;
    OUT.TexCoordC = (baseTC-Timer) * 11;
    OUT.TexCoordD = (-baseTC+Timer) * 11;
	OUT.TexCoordE = (baseTC+Timer) * 2;
	OUT.TexCoordF = (baseTC+Timer) * 5;
	float scan = ty*ScanLines+Timer*SpeedVHold;
	// Flash
	float flash = 1.0;
	if(frac(Timer/10)<0.1) flash = 3.0*(0.5+0.5*sin(Timer*4.0));
    OUT.ScanFlash = float4(scan,flash,0,1); 
    return OUT;
}


//**********************************************************************************************************************
// pixel functions
//

// tone function
float4 make_tones(float3 Pos : POSITION, float3 Size : PSIZE) : COLOR 
{
	float2 delta = Pos.xy - float2(0.5,0.5);
	float d = dot(delta,delta);
	float rSquared = (Pos.z*Pos.z)/2.0;
	float n2 = (d<rSquared) ? 1.0 : 0.0;
	return float4(n2,n2,n2,1.0);
}

// noise function
float4 noisy_function(float3 Pos : POSITION) : COLOR
{
	return (noise(Pos * 50.5) * .5) + .5f;
}

float4 sine_function(float2 Pos : POSITION) : COLOR
{
	return 0.5*sin(Pos.x*2*3.141592653589793238) + 0.5f;
}

//**********************************************************************************************************************
// pixel textures
//

texture g_texSrcColor
<
   string NTM = "Base";   // this texture is the base map channel
>;

texture DepthMap : RENDERDEPTHSTENCILTARGET
<
    float2 ViewportRatio = { 1.0, 1.0 };
    string format = "D24S8";
    string UIWidget = "none";
>;

texture NoisyTex
< 
    string ResourceType = "VOLUME"; 
    string function = "noisy_function"; 
    string UIWidget = "None";
    float3 Dimensions = { 64.0f, 64.0f, 64.0f };
>;

// Sine Func
texture SineTex
<
    string ResourceType = "2D"; 
    string function = "sine_function"; 
    string UIWidget = "None";
    float2 Dimensions = { 32.0f, 1};
>;

texture ToneTex 
<
    string function = "make_tones";
	string ResourceType = "VOLUME";
	float3 Dimensions = { 16.0f, 16.0f, 32.0f };
	string UIWidget = "None";
>;

//**********************************************************************************************************************
// sampler
//

sampler2D g_samSrcColor = sampler_state
{
   Texture = (g_texSrcColor);
   AddressU = Clamp;
   AddressV = Clamp;
   MinFilter = Point;
   MagFilter = Linear;
   MipFilter = Linear;
};

sampler3D NoisySampler = sampler_state 
{
    texture = (NoisyTex);
    MipFilter = LINEAR;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    AddressU = WRAP;
    AddressV = WRAP;
    AddressW = WRAP;
};

sampler1D SineSampler = sampler_state 
{
    texture = (SineTex);
    MipFilter = NONE;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    AddressU = WRAP;
    AddressV = WRAP;
};

sampler ToneSampler = sampler_state 
{
    texture = (ToneTex);
    AddressU  = WRAP;        
    AddressV  = WRAP;
    AddressW  = CLAMP;
    MIPFILTER = NONE;
    MINFILTER = ANISOTROPIC;
    MAGFILTER = LINEAR;
};

//**********************************************************************************************************************
// pixel shaders
//

float4 PS_BlackAndWhite(float2 vTexCoord : TEXCOORD0) : COLOR0
{
   float4 col = tex2D(g_samSrcColor, vTexCoord);
   float avg = (col.r + col.g + col.b) / 3.0;
   return float4(avg,avg,avg,1);

}

//---------------
static float ScanLine = (Timer*Speed);
static float Side = (Timer*Speed2);
float4 PS_Scratch(float2 vTexCoord : TEXCOORD0) : COLOR0 {
	float4 img = tex2D(g_samSrcColor,vTexCoord);
	float2 s = float2(vTexCoord.x+Side,ScanLine);
	float scratch = tex2D(Noise2DSamp,s).x;
	scratch = 2.0f*(scratch - ScratchIntensity)/IS;
	scratch = 1.0-abs(1.0f-scratch);
	//scratch = scratch * 100.0f;
	scratch = max(0,scratch);
	//scratch = min(scratch,1.0f);
    return img + float4(scratch.xxx,0);
}

//---------------
float4 PS_Sepia(float2 vTexCoord : TEXCOORD0) : COLOR0
{   
    float3 scnColor = LightColor * tex2D(g_samSrcColor, vTexCoord);
    float3 grayXfer = float3(0.3,0.59,0.11);
    float gray = dot(grayXfer,scnColor);
    float3 muted = lerp(scnColor,gray.xxx,Desat);
    float3 sepia = lerp(DarkColor,LightColor,gray);
    float3 result = lerp(muted,sepia,Toned);
    return float4(result,1);
}

//---------------
float4 PS_Tone(float2 vTexCoord : TEXCOORD0) : COLOR0
{
	float4 scnC = tex2D(g_samSrcColor,vTexCoord);
	float lum = dot(float3(.2,.7,.1),scnC.xyz);
	float3 lx = float3((DOTS_PER_BIT*IMG_DIVS*vTexCoord.xy),lum);
	float4 dotC = tex3D(ToneSampler,lx);
    return float4(dotC.xyz,1.0);
}

//---------------
float4 PS_TV(VS_QUADOUT IN) : COLOR0
{   
	float4 img = tex2D(g_samSrcColor, IN.TexCoordA.xy);
	float scanlines = tex1D(SineSampler,IN.ScanFlash.x).xxx;
	img *= scanlines;
	img *= IN.ScanFlash.y;
	float4 noise = float4(tex3D(NoisySampler, IN.TexCoordB).x,
							tex3D(NoisySampler, IN.TexCoordC).x,
							tex3D(NoisySampler, IN.TexCoordD).x,1);
	float4 noise2 = tex3D(NoisySampler, IN.TexCoordE);
	float4 noise3 = tex3D(NoisySampler, IN.TexCoordF);
	img *= 3.0 * noise*noise2*noise3 + 0.8;
	return (img);
} 

//**********************************************************************************************************************
// Techniques
//
technique BlackAndWhite
{
   pass p0
   {
      VertexShader = compile vs_2_0 VS_Main();
      PixelShader = compile ps_2_0 PS_BlackAndWhite();
      ZEnable = false;
      AlphaBlendEnable = false;
      cullmode = none;
   }
}

technique Scratch
{
    pass p0 
    {		
		VertexShader = compile vs_2_0 VS_Main();
		PixelShader = compile ps_2_b PS_Scratch();
		ZEnable = false;
		ZWriteEnable = false;
		cullmode = none;
    }
}

technique Sepia
{
    pass p0
    {
		VertexShader = compile vs_2_0 VS_Main();
		PixelShader  = compile ps_2_0 PS_Sepia();
		ZEnable = false;
		AlphaBlendEnable = false;
		cullmode = none;
    }
}

technique Toned
{
	pass p0
	{
		VertexShader = compile vs_2_0 VS_Main();
		PixelShader = compile ps_2_0 PS_Tone();
		ZEnable = true;
		ZWriteEnable = true;
		cullmode = none;
	}
}

technique TV
{
   pass TV_lines 
   {
		VertexShader = compile vs_2_0 VS_Quad();
		PixelShader  = compile ps_2_0 PS_TV();
		cullmode = none;
		ZEnable = false;
    }
}