Shader "Hidden/ComicOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "ComicOutline"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _OutlineThickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float4 _OutlineColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float SampleDepth(float2 uv)
            {
                return SampleSceneDepth(uv);
            }

            float3 SampleNormal(float2 uv)
            {
                return SampleSceneNormals(uv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float2 texelSize = _MainTex_TexelSize.xy * _OutlineThickness;

                // Sobel depth edge detection
                float d0 = SampleDepth(uv + float2(-texelSize.x, -texelSize.y));
                float d1 = SampleDepth(uv + float2(0, -texelSize.y));
                float d2 = SampleDepth(uv + float2(texelSize.x, -texelSize.y));
                float d3 = SampleDepth(uv + float2(-texelSize.x, 0));
                float d4 = SampleDepth(uv);
                float d5 = SampleDepth(uv + float2(texelSize.x, 0));
                float d6 = SampleDepth(uv + float2(-texelSize.x, texelSize.y));
                float d7 = SampleDepth(uv + float2(0, texelSize.y));
                float d8 = SampleDepth(uv + float2(texelSize.x, texelSize.y));

                float sobelX = d0 + 2*d3 + d6 - d2 - 2*d5 - d8;
                float sobelY = d0 + 2*d1 + d2 - d6 - 2*d7 - d8;
                float depthEdge = sqrt(sobelX * sobelX + sobelY * sobelY);
                depthEdge = step(_DepthThreshold, depthEdge);

                // Normal edge detection
                float3 n0 = SampleNormal(uv + float2(-texelSize.x, 0));
                float3 n1 = SampleNormal(uv + float2(texelSize.x, 0));
                float3 n2 = SampleNormal(uv + float2(0, -texelSize.y));
                float3 n3 = SampleNormal(uv + float2(0, texelSize.y));

                float normalEdge = 0;
                normalEdge += 1.0 - dot(n0, n1);
                normalEdge += 1.0 - dot(n2, n3);
                normalEdge = step(_NormalThreshold, normalEdge);

                float edge = saturate(depthEdge + normalEdge);

                // Apply ink-style outline
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, edge);
                return color;
            }
            ENDHLSL
        }
    }
}
