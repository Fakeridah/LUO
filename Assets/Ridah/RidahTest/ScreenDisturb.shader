// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Custom/ScreenDisturb"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_FlashTime("FlashTime", Float) = 1
		_PartFlashNoiseTex("PartFlashNoiseTex", 2D) = "white" {}
		_Noise2Tex("Noise2Tex", 2D) = "white" {}
		_Noise2SpeedX("Noise2SpeedX", Float) = 0.86
		_Noise2SpeedY("Noise2SpeedY", Float) = 0.18
		_Noise2RemapOffset("Noise2RemapOffset", Float) = -0.15
		_MainTex("MainTex", 2D) = "white" {}
		_BgMask("BgMask", 2D) = "white" {}
		_Noise2RBOffset("Noise2RBOffset", Float) = 0.15
		_MosaicIntensity("MosaicIntensity", Float) = 6
		_Noise2Intensity("Noise2Intensity", Float) = 0.85
		_MosaicFlashRate("MosaicFlashRate", Float) = 4
		_FlashTimeAdjust("FlashTimeAdjust", Range( 0 , 1)) = 0.8
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

	}

	SubShader
	{
		LOD 0

		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
		
		Stencil
		{
			Ref [_Stencil]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
			CompFront [_StencilComp]
			PassFront [_StencilOp]
			FailFront Keep
			ZFailFront Keep
			CompBack Always
			PassBack Keep
			FailBack Keep
			ZFailBack Keep
		}


		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		
		Pass
		{
			Name "Default"
		CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile __ UNITY_UI_CLIP_RECT
			#pragma multi_compile __ UNITY_UI_ALPHACLIP
			
			#include "UnityShaderVariables.cginc"

			
			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				half2 texcoord  : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				
			};
			
			uniform fixed4 _Color;
			uniform fixed4 _TextureSampleAdd;
			uniform float4 _ClipRect;
			uniform sampler2D _MainTex;
			uniform float _MosaicIntensity;
			uniform float4 _MainTex_ST;
			uniform float _FlashTime;
			uniform float _FlashTimeAdjust;
			uniform sampler2D _PartFlashNoiseTex;
			uniform float _MosaicFlashRate;
			uniform sampler2D _Noise2Tex;
			uniform float _Noise2SpeedX;
			uniform float _Noise2SpeedY;
			uniform float _Noise2RemapOffset;
			uniform float _Noise2Intensity;
			uniform float _Noise2RBOffset;
			uniform sampler2D _BgMask;
			uniform float4 _BgMask_ST;

			
			v2f vert( appdata_t IN  )
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID( IN );
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
				OUT.worldPosition = IN.vertex;
				
				
				OUT.worldPosition.xyz +=  float3( 0, 0, 0 ) ;
				OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

				OUT.texcoord = IN.texcoord;
				
				OUT.color = IN.color * _Color;
				return OUT;
			}

			fixed4 frag(v2f IN  ) : SV_Target
			{
				float2 uv011_g31 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float div12_g31=256.0/float((int)_MosaicIntensity);
				float4 posterize12_g31 = ( floor( float4( uv011_g31, 0.0 , 0.0 ) * div12_g31 ) / div12_g31 );
				float4 tex2DNode15_g31 = tex2D( _MainTex, posterize12_g31.rg );
				float2 uv_MainTex = IN.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float4 tex2DNode50 = tex2D( _MainTex, uv_MainTex );
				float4 break22_g31 = tex2DNode50;
				float temp_output_7_0 = saturate( ( ( 0.2 * sin( ( _FlashTime * _Time.y ) ) ) - ( _FlashTimeAdjust * 0.2 ) ) );
				float temp_output_33_0_g31 = ( _SinTime.w * 1.0 );
				float2 temp_cast_5 = (temp_output_33_0_g31).xx;
				float dotResult4_g34 = dot( temp_cast_5 , float2( 12.9898,78.233 ) );
				float lerpResult10_g34 = lerp( 0.1 , 0.3 , frac( ( sin( dotResult4_g34 ) * 43758.55 ) ));
				float2 temp_cast_6 = (temp_output_33_0_g31).xx;
				float dotResult4_g32 = dot( temp_cast_6 , float2( 12.9898,78.233 ) );
				float lerpResult10_g32 = lerp( 1.3 , 1.5 , frac( ( sin( dotResult4_g32 ) * 43758.55 ) ));
				float4 appendResult5_g31 = (float4(lerpResult10_g34 , lerpResult10_g32 , 0.0 , 0.0));
				float2 uv06_g31 = IN.texcoord.xy * float2( 0.18,2.21 ) + appendResult5_g31.xy;
				float div8_g31=256.0/float((int)_MosaicFlashRate);
				float4 posterize8_g31 = ( floor( float4( uv06_g31, 0.0 , 0.0 ) * div8_g31 ) / div8_g31 );
				float temp_output_3_0_g33 = ( 0.8 - tex2D( _PartFlashNoiseTex, posterize8_g31.rg ).r );
				float ifLocalVar30_g31 = 0;
				if( temp_output_7_0 <= 0.0 )
				ifLocalVar30_g31 = 1.0;
				else
				ifLocalVar30_g31 = saturate( saturate( ( temp_output_3_0_g33 / fwidth( temp_output_3_0_g33 ) ) ) );
				float lerpResult24_g31 = lerp( tex2DNode15_g31.r , break22_g31.x , ifLocalVar30_g31);
				float lerpResult27_g31 = lerp( tex2DNode15_g31.g , break22_g31.y , ifLocalVar30_g31);
				float lerpResult25_g31 = lerp( tex2DNode15_g31.b , break22_g31.z , ifLocalVar30_g31);
				float lerpResult26_g31 = lerp( tex2DNode15_g31.a , break22_g31.w , ifLocalVar30_g31);
				float4 appendResult28_g31 = (float4(lerpResult24_g31 , lerpResult27_g31 , lerpResult25_g31 , lerpResult26_g31));
				float4 break66 = appendResult28_g31;
				float2 uv014_g29 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 appendResult9_g29 = (float4(_Noise2SpeedX , _Noise2SpeedY , 0.0 , 0.0));
				float2 uv04_g29 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner5_g29 = ( 1.0 * _Time.y * appendResult9_g29.xy + uv04_g29);
				float temp_output_29_0_g29 = _Noise2RemapOffset;
				float temp_output_34_0_g29 = ( temp_output_7_0 * _Noise2Intensity );
				float2 temp_output_16_0_g29 = ( uv014_g29 + ( (temp_output_29_0_g29 + (tex2D( _Noise2Tex, panner5_g29 ).r - 0.0) * (( temp_output_29_0_g29 + 1.0 ) - temp_output_29_0_g29) / (1.0 - 0.0)) * temp_output_34_0_g29 ) );
				float temp_output_30_0_g29 = _Noise2RBOffset;
				float4 appendResult20_g29 = (float4(( temp_output_34_0_g29 * ( -1.0 * temp_output_30_0_g29 ) ) , 0.0 , 0.0 , 0.0));
				float2 uv_BgMask = IN.texcoord.xy * _BgMask_ST.xy + _BgMask_ST.zw;
				float4 tex2DNode27 = tex2D( _BgMask, uv_BgMask );
				float lerpResult28 = lerp( break66.x , tex2D( _MainTex, ( float4( temp_output_16_0_g29, 0.0 , 0.0 ) + appendResult20_g29 ).xy ).r , tex2DNode27.r);
				float lerpResult29 = lerp( break66.y , tex2D( _MainTex, temp_output_16_0_g29 ).g , tex2DNode27.r);
				float4 appendResult24_g29 = (float4(( temp_output_34_0_g29 * ( 1.0 * temp_output_30_0_g29 ) ) , 0.0 , 0.0 , 0.0));
				float lerpResult30 = lerp( break66.z , tex2D( _MainTex, ( float4( temp_output_16_0_g29, 0.0 , 0.0 ) + appendResult24_g29 ).xy ).b , tex2DNode27.r);
				float4 appendResult31 = (float4(lerpResult28 , lerpResult29 , lerpResult30 , tex2DNode50.a));
				
				half4 color = appendResult31;
				
				#ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
				
				#ifdef UNITY_UI_ALPHACLIP
				clip (color.a - 0.001);
				#endif

				return color;
			}
		ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18000
