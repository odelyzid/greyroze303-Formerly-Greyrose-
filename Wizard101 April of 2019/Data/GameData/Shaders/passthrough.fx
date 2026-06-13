//**********************************************************************************************************************
/**
 *  Simple pass-through shader designed to work with Gamebryo.  No effects will be added to scene.
 *
 *  @author: Bill Randolph
 *
 *  created: 3/23/2006
 *
 *  @file
 **/
//**********************************************************************************************************************

//**********************************************************************************************************************
// Base texture; the texture applied to the mesh as the 'Base' will be assigned to this sampler
//**********************************************************************************************************************
texture g_texSrcColor
<
   string NTM = "Base";   // this texture is the base map channel
>;
sampler2D g_samSrcColor = sampler_state
{
   Texture = (g_texSrcColor);
   AddressU = Clamp;
   AddressV = Clamp;
   MinFilter = Point;
   MagFilter = Linear;
   MipFilter = Linear;
};

float4x4 g_matWorldViewProj : WORLDVIEWPROJECTION;

//**********************************************************************************************************************
// Structure returned by the vert shader
//**********************************************************************************************************************
struct VS_OUTPUT
{
   float4 Pos      : POSITION;
   float2 Tex0     : TEXCOORD0;
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
//**********************************************************************************************************************
VS_OUTPUT VS_Simple_Main(float4 inPos: POSITION, float2 inTex0: TEXCOORD0)
{
   VS_OUTPUT Out;
   Out.Pos = mul(inPos, g_matWorldViewProj);
   Out.Tex0 = inTex0;
   return Out;

}  // end of VS_Simple_Main


//**********************************************************************************************************************
/**
 * Pass-thru pixel shader
 *
 * @param TEXCOORD0
 *
 * @return
 **/
//**********************************************************************************************************************
float4 PS_Simple_PassThrough(float2 vTexCoord : TEXCOORD0) : COLOR0
{
   // Pass-through the color value:
   return tex2D(g_samSrcColor, vTexCoord);

}  // end of PS_Simple_PassThrough

//**********************************************************************************************************************
// Techniques
//**********************************************************************************************************************
technique PassThrough
{
   pass p0
   {
      VertexShader = compile vs_2_0 VS_Simple_Main();
      PixelShader = compile ps_2_0 PS_Simple_PassThrough();
      ZEnable = false;
      AlphaBlendEnable = false;
   }
}  // end of PassThrough
