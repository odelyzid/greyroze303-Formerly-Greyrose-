//**********************************************************************************************************************
/**
 *  Combines the image with another
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

//**********************************************************************************************************************
// Shader texture; the texture applied to the mesh as the 'Shader index 0' will be assigned to this sampler
// This is the texture the given one will be combined with
//**********************************************************************************************************************
texture g_texSceneColor
<
   string NTM = "Shader";
   int NTMIndex = 0;
>;
sampler2D g_samSceneColor = sampler_state
{
    Texture = <g_texSceneColor>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Linear;
    MipFilter = Linear;
};


//**********************************************************************************************************************
/**
 * Combine the source image with the original image
 *
 * @param Tex     texture coord for source image (the base map)
 * @param Tex2    texture coord for original image (the shader map)
 *
 * @return        pixel color
 **/
//**********************************************************************************************************************
float4 Combine( float2 Tex : TEXCOORD0, float2 Tex2 : TEXCOORD1 ) : COLOR0
{
   // doesn't work:
   //float3 colorOrig = tex2D( g_samSceneColor, Tex2 );
   //colorOrig += tex2D( g_samSrcColor, Tex );
   //return float4( colorOrig, 1.0f );

   //return tex2D(g_samSrcColor, Tex);
   //return tex2D(g_samSceneColor, Tex);

   // Tex2 seems to be bogus....TODO: fix this?  For now, use Tex for both textures
   float3 colorOrig = tex2D( g_samSceneColor, Tex );
   colorOrig += tex2D( g_samSrcColor, Tex );
   return float4( colorOrig, 1.0f );

}  // end of Combine


//**********************************************************************************************************************
// Techniques
//**********************************************************************************************************************
technique Combine
{
   pass p0
   {
      VertexShader = compile vs_2_0 VSMain();
      PixelShader = compile ps_2_0 Combine();
      ZEnable = false;
   }

}  // end of Combine

