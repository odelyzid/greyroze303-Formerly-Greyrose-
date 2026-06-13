//**********************************************************************************************************************
/**
 *  Bright pass filter; modeled after the Microsoft DirectX SDK example
 *
 *  @author: Bill Randolph
 *
 *  created: 3/28/2006
 *
 *  @file
 **/
//**********************************************************************************************************************

//---------------------------------------------------------------------------
// Vertex Shaders
//---------------------------------------------------------------------------
float4x4 g_matWorldViewProj : WORLDVIEWPROJECTION;

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
 
//**********************************************************************************************************************
// Base texture; the texture applied to the mesh as the 'Base' will be assigned to this sampler
//**********************************************************************************************************************
texture g_texSrcColor
<
   string NTM = "Base";
>;
sampler2D g_samSrcColor = sampler_state
{
   Texture = <g_texSrcColor>;
   AddressU = Clamp;
   AddressV = Clamp;
   MinFilter = Point;
   MagFilter = Linear;
   MipFilter = Linear;
};

// (Original values: 0.08, 0.18, 0.8)
// Good range for this value: 0.065 - 0.08
float BloomBrightPassLuminance : GLOBAL = 0.07f;   // good for overall diffuse image look
static const float fMiddleGray = 0.18f;            // decrease = darken/decrease bloom
static const float fWhiteCutoff = 0.8f;            // decrease = lighten/increase  bloom

static const float brightPassThreshold = 5.0f;

//**********************************************************************************************************************
/**
 * Pixel shader to perform a high-pass filter on the source
 * See the DirectX 9 C++ help documentation for the HDRLighting example for an explanation of these equations
 *
 * @param Tex     input texture coord
 *
 * @return        color value
 **/
//**********************************************************************************************************************
float4 BrightPassFilter( in float2 Tex : TEXCOORD0 ) : COLOR0
{
   float3 ColorOut = tex2D( g_samSrcColor, Tex );

   // Determine what the pixel's value will be after tone mapping occurs
   ColorOut *= fMiddleGray / ( BloomBrightPassLuminance + 0.001f );
   ColorOut *= ( 1.0f + ( ColorOut / ( fWhiteCutoff * fWhiteCutoff ) ) );
   ColorOut -= brightPassThreshold;

   // Clamp floor to 0
   ColorOut = max( ColorOut, 0.0f );

   // Map the resulting value into the 0 to 1 range. Higher values for
   // BRIGHT_PASS_OFFSET will isolate lights from illuminated scene 
   // objects.
   ColorOut /= ( brightPassThreshold + ColorOut );

   return float4( ColorOut, 1.0f );

}  // end of BrightPassFilter


//**********************************************************************************************************************
// Techniques
//**********************************************************************************************************************
technique BrightPass
<
   string Parameter0 = "BloomBrightPassLuminance";
   float4 Parameter0Def = float4( 0.08f, 0, 0, 0 );
   int Parameter0Size = 1;
   string Parameter0Desc = " (float)";
>
{
   pass p0
   {
      VertexShader = compile vs_2_0 VSMain();
      PixelShader = compile ps_2_0 BrightPassFilter();
      ZEnable = false;
   }

}  // end of BrightPass

