#ifndef TEX_2D_ARRAY_DEPTH_ONLY_PASS_INCLUDED
#define TEX_2D_ARRAY_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 position     : POSITION;
    float3 texcoord     : TEXCOORD0;
    float4 animInfo     : TEXCOORD3;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float3 uv           : TEXCOORD0;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////
float2 GetTexUVOffset(float AnimTime, float4 AnimInfo)
{
    uint frameCount = round(AnimInfo.x);

    if (frameCount > 1) {
        float frameInterval = AnimInfo.y;

        float cycleTime = fmod(AnimTime, frameInterval * frameCount);
        uint curFrame = floor(cycleTime / frameInterval);
        uint framePerRow = round(AnimInfo.w);
        
        return float2((curFrame % framePerRow) * AnimInfo.z, (curFrame / framePerRow) * -AnimInfo.z);
    } else {
        return float2(0, 0);
    }
}

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    //output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.uv = input.texcoord + float3(GetTexUVOffset(_Time.y, input.animInfo), 0);
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARRAY_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);

#ifdef LOD_FADE_CROSSFADE
    return input.positionCS.z; //LODFadeCrossFade(input.positionCS);
#endif

    return input.positionCS.z;
}
#endif
