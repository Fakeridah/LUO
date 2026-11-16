Shader "Unlit/Glitch"
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

 float random(float2 p)
    {
        float t = floor(_Time.y * 20.) / 10.;
        return frac(sin(dot(p, float2(t * 12.9898, t * 78.233))) * 43758.5453);
    }

    float noise(float2 uv, float blockiness)
    {
        float2 lv = frac(uv);
        float2 id = floor(uv);

        float n1 = random(id);
        float n2 = random(id+float2(1,0));
        float n3 = random(id+float2(0,1));
        float n4 = random(id+float2(1,1));

        float2 u = smoothstep(0.0, 1.0 + blockiness, lv);

        return lerp(lerp(n1, n2, u.x), lerp(n3, n4, u.x), u.y);
    }

    float fbm(float2 uv, int count, float blockiness, float complexity)
    {
        float val = 0.0;
        float amp = 0.5;

        for(int i = count;i>0;i--)
        {
            
          val += amp * noise(uv, blockiness);
            amp *= 0.5;
            uv *= complexity;
            }
        return val;
    }

    const float glitchAmplitude = 0.2; // increase this
    const float glitchNarrowness = 4.0;
    const float glitchBlockiness = 2.0;
    const float glitchMinimizer = 10.0; // decrease this

            fixed4 frag (v2f i) : SV_Target
            {
                
                float2 uv = i.uv;
    //float2 uv = fragCoord/iResolution.xy;
        float2 a = float2(uv.x * (_ScreenParams.x / _ScreenParams.y), uv.y);
        float2 uv2 = float2(a.x / _ScreenParams.x, exp(a.y));
        float2 id = floor(uv * 8.0);
        //id.x /= floor(texture(iChannel0, float2(id / 8.0)).r * 8.0);

        // Generate shift amplitude
        float shift = .2 * pow(fbm(uv2, int(random(id) * 6.), 2., 4.), 10.);

        // Create a scanline effect
        float scanline = abs(cos(uv.y * 400.));
        scanline = smoothstep(0.0, 2.0, scanline);
        shift = smoothstep(0.00001, 0.2, shift);

        // Apply glitch
        float randShift = random(id) * shift;
        float colR = tex2D(_MainTex, float2(uv.x - shift, uv.y)).r * (1. - shift) + randShift;
        float colG = tex2D(_MainTex, float2(uv.x - shift, uv.y)).g * (1. - shift) + randShift;
        float colB = tex2D(_MainTex, float2(uv.x - shift, uv.y)).b * (1. - shift) + randShift;
        // Mix with the scanline effect
        // float3 f = float3(colR, colG, colB);
        float3 f = float3(colR, colG, colB);// - (0.1 * scanline)
                // sample the texture
                fixed4 col = tex2D(_MainTex, uv);
                return float4(f,1);
            }
            ENDCG
        }
    }
}
