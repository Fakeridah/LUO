Shader "Hidden/Ridah/PortalApertureComposite"
{
    Properties
    {
        _MainTex("Portal View", 2D) = "black" {}
        _PortalStencilRef("Portal Stencil Reference", Float) = 64
        _PortalStencilWriteMask("Portal Stencil Write Mask", Float) = 12
        _PortalCompositeStencilRef("Composite Stencil Reference", Float) = 4
        _PortalCompositeStencilReadMask("Composite Stencil Read Mask", Float) = 12
        _PortalDepthStencilRef("Depth Stencil Reference", Float) = 4
        _PortalDepthStencilReadMask("Depth Stencil Read Mask", Float) = 4
        _OverlapColor("Portal Overlap Color", Color) = (0, 0.5843, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "MarkAperture"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            ColorMask 0
            Stencil
            {
                Ref [_PortalStencilRef]
                ReadMask 12
                WriteMask [_PortalStencilWriteMask]
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                return output;
            }

            half4 FragMask(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "CompositeAperture"
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask RGBA
            Stencil
            {
                Ref [_PortalCompositeStencilRef]
                ReadMask [_PortalCompositeStencilReadMask]
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertFullscreenTexture
            #pragma fragment FragComposite
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            struct FullscreenTextureVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            FullscreenTextureVaryings VertFullscreenTexture(
                uint vertexID : SV_VertexID)
            {
                FullscreenTextureVaryings output;
                float2 position = float2(
                    (vertexID << 1) & 2,
                    vertexID & 2);
                output.positionCS = float4(
                    position * 2.0 - 1.0, 0.0, 1.0);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 FragComposite(FullscreenTextureVaryings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                    uv.y = 1.0 - uv.y;
                #endif
                return tex2D(_MainTex, uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ClearApertureDepth"
            Cull Back
            ZWrite On
            ZTest Always
            ColorMask 0
            Stencil
            {
                Ref [_PortalDepthStencilRef]
                ReadMask [_PortalDepthStencilReadMask]
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertFullscreen
            #pragma fragment FragClearDepth
            #include "UnityCG.cginc"

            struct FullscreenVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            FullscreenVaryings VertFullscreen(uint vertexID : SV_VertexID)
            {
                FullscreenVaryings output;
                float2 position = float2(
                    (vertexID << 1) & 2,
                    vertexID & 2);
                output.positionCS = float4(position * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            float FragClearDepth(FullscreenVaryings input) : SV_Depth
            {
                #if UNITY_REVERSED_Z
                return 0.0;
                #else
                return 1.0;
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "RestoreAperturePlaneDepth"
            Cull Back
            ZWrite On
            ZTest Always
            ColorMask 0
            Stencil
            {
                Ref [_PortalDepthStencilRef]
                ReadMask [_PortalDepthStencilReadMask]
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepthOnly
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                return output;
            }

            half4 FragDepthOnly(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "MarkNearPlaneCap"
            Cull Off
            ZWrite Off
            // Source DrawRenderFixMesh disables depth testing while it writes
            // the cap into the portal stencil mask.
            ZTest Always
            ColorMask 0
            Stencil
            {
                Ref [_PortalStencilRef]
                ReadMask 12
                WriteMask [_PortalStencilWriteMask]
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                return output;
            }

            half4 FragMask(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "CompositePortalOverlapColor"
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask RGBA
            Stencil
            {
                Ref 12
                ReadMask 12
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertFullscreen
            #pragma fragment FragOverlapColor
            #include "UnityCG.cginc"

            float4 _OverlapColor;

            struct FullscreenVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            FullscreenVaryings VertFullscreen(uint vertexID : SV_VertexID)
            {
                FullscreenVaryings output;
                float2 position = float2(
                    (vertexID << 1) & 2,
                    vertexID & 2);
                output.positionCS = float4(position * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            half4 FragOverlapColor(FullscreenVaryings input) : SV_Target
            {
                return _OverlapColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ClearPortalStencilBits"
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0
            Stencil
            {
                Ref 0
                ReadMask 12
                WriteMask 12
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertFullscreen
            #pragma fragment FragClearStencil
            #include "UnityCG.cginc"

            struct FullscreenVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            FullscreenVaryings VertFullscreen(uint vertexID : SV_VertexID)
            {
                FullscreenVaryings output;
                float2 position = float2(
                    (vertexID << 1) & 2,
                    vertexID & 2);
                output.positionCS = float4(position * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            half4 FragClearStencil(FullscreenVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
