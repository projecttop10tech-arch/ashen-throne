Shader "AshenThrone/ColorblindFilter"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Mode ("Colorblind Mode (0=Protan, 1=Deutan, 2=Tritan)", Int) = 0
        _Intensity ("Filter Intensity", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ColorblindFilter"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            int _Mode;
            float _Intensity;

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

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            // Daltonization correction matrices
            // Simulate then correct (shift lost info into visible channels)
            half3 ApplyProtanopia(half3 c)
            {
                // Simulate protanopia
                half3 sim;
                sim.r = 0.567 * c.r + 0.433 * c.g;
                sim.g = 0.558 * c.r + 0.442 * c.g;
                sim.b = 0.242 * c.g + 0.758 * c.b;
                // Error and correction
                half3 err = c - sim;
                half3 correction;
                correction.r = 0;
                correction.g = err.r * 0.7 + err.g;
                correction.b = err.r * 0.7 + err.b;
                return c + correction;
            }

            half3 ApplyDeuteranopia(half3 c)
            {
                half3 sim;
                sim.r = 0.625 * c.r + 0.375 * c.g;
                sim.g = 0.7 * c.r + 0.3 * c.g;
                sim.b = 0.3 * c.g + 0.7 * c.b;
                half3 err = c - sim;
                half3 correction;
                correction.r = err.g * 0.7 + err.r;
                correction.g = 0;
                correction.b = err.g * 0.7 + err.b;
                return c + correction;
            }

            half3 ApplyTritanopia(half3 c)
            {
                half3 sim;
                sim.r = 0.95 * c.r + 0.05 * c.g;
                sim.g = 0.433 * c.g + 0.567 * c.b;
                sim.b = 0.475 * c.g + 0.525 * c.b;
                half3 err = c - sim;
                half3 correction;
                correction.r = err.b * 0.7 + err.r;
                correction.g = err.b * 0.7 + err.g;
                correction.b = 0;
                return c + correction;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 corrected = col.rgb;

                if (_Mode == 0) corrected = ApplyProtanopia(col.rgb);
                else if (_Mode == 1) corrected = ApplyDeuteranopia(col.rgb);
                else corrected = ApplyTritanopia(col.rgb);

                col.rgb = lerp(col.rgb, saturate(corrected), _Intensity);
                return col;
            }
            ENDHLSL
        }
    }
}
