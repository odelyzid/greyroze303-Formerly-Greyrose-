//**********************************************************************************************************************
/**
 *  Scales the image up by 4, modeled after the Microsoft DirectX SDK example
 *
 *  @author: Bill Randolph
 *
 *  created: 3/28/2006
 *
 *  @file
 **/
 
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


//**********************************************************************************************************************
/**
 * Scales the texture up (this is a placeholder routine that just passes thru; who knows, maybe no more is needed)
 *
 * @param Tex     texture coord
 *
 * @return        pixel color
 **/
//**********************************************************************************************************************
float4 UpFilter( float2 Tex : TEXCOORD0 ) : COLOR0
{
   return tex2D( g_samSrcColor, Tex );

}  // end of UpFilter


//**********************************************************************************************************************
// Techniques
//**********************************************************************************************************************
technique UpFilter4x
{
   pass p0
   <
      float fScaleX = 4.0f;
      float fScaleY = 4.0f;
   >
   {
      VertexShader = compile vs_2_0 VSMain();
      PixelShader = compile ps_2_0 UpFilter();
      ZEnable = false;
   }

}  // end of UpFilter4x