2617;170;1731;1040;-71.18152;-0.5645142;1;True;False
Node;AmplifyShaderEditor.RangedFloatNode;5;-1450.12,388.4572;Inherit;False;Property;_FlashTime;FlashTime;0;0;Create;True;0;0;False;0;1;0.6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;3;-1459.12,474.4571;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;91;-1297.371,121.8235;Inherit;False;350;166;扰动闪烁的时间调整，0-1,0表示闪烁的间隔时间和闪烁时间相同，越接近1闪烁时间越短，1不闪烁;1;80;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;77;-1273.371,447.8235;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;80;-1247.371,171.8235;Inherit;False;Property;_FlashTimeAdjust;FlashTimeAdjust;20;0;Create;True;0;0;False;0;0.8;0.8;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;92;-1145.72,363.9811;Inherit;False;Constant;_Float3;Float 3;20;0;Create;True;0;0;False;0;0.2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SinOpNode;4;-1133.12,476.4571;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;-996.1191,455.4571;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;82;-867.3708,256.8235;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;41;-1104.946,634.382;Inherit;False;1534.608;725.488;强烈扰动;10;23;25;24;26;36;20;19;22;56;59;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;88;71.51613,15.185;Inherit;False;968.3649;515.414;柔和扰动改为局部马赛克;4;66;64;63;50;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;79;-816.3708,447.8235;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;12;-496.6694,325.5838;Inherit;True;Property;_MainTex;MainTex;11;0;Create;True;0;0;False;0;None;None;False;white;Auto;Texture2D;-1;0;1;SAMPLER2D;0
Node;AmplifyShaderEditor.SamplerNode;50;121.5161,300.599;Inherit;True;Property;_TextureSample6;Texture Sample 6;13;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;25;-1040.301,931.5922;Inherit;True;Property;_Noise2Tex;Noise2Tex;4;0;Create;True;0;0;False;0;4e3951c538fc8a647a4a10a99b480987;4e3951c538fc8a647a4a10a99b480987;False;white;Auto;Texture2D;-1;0;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RangedFloatNode;64;168.9685,149.185;Inherit;False;Property;_MosaicFlashRate;MosaicFlashRate;18;0;Create;True;0;0;False;0;4;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;7;-668.1191,473.4571;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;23;-800.3011,1048.592;Inherit;False;Property;_Noise2SpeedY;Noise2SpeedY;8;0;Create;True;0;0;False;0;0.18;0.18;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;24;-801.3011,966.5922;Inherit;False;Property;_Noise2SpeedX;Noise2SpeedX;6;0;Create;True;0;0;False;0;0.86;0.86;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;26;-807.8646,868.3591;Inherit;False;Property;_Noise2RemapOffset;Noise2RemapOffset;10;0;Create;True;0;0;False;0;-0.15;-0.15;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;56;-782.5947,688.6831;Inherit;False;Property;_Noise2Intensity;Noise2Intensity;17;0;Create;True;0;0;False;0;0.85;0.85;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;63;170.9685,65.185;Inherit;False;Property;_MosaicIntensity;MosaicIntensity;16;0;Create;True;0;0;False;0;6;6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;36;-802.9167,790.5609;Inherit;False;Property;_Noise2RBOffset;Noise2RBOffset;13;0;Create;True;0;0;False;0;0.15;0.15;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;59;-502.6926,903.0483;Inherit;False;SingleDisturb;-1;;29;a8113882c53ba64408ab94202aae5c7d;0;7;33;FLOAT;0;False;30;FLOAT;0.1;False;29;FLOAT;0;False;13;FLOAT;0;False;3;SAMPLER2D;0;False;7;FLOAT;0;False;8;FLOAT;0;False;3;FLOAT4;17;FLOAT4;28;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;90;468.1565,88.73927;Inherit;False;PartMosaic;1;;31;79f325ad22ba9cb4c9ab63fbe17d1e76;0;5;29;FLOAT;0;False;18;FLOAT;6;False;19;FLOAT;9;False;21;FLOAT4;0,0,0,0;False;20;SAMPLER2D;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode;22;103.662,1129.87;Inherit;True;Property;_TextureSample0;Texture Sample 0;12;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;20;109.662,693.8694;Inherit;True;Property;_TextureSample5;Texture Sample 5;11;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;19;104.662,914.8694;Inherit;True;Property;_TextureSample4;Texture Sample 4;10;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;27;490.8636,633.5511;Inherit;True;Property;_BgMask;BgMask;12;0;Create;True;0;0;False;0;-1;f0fe2b316d23fe74eac913a1e482c4aa;f0fe2b316d23fe74eac913a1e482c4aa;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;66;786.881,88.65327;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.CommentaryNode;40;-1106.462,-723.4872;Inherit;False;1840.221;697.6157;柔和扰动;11;8;55;14;11;13;16;15;37;10;9;60;;1,1,1,1;0;0
Node;AmplifyShaderEditor.LerpOp;29;1062.935,608.9891;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;28;1059.935,459.9891;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;30;1066.935,765.9891;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;16;-839.6454,-542.9538;Inherit;False;Property;_Noise1RemapOffset;Noise1RemapOffset;9;0;Create;True;0;0;False;0;-0.98;-0.98;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;37;-823.8395,-621.8693;Inherit;False;Property;_Noise1RBOffset;Noise1RBOffset;14;0;Create;True;0;0;False;0;0.005;0.005;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;8;-1056.462,-309.4872;Inherit;True;Property;_Noise1Tex;Noise1Tex;3;0;Create;True;0;0;False;0;f687f6ae1348e7e47b50843dc0e70bff;f687f6ae1348e7e47b50843dc0e70bff;False;white;Auto;Texture2D;-1;0;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RangedFloatNode;10;-816.4614,-192.4872;Inherit;False;Property;_Noise1SpeedY;Noise1SpeedY;7;0;Create;True;0;0;False;0;0.02;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;15;548.9292,-400.1443;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode;11;122.3385,-448.4872;Inherit;True;Property;_TextureSample1;Texture Sample 1;5;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;14;121.3385,-233.4872;Inherit;True;Property;_TextureSample3;Texture Sample 3;5;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;60;-481.3114,-465.5931;Inherit;False;SingleDisturb;-1;;30;a8113882c53ba64408ab94202aae5c7d;0;7;33;FLOAT;0;False;30;FLOAT;0.1;False;29;FLOAT;0;False;13;FLOAT;0;False;3;SAMPLER2D;0;False;7;FLOAT;0;False;8;FLOAT;0;False;3;FLOAT4;17;FLOAT4;28;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;9;-817.4614,-274.4872;Inherit;False;Property;_Noise1SpeedX;Noise1SpeedX;5;0;Create;True;0;0;False;0;0.03;0.03;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;13;127.3385,-669.4872;Inherit;True;Property;_TextureSample2;Texture Sample 2;5;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;78;-1170.371,291.8235;Inherit;False;Property;_SinTimeScale;SinTimeScale;19;0;Create;True;0;0;False;0;0.2;0.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;55;-815.0146,-401.3531;Inherit;False;Property;_Noise1Intensity;Noise1Intensity;15;0;Create;True;0;0;False;0;0.01;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;31;1351.029,499.445;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;95;1492.744,497.6065;Float;False;True;-1;2;ASEMaterialInspector;0;6;Custom/ScreenDisturb;5056123faa0c79b47ab6ad7e8bf059a4;True;Default;0;0;Default;2;True;2;5;False;-1;10;False;-1;0;1;False;-1;0;False;-1;False;False;True;2;False;-1;True;True;True;True;True;0;True;-9;True;True;0;True;-5;255;True;-8;255;True;-7;0;True;-4;0;True;-6;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;2;False;-1;True;0;True;-11;False;True;5;Queue=Transparent=Queue=0;IgnoreProjector=True;RenderType=Transparent=RenderType;PreviewType=Plane;CanUseSpriteAtlas=True;False;0;False;False;False;False;False;False;False;False;False;False;True;2;0;;0;0;Standard;0;0;1;True;False;;0
WireConnection;77;0;5;0
WireConnection;77;1;3;0
WireConnection;4;0;77;0
WireConnection;6;0;92;0
WireConnection;6;1;4;0
WireConnection;82;0;80;0
WireConnection;82;1;92;0
WireConnection;79;0;6;0
WireConnection;79;1;82;0
WireConnection;50;0;12;0
WireConnection;7;0;79;0
WireConnection;59;33;56;0
WireConnection;59;30;36;0
WireConnection;59;29;26;0
WireConnection;59;13;7;0
WireConnection;59;3;25;0
WireConnection;59;7;24;0
WireConnection;59;8;23;0
WireConnection;90;29;7;0
WireConnection;90;18;63;0
WireConnection;90;19;64;0
WireConnection;90;21;50;0
WireConnection;90;20;12;0
WireConnection;22;0;12;0
WireConnection;22;1;59;17
WireConnection;20;0;12;0
WireConnection;20;1;59;28
WireConnection;19;0;12;0
WireConnection;19;1;59;0
WireConnection;66;0;90;0
WireConnection;29;0;66;1
WireConnection;29;1;19;2
WireConnection;29;2;27;1
WireConnection;28;0;66;0
WireConnection;28;1;20;1
WireConnection;28;2;27;1
WireConnection;30;0;66;2
WireConnection;30;1;22;3
WireConnection;30;2;27;1
WireConnection;15;0;13;1
WireConnection;15;1;11;2
WireConnection;15;2;14;3
WireConnection;11;0;12;0
WireConnection;11;1;60;0
WireConnection;14;0;12;0
WireConnection;14;1;60;17
WireConnection;60;33;55;0
WireConnection;60;30;37;0
WireConnection;60;29;16;0
WireConnection;60;13;7;0
WireConnection;60;3;8;0
WireConnection;60;7;9;0
WireConnection;60;8;10;0
WireConnection;13;0;12;0
WireConnection;13;1;60;28
WireConnection;31;0;28;0
WireConnection;31;1;29;0
WireConnection;31;2;30;0
WireConnection;31;3;50;4
WireConnection;95;0;31;0
ASEEND*/
//CHKSM=1C5E337D0FA20070E2EF0F4D1D39BC4EF5AF71F1