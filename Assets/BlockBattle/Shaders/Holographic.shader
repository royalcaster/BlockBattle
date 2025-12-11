Shader "BlockBattle/Holographic"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.6, 1.0, 0.5)
        _EmissionColor ("Emission Color", Color) = (0.3, 0.8, 1.0, 1.0)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 1.5
        _FresnelPower ("Fresnel Power", Range(0, 5)) = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 1.0
        _ScanlineSpeed ("Scanline Speed", Range(0, 10)) = 2.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _Transparency ("Transparency", Range(0, 1)) = 0.6
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        
        Pass
        {
            Name "HolographicPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Enable VR stereo rendering
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fresnel : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _EmissionIntensity;
                float _FresnelPower;
                float _FresnelIntensity;
                float _ScanlineSpeed;
                float _ScanlineIntensity;
                float _Transparency;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Setup VR stereo rendering
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                
                // Calculate fresnel effect
                float3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                float fresnel = 1.0 - saturate(dot(normalize(normalInput.normalWS), normalize(viewDirWS)));
                output.fresnel = pow(fresnel, _FresnelPower);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Setup VR stereo rendering
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Base color with transparency - this preserves the block's original color
                half4 color = _BaseColor;
                color.a *= _Transparency;
                
                // Fresnel effect (edges glow more) - tint emission with base color for better color preservation
                float fresnel = input.fresnel * _FresnelIntensity;
                // Blend emission color with base color to maintain block color while adding holographic glow
                half3 tintedEmission = lerp(_EmissionColor.rgb, _BaseColor.rgb, 0.3);
                half3 fresnelColor = tintedEmission * fresnel;
                
                // Scanline effect
                float time = _Time.y;
                float scanline = sin((input.positionWS.y + time * _ScanlineSpeed) * 20.0) * 0.5 + 0.5;
                scanline = lerp(1.0, scanline, _ScanlineIntensity);
                
                // Combine base color with emission and fresnel
                // Add fresnel glow but keep base color prominent
                color.rgb = lerp(color.rgb, color.rgb + fresnelColor * _EmissionIntensity, 0.5);
                color.rgb *= scanline;
                
                // Add subtle emission glow that complements the base color
                color.rgb += tintedEmission * _EmissionIntensity * 0.2;
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

