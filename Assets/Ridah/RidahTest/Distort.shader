Shader "Unlit/Distort"
{

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
                Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
float mod(float n, float d){float a =n-d*int(n/d); return a <0?a+d:a;}
float2 mod(float2 n, float2 d){float2 a =n-d*int2(n/d); return a <0?a+d:a;}
float3 mod(float3 n, float3 d){float3 a =n-d*int3(n/d); return a <0?a+d:a;}
float4 mod(float4 n, float4 d){float4 a =n-d*int4(n/d); return a <0?a+d:a;}

 float snoise(in float2 v);
            
 float3 permute(in  float3 x) { return mod( x*x*34.+x, 289.); }
 float snoise(in  float2 v) {
   float2 i = floor((v.x+v.y)*.36602540378443 + v),
      x0 = (i.x+i.y)*.211324865405187 + v - i;
   float s = step(x0.x,x0.y);
   float2 j = float2(1.0-s,s),
      x1 = x0 - j + .211324865405187, 
      x3 = x0 - .577350269189626; 
  i = mod(i,289.);
   float3 p = permute( permute( i.y + float3(0, j.y, 1 ))+ i.x + float3(0, j.x, 1 )   ),
       m = max( .5 - float3(dot(x0,x0), dot(x1,x1), dot(x3,x3)), 0.),
       x = frac(p * .024390243902439) * 2. - 1.,
       h = abs(x) - .5,
      a0 = x - floor(x + .5);
  return .5 + 65. * dot( pow(m,float3(4.,4.,4.))*(- 0.85373472095314*( a0*a0 + h*h )+1.79284291400159 ), a0 * float3(x0.x,x1.x,x3.x) + h * float3(x0.y,x1.y,x3.y));
}
            fixed4 frag (v2f i) : SV_Target
            {
                
                float2 uv = i.uv;
    uv /= 1.1;
    uv += .05;
    uv *= 1.;
	float t = _Time.y*.8;
	float s = smoothstep(.5,1.,uv.x);
    uv.y += s * sin(t+uv.x * 5.) * .05;// * snoise(uv*2.+1.+t*.3);
    uv.x += s * snoise(uv*(4.3*(s/3.7+1.2))-float2(t*1.2,0.));

    
                // sample the texture
                fixed4 col = tex2D(_MainTex, uv);
                return float4(col.rgb,col.a);
            }
            ENDCG
        }
    }
}
