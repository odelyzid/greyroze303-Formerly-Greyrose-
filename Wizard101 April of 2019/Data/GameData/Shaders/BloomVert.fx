//**********************************************************************************************************************
/**
 *  "Blooms" (blurs) the image vertically
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

int BloomVertCount : GLOBAL = 13;
float2 BloomVertPixel[13] : GLOBAL =
{
   { 0, -6 },
   { 0, -5 },
   { 0, -4 },
   { 0, -3 },
   { 0, -2 },
   { 0, -1 },
   { 0,  0 },
   { 0,  1 },
   { 0,  2 },
   { 0,  3 },
   { 0,  4 },
   { 0,  5 },
   { 0,  6 },
};
float2 BloomVertTexel[13] : GLOBAL;

static const float BlurWeights[13] = 
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
   0.002216,
};

// Higher values emphasize the "hot spots" in the image more; lower values, used in conjunction with lower Luminance,
// will more widely diffuse the image.
float BloomVertScale : GLOBAL = 1.5f;


//**********************************************************************************************************************
/**
 * Blurs the image vertically
 *
 * @param Tex     input texture coord
 *
 * @return        pixel color
 **/
//**********************************************************************************************************************
float4 BloomVertical( float2 Tex : TEXCOORD0 ) : COLOR0
{
   float4 Color = 0;

   for (int i = 0; i < BloomVertCount; i++)
   {    
      //Color += tex2D( g_samSrcColor, Tex + TexelKernel[i].xy ) * BlurWeights[i];
      Color += tex2D( g_samSrcColor, Tex + BloomVertTexel[i].xy ) * BlurWeights[i];
   }

   return Color * BloomVertScale;

}  // end of BloomVertical


//**********************************************************************************************************************
// Techniques
//**********************************************************************************************************************
technique BloomVertical
<
    string Parameter0 = "BloomVertScale";
    float4 Parameter0Def = float4( 1.5f, 0, 0, 0 );
    int Parameter0Size = 1;
    string Parameter0Desc = " (float)";
>
{
    pass p0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader = compile ps_2_0 BloomVertical();
        ZEnable = false;
    }

}  // end of BloomVertical

