Shader "Custom/Refraction"
{
Properties
{
_Albedo("Albedo", 2D) = "white" {}
_Opacity("Opacity", Range( 0 , 1)) = 1
_Smoothness("Smoothness", Range( 0 , 1)) = 0.5
_Metalness("Metalness", Range( 0 , 1)) = 0
_NormalMap("Normal Map", 2D) = "bump" {}
[Header(Refraction Options (Require HDRP Lit))]
_IndexofRefraction("Index of Refraction", Range(-1, 1.5)) = 1.5
_ChromaticAberration("Chromatic Aberration", Range( 0 , 0.3)) = 0.1
}
SubShader
{
Tags 
{ 
"RenderPipeline" = "HDRenderPipeline" 
"RenderType" = "Transparent" 
"Queue" = "Transparent" 
}
UsePass "HDRP/Lit/Scenepickingpass"
UsePass "HDRP/Lit/DepthOnly"
UsePass "HDRP/Lit/ForwardOnly"
}

Fallback "HDRP/Lit"
}
