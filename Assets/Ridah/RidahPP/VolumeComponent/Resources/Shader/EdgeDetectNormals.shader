// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Ridah/EdgeDetect" { 
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
	}

	HLSLINCLUDE
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Ridah.hlsl"
	struct appdata
	{
		uint vertexID : SV_VertexID;
		half2 texcoord :TEXCOORD;
	};
	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv[5] : TEXCOORD0;
	};
	
	struct v2fd {
		float4 pos : SV_POSITION;
		float2 uv[2] : TEXCOORD0;
	};

	sampler2D _MainTex;
	uniform float4 _MainTex_TexelSize;
	half4 _MainTex_ST;

	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_TexelSize;;
	half4 _PostSource_ST;



	uniform half4 _Sensitivity; 
	uniform half4 _BgColor;
	uniform half _BgFade;
	uniform half _SampleDistance1;
	uniform half _SampleDistance2;
	uniform half _SampleDistance3;
	uniform float _Exponent;
	uniform float _Threshold;
	uniform float4x4 _WorldToCameraMatrix; 

	struct v2flum {
		float4 pos : SV_POSITION;
		float2 uv[3] : TEXCOORD0;
	};

	v2flum vertLum (appdata v)
	{
		v2flum o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		float2 uv = v.texcoord;
		o.uv[0] = UnityStereoScreenSpaceUVAdjust(uv, _PostSource_ST);
		o.uv[1] = UnityStereoScreenSpaceUVAdjust(uv + float2(-_PostSource_TexelSize.x, -_PostSource_TexelSize.y) * _SampleDistance3, _PostSource_ST);
		o.uv[2] = UnityStereoScreenSpaceUVAdjust(uv + float2(+_PostSource_TexelSize.x, -_PostSource_TexelSize.y) * _SampleDistance3, _PostSource_ST);
		return o;
	}

	half4 fragLum (v2flum i) : SV_Target
	{
		half4 original = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.uv[0]);

		// a very simple cross gradient filter

		half3 p1 = original.rgb;
		half3 p2 = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.uv[1]).rgb;
		half3 p3 = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.uv[2]).rgb;
		
		half3 diff = p1 * 2 - p2 - p3;
		half len = dot(diff, diff);
		len = step(len, _Threshold);
		//if(len >= _Threshold)
		//	original.rgb = 0;

		return len * lerp(original, _BgColor, _BgFade);			
	}	
	
	inline half CheckSame (half4 n, half4 theSample)
	{
		// difference in normals
		// do not bother decoding normals - there's no need here
		half2 diff = abs(n.xy - theSample.xy) * _Sensitivity.y;
		int isSameNormal = (diff.x + diff.y) * _Sensitivity.y < 0.1;
		// difference in depth
		float zdiff = abs(n.w-theSample.w);
		// scale the required threshold by the distance
		int isSameDepth = zdiff * _Sensitivity.x < 0.09 * n.w;
		// return:
		// 1 - if normals and depth are similar enough
		// 0 - otherwise
		
		return isSameNormal * isSameDepth ? 1.0 : 0.0;
	}	
		
	v2f vertRobert( appdata v ) 
	{
		v2f o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		
		float2 uv = v.texcoord.xy;
		o.uv[0] = UnityStereoScreenSpaceUVAdjust(uv, _PostSource_ST);
		
		#if UNITY_UV_STARTS_AT_TOP
		if (_PostSource_TexelSize.y < 0)
			uv.y = 1-uv.y;
		#endif
				
		// calc coord for the X pattern
		// maybe nicer TODO for the future: 'rotated triangles'
		
		o.uv[1] = UnityStereoScreenSpaceUVAdjust(uv + _PostSource_TexelSize.xy * half2(1,1) * _SampleDistance1, _PostSource_ST);
		o.uv[2] = UnityStereoScreenSpaceUVAdjust(uv + _PostSource_TexelSize.xy * half2(-1,-1) * _SampleDistance1, _PostSource_ST);
		o.uv[3] = UnityStereoScreenSpaceUVAdjust(uv + _PostSource_TexelSize.xy * half2(-1,1) * _SampleDistance1, _PostSource_ST);
		o.uv[4] = UnityStereoScreenSpaceUVAdjust(uv + _PostSource_TexelSize.xy * half2(1,-1) * _SampleDistance1, _PostSource_ST);
				 
		return o;
	} 
	
	v2f vertThin( appdata v )
	{
		v2f o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		
		float2 uv = v.texcoord.xy;
		o.uv[0] = UnityStereoScreenSpaceUVAdjust(uv, _PostSource_ST);
		
		#if UNITY_UV_STARTS_AT_TOP
		if (_PostSource_TexelSize.y < 0)
			uv.y = 1-uv.y;
		#endif
		
		o.uv[1] = UnityStereoScreenSpaceUVAdjust(uv, _PostSource_ST);
		o.uv[4] = UnityStereoScreenSpaceUVAdjust(uv, _PostSource_ST);
				
		// offsets for two additional samples
		o.uv[2] = UnityStereoScreenSpaceUVAdjust(uv + float2(-_PostSource_TexelSize.x, -_PostSource_TexelSize.y) * _SampleDistance1, _PostSource_ST);
		o.uv[3] = UnityStereoScreenSpaceUVAdjust(uv + float2(+_PostSource_TexelSize.x, -_PostSource_TexelSize.y) * _SampleDistance1, _PostSource_ST);
		
		return o;
	}	  
	 
	v2fd vertD( appdata v )
	{
		v2fd o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		
		float2 uv = v.texcoord.xy;
		o.uv[0] = uv;
		
		#if UNITY_UV_STARTS_AT_TOP
		if (_PostSource_TexelSize.y < 0)
			uv.y = 1-uv.y;
		#endif
		
		o.uv[1] = uv;
		
		return o;
	}
	
            sampler2D _GrabbedDepthTexture;
            float4 _GrabbedDepthTexture_TexelSize;
	float4 fragDCheap(v2fd i) : SV_Target 
	{	
		// inspired by borderlands implementation of popular "sobel filter"

		//float centerDepth = Linear01Depth(SampleSceneDepth(i.uv[1]),_ZBufferParams);
		float centerDepth = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]),_ZBufferParams);
		float4 depthsDiag;
		float4 depthsAxis;

		float2 uvDist = _SampleDistance2 * _PostSource_TexelSize.xy;

		depthsDiag.x = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]+uvDist),_ZBufferParams); // TR
		depthsDiag.y = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]+uvDist*float2(-1,1)),_ZBufferParams); // TL
		depthsDiag.z = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]-uvDist*float2(-1,1)),_ZBufferParams); // BR
		depthsDiag.w = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]-uvDist),_ZBufferParams); // BL
						
		depthsAxis.x = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]+uvDist*float2(0,1)),_ZBufferParams); // T
		depthsAxis.y = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]-uvDist*float2(1,0)),_ZBufferParams); // L
		depthsAxis.z = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]+uvDist*float2(1,0)),_ZBufferParams); // R
		depthsAxis.w = Linear01Depth(tex2D(_GrabbedDepthTexture, i.uv[1]-uvDist*float2(0,1)),_ZBufferParams); // B

		depthsDiag -= centerDepth;
		depthsAxis /= centerDepth;

		const float4 HorizDiagCoeff = float4(1,1,-1,-1);
		const float4 VertDiagCoeff = float4(-1,1,-1,1);
		const float4 HorizAxisCoeff = float4(1,0,0,-1);
		const float4 VertAxisCoeff = float4(0,1,-1,0);

		float4 SobelH = depthsDiag * HorizDiagCoeff + depthsAxis * HorizAxisCoeff;
		float4 SobelV = depthsDiag * VertDiagCoeff + depthsAxis * VertAxisCoeff;

		float SobelX = dot(SobelH, float4(1,1,1,1));
		float SobelY = dot(SobelV, float4(1,1,1,1));
		float Sobel = sqrt(SobelX * SobelX + SobelY * SobelY);

		Sobel = 1.0-pow(saturate(Sobel), _Exponent);
		return Sobel * lerp(SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(i.uv[0], _PostSource_ST)), _BgColor, _BgFade);
	}

	// pretty much also just a sobel filter, except for that edges "outside" the silhouette get discarded
	//  which makes it compatible with other depth based post fx

	float4 fragD(v2fd i) : SV_Target 
	{	
		// inspired by borderlands implementation of popular "sobel filter"

		float centerDepth = Linear01Depth(SampleSceneDepth(i.uv[1]),_ZBufferParams);
		float4 depthsDiag;
		float4 depthsAxis;

		float2 uvDist = _SampleDistance2 * _PostSource_TexelSize.xy;

		depthsDiag.x = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]+uvDist, _CameraDepthTexture_ST)),_ZBufferParams); // TR
		depthsDiag.y = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]+uvDist*float2(-1,1), _CameraDepthTexture_ST)),_ZBufferParams); // TL
		depthsDiag.z = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]-uvDist*float2(-1,1), _CameraDepthTexture_ST)),_ZBufferParams); // BR
		depthsDiag.w = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]-uvDist, _CameraDepthTexture_ST)),_ZBufferParams); // BL

		depthsAxis.x = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]+uvDist*float2(0,1), _CameraDepthTexture_ST)),_ZBufferParams); // T
		depthsAxis.y = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]-uvDist*float2(1,0), _CameraDepthTexture_ST)),_ZBufferParams); // L
		depthsAxis.z = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]+uvDist*float2(1,0), _CameraDepthTexture_ST)),_ZBufferParams); // R
		depthsAxis.w = Linear01Depth(SampleSceneDepth(UnityStereoScreenSpaceUVAdjust(i.uv[1]-uvDist*float2(0,1), _CameraDepthTexture_ST)),_ZBufferParams); // B

		// make it work nicely with depth based image effects such as depth of field:
		depthsDiag = (depthsDiag > centerDepth.xxxx) ? depthsDiag : centerDepth.xxxx;
		depthsAxis = (depthsAxis > centerDepth.xxxx) ? depthsAxis : centerDepth.xxxx;

		depthsDiag -= centerDepth;
		depthsAxis /= centerDepth;

		const float4 HorizDiagCoeff = float4(1,1,-1,-1);
		const float4 VertDiagCoeff = float4(-1,1,-1,1);
		const float4 HorizAxisCoeff = float4(1,0,0,-1);
		const float4 VertAxisCoeff = float4(0,1,-1,0);

		float4 SobelH = depthsDiag * HorizDiagCoeff + depthsAxis * HorizAxisCoeff;
		float4 SobelV = depthsDiag * VertDiagCoeff + depthsAxis * VertAxisCoeff;

		float SobelX = dot(SobelH, float4(1,1,1,1));
		float SobelY = dot(SobelV, float4(1,1,1,1));
		float Sobel = sqrt(SobelX * SobelX + SobelY * SobelY);

		Sobel = 1.0-pow(saturate(Sobel), _Exponent);
		return Sobel * lerp(SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(i.uv[0], _PostSource_ST)), _BgColor, _BgFade);
	}

	half4 fragRobert(v2f i) : SV_Target {			
		half depth1 =  Linear01Depth(SampleSceneDepth(i.uv[1]),_ZBufferParams);
		half depth2 =  Linear01Depth(SampleSceneDepth(i.uv[2]),_ZBufferParams);
		half depth3 =  Linear01Depth(SampleSceneDepth(i.uv[3]),_ZBufferParams);
		half depth4 =  Linear01Depth(SampleSceneDepth(i.uv[4]),_ZBufferParams);
		half4 sample1 = half4(SampleSceneNormals(i.uv[1].xy),depth1);//
		half4 sample2 = half4(SampleSceneNormals(i.uv[2].xy),depth2);//
		half4 sample3 = half4(SampleSceneNormals(i.uv[3].xy),depth3);//
		half4 sample4 = half4(SampleSceneNormals(i.uv[4].xy),depth4);//

		half edge = 1.0;
		
		edge *= CheckSame(sample1, sample2);
		edge *= CheckSame(sample3, sample4);

		return edge * lerp(SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.uv[0]), _BgColor, _BgFade);
	}
	
	half4 fragThin (v2f i) : SV_Target
	{
		half4 original = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,i.uv[0]);
		
		half depth1 = Linear01Depth(SampleSceneDepth(i.uv[1]),_ZBufferParams);
		half depth2 = Linear01Depth(SampleSceneDepth(i.uv[2]),_ZBufferParams);
		half depth3 = Linear01Depth(SampleSceneDepth(i.uv[3]),_ZBufferParams);

		half4 center =	half4(SampleSceneNormals(i.uv[1]),depth1);//
		half4 sample1 = half4(SampleSceneNormals(i.uv[2]),depth2);//
		half4 sample2 = half4(SampleSceneNormals(i.uv[3]),depth3);//
		
		// encoded normal
		//half2 centerNormal = center.xy;
		// decoded depth
		//float centerDepth = DecodeFloatRG (center.zw);
		
		half edge = 1.0;
		
		edge *= CheckSame(center, sample1);
		edge *= CheckSame(center, sample2);
			
		return edge * lerp(original, _BgColor, _BgFade);
	}
	
	ENDHLSL
	
Subshader {
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex vertThin
      #pragma fragment fragThin
      ENDHLSL
  }
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex vertRobert
      #pragma fragment fragRobert
      ENDHLSL
  }
 Pass {Tags{"LightMode" = "UseColorTexture" }
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
	  #pragma target 3.0   
      #pragma vertex vertD
      #pragma fragment fragDCheap
      ENDHLSL
  }
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
	  #pragma target 3.0   
      #pragma vertex vertD
      #pragma fragment fragD
      ENDHLSL
  }
 Pass {
	  ZTest Always Cull Off ZWrite Off
      HLSLPROGRAM
	  #pragma target 3.0   
      #pragma vertex vertLum
      #pragma fragment fragLum
      ENDHLSL
  }
}

Fallback off
	
} // shader
