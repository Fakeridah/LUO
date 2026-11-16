// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'



Shader "Ridah/FastBlur" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	
	HLSLINCLUDE
	
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "Ridah.hlsl"

		
		TEXTURE2D_X(_PostSource);
		SAMPLER(sampler_PostSource);
		half4 _PostSource_TexelSize;
		half4 _PostSource_ST;

		uniform half4 _Parameter;

		
		struct appdata
		{
			uint vertexID : SV_VertexID;
			half2 texcoord :TEXCOORD;
		};

		struct v2f_tap
		{
			float4 pos : SV_POSITION;
			half2 uv20 : TEXCOORD0;
			half2 uv21 : TEXCOORD1;
			half2 uv22 : TEXCOORD2;
			half2 uv23 : TEXCOORD3;
		};			

		
		v2f_tap vert4Tap ( appdata v )
		{
			v2f_tap o;

			o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
			v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
        	o.uv20 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _PostSource_TexelSize.xy, _PostSource_ST);
			o.uv21 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _PostSource_TexelSize.xy * half2(-0.5h,-0.5h), _PostSource_ST);
			o.uv22 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _PostSource_TexelSize.xy * half2(0.5h,-0.5h), _PostSource_ST);
			o.uv23 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _PostSource_TexelSize.xy * half2(-0.5h,0.5h), _PostSource_ST);

			return o; 
		}					
		
		half4 fragDownsample ( v2f_tap i ) : SV_Target
		{				
			half4 color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv20);
			color += SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv21);
			color += SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv22);
			color += SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv23);
			return color / 4;
		}
	
		// weight curves

		static const half curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };  // gauss'ish blur weights

		static const half4 curve4[7] = { half4(0.0205,0.0205,0.0205,0), half4(0.0855,0.0855,0.0855,0), half4(0.232,0.232,0.232,0),
			half4(0.324,0.324,0.324,1), half4(0.232,0.232,0.232,0), half4(0.0855,0.0855,0.0855,0), half4(0.0205,0.0205,0.0205,0) };

		struct v2f_withBlurCoords8 
		{
			float4 pos : SV_POSITION;
			half4 uv : TEXCOORD0;
			half2 offs : TEXCOORD1;
		};	
		
		struct v2f_withBlurCoordsSGX 
		{
			float4 pos : SV_POSITION;
			half2 uv : TEXCOORD0;
			half4 offs[3] : TEXCOORD1;
		};

		v2f_withBlurCoords8 vertBlurHorizontal (appdata v)
		{
			v2f_withBlurCoords8 o;
			o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
			v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
			
			o.uv = half4(v.texcoord.xy,1,1);
			o.offs = _PostSource_TexelSize.xy * half2(1.0, 0.0) * _Parameter.x;

			return o; 
		}
		
		v2f_withBlurCoords8 vertBlurVertical (appdata v)
		{
			v2f_withBlurCoords8 o;
			o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
			v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
			
			o.uv = half4(v.texcoord.xy,1,1);
			o.offs = _PostSource_TexelSize.xy * half2(0.0, 1.0) * _Parameter.x;
			 
			return o; 
		}	

		half4 fragBlur8 ( v2f_withBlurCoords8 i ) : SV_Target
		{
			half2 uv = i.uv.xy; 
			half2 netFilterWidth = i.offs;  
			half2 coords = uv - netFilterWidth * 3.0;  
			
			half4 color = 0;
  			for( int l = 0; l < 7; l++ )  
  			{   
				half4 tap = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, coords);
				color += tap * curve4[l];
				coords += netFilterWidth;
  			}
			return color;
		}

	
		v2f_withBlurCoordsSGX vertBlurHorizontalSGX (appdata v)
		{
			v2f_withBlurCoordsSGX o;
			o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
			v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
			
			o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord.xy, _PostSource_ST).xy;

			half offsetMagnitude = _PostSource_TexelSize.x * _Parameter.x;
			o.offs[0] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsetMagnitude * half4(-3.0h, 0.0h, 3.0h, 0.0h), _PostSource_ST);
			o.offs[1] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsetMagnitude * half4(-2.0h, 0.0h, 2.0h, 0.0h), _PostSource_ST);
			o.offs[2] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsetMagnitude * half4(-1.0h, 0.0h, 1.0h, 0.0h), _PostSource_ST);

			return o; 
		}
		
		v2f_withBlurCoordsSGX vertBlurVerticalSGX (appdata v)
		{
			v2f_withBlurCoordsSGX o;
			o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
			v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
			
			o.uv = half4(UnityStereoScreenSpaceUVAdjust(v.texcoord.xy, _PostSource_ST),1,1).xy;

			half offsetMagnitude = _PostSource_TexelSize.y * _Parameter.x;
			o.offs[0] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsetMagnitude * half4(0.0h, -3.0h, 0.0h, 3.0h), _PostSource_ST);
			o.offs[1] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsetMagnitude * half4(0.0h, -2.0h, 0.0h, 2.0h), _PostSource_ST);
			o.offs[2] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsetMagnitude * half4(0.0h, -1.0h, 0.0h, 1.0h), _PostSource_ST);

			return o; 
		}

		half4 fragBlurSGX ( v2f_withBlurCoordsSGX i ) : SV_Target
		{
			half2 uv = i.uv.xy;
			
			half4 color =SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv) * curve4[3];
			
  			for( int l = 0; l < 3; l++ )  
  			{   
				half4 tapA = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.offs[l].xy);
				half4 tapB = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.offs[l].zw); 
				color += (tapA + tapB) * curve4[l];
  			}

			return color;

		}	
					
	ENDHLSL
	
	SubShader {
	  ZTest Off Cull Off ZWrite Off Blend Off

	// 0
	Pass { 
	
		HLSLPROGRAM
		
		#pragma vertex vert4Tap
		#pragma fragment fragDownsample
		
		ENDHLSL
		 
		}

	// 1
	Pass {
		ZTest Always
		Cull Off
		
		HLSLPROGRAM 
		
		#pragma vertex vertBlurVertical
		#pragma fragment fragBlur8
		
		ENDHLSL
		}	
		
	// 2
	Pass {		
		ZTest Always
		Cull Off
				
		HLSLPROGRAM
		
		#pragma vertex vertBlurHorizontal
		#pragma fragment fragBlur8
		
		ENDHLSL
		}	

	// alternate blur
	// 3
	Pass {
		ZTest Always
		Cull Off
		
		HLSLPROGRAM 
		
		#pragma vertex vertBlurVerticalSGX
		#pragma fragment fragBlurSGX
		
		ENDHLSL
		}	
		
	// 4
	Pass {		
		ZTest Always
		Cull Off
				
		HLSLPROGRAM
		
		#pragma vertex vertBlurHorizontalSGX
		#pragma fragment fragBlurSGX
		
		ENDHLSL
		}	
	}	

	FallBack Off
}
