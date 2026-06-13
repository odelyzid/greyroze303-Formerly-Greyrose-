//**********************************************************************************************************************
/**
 *  Scales the image down by 4, modeled after the Microsoft DirectX SDK example
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

// These globals are read by the Gamebryo app.  The BloomDownFilterTexelCoords array MUST be initialized to contain
// the contents of BloomDownFilterPixelCoords, divided by screen height & width.
int BloomDownFilter4Count : GLOBAL = 16;
float2 BloomDownFilter4PixelCoords[16] : GLOBAL =
{
   { 1.5,  -1.5 },
   { 1.5,  -0.5 },
   { 1.5,   0.5 },
   { 1.5,   1.5 },

   { 0.5,  -1.5 },
   { 0.5,  -0.5 },
   { 0.5,   0.5 },
   { 0.5,   1.5 },

   {-0.5,  -1.5 },
   {-0.5,  -0.5 },
   {-0.5,   0.5 },
   {-0.5,   1.5 },

   {-1.5,  -1.5 },
   {-1.5,  -0.5 },
   {-1.5,   0.5 },
   {-1.5,   1.5 },
};
float2 BloomDownFilter4TexelCoords[16] : GLOBAL;

//**********************************************************************************************************************
/**
 * Scales the texture down
 *
 * @param Tex     texture coord
 *
 * @return        pixel color
 **/
//**********************************************************************************************************************
float4 DownFilter( in float2 Tex : TEXCOORD0 ) : COLOR0
{
   float4 color = 0;

   for (int i = 0; i < 16; i++)
   {
      color += tex2D( g_samSrcColor, Tex + BloomDownFilter4TexelCoords[i].xy );
   }

   return color / 16;

}  // end of DownFilter


//**********************************************************************************************************************
// Techniques
//**********************************************************************************************************************
technique DownFilter4x
{
   pass p0
   <
      float fScaleX = 0.25f;
      float fScaleY = 0.25f;
   >
   {
      VertexShader = compile vs_2_0 VSMain();
      PixelShader = compile ps_2_0 DownFilter();
      ZEnable = false;
   }

}  // end of DownFilter4x

