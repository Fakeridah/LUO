#ifdef UNITY_COLORSPACE_GAMMA
#define unity_ColorSpaceGrey fixed4(0.5, 0.5, 0.5, 0.5)
#define unity_ColorSpaceDouble fixed4(2.0, 2.0, 2.0, 2.0)
#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)
#define unity_ColorSpaceLuminance half4(0.22, 0.707, 0.071, 0.0) // Legacy: alpha is set to 0.0 to specify gamma mode
#else // Linear values
#define unity_ColorSpaceGrey fixed4(0.214041144, 0.214041144, 0.214041144, 0.5)
#define unity_ColorSpaceDouble fixed4(4.59479380, 4.59479380, 4.59479380, 2.0)
#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)
#define unity_ColorSpaceLuminance half4(0.0396819152, 0.458021790, 0.00609653955, 1.0) // Legacy: alpha is set to 1.0 to specify linear mode
#endif
float4 _CameraDepthTexture_ST;
TEXTURE2D_X_FLOAT(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
TEXTURE2D_X_FLOAT(_CameraNormalsTexture);
SAMPLER(sampler_CameraNormalsTexture);

float SampleSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv)).r;
}
float LoadSceneDepth(uint2 uv)
{
    return LOAD_TEXTURE2D_X(_CameraDepthTexture, uv).r;
}
float3 SampleSceneNormals(float2 uv)
{
    return UnpackNormalOctRectEncode(SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, UnityStereoTransformScreenSpaceTex(uv)).xy);
}
float3 LoadSceneNormals(uint2 uv)
{
    return UnpackNormalOctRectEncode(LOAD_TEXTURE2D_X(_CameraNormalsTexture, uv).xy);
}


float2 MultiplyUV (float4x4 mat, float2 inUV) {
    float4 temp = float4 (inUV.x, inUV.y, 0, 0);
    temp = mul (mat, temp);
    return temp.xy;
}
inline float2 UnityStereoScreenSpaceUVAdjustInternal(float2 uv, float4 scaleAndOffset)
{
    return uv.xy * scaleAndOffset.xy + scaleAndOffset.zw;
}
inline float4 UnityStereoScreenSpaceUVAdjustInternal(float4 uv, float4 scaleAndOffset)
{
    return float4(UnityStereoScreenSpaceUVAdjustInternal(uv.xy, scaleAndOffset), UnityStereoScreenSpaceUVAdjustInternal(uv.zw, scaleAndOffset));
}
inline half Luminance(half3 rgb)
{
    return dot(rgb, unity_ColorSpaceLuminance.rgb);
}

inline float3 DecodeViewNormalStereo( float3 enc4 )
{
    float kScale = 1.7777;
    float3 nn = enc4.xyz*float3(2*kScale,2*kScale,0) + float3(-kScale,-kScale,1);
    float g = 2.0 / dot(nn.xyz,nn.xyz);
    float3 n;
    n.xy = g*nn.xy;
    n.z = g-1;
    return n;
}
inline float DecodeFloatRG( float2 enc )
{
    float2 kDecodeDot = float2(1.0, 1/255.0);
    return dot( enc, kDecodeDot );
}
inline void DecodeDepthNormal( float4 enc, out float depth, out float3 normal )
{
    depth = DecodeFloatRG (enc.zw);
    normal = DecodeViewNormalStereo (enc);
}
#define UnityStereoScreenSpaceUVAdjust(x, y) UnityStereoScreenSpaceUVAdjustInternal(x, y)


//shadertoy
#define iTime _Time.y
#define iResolution _ScreenParams
#define TAU 6.28318530718
float random(in float2 st)
{
    return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
}
uint uhash(int3 f) 
{
    uint3  x = uint3(f);
    uint   k = 1103515245U; 
           x = ((x>>8U)^x.yzx)*k;
           x = ((x>>8U)^x.yzx)*k;  
    return uint3(((x>>8U)^x.yzx)*k);
}
float hash( float n ) {return frac(sin(n)*7586.5453);}
float3 hash( float3 p )
{
	p = float3( dot(p,float3(127.1,311.7, 74.7)),
			  dot(p,float3(269.5,183.3,246.1)),
			  dot(p,float3(113.5,271.9,124.6)));

	return -1.0 + 2.0*frac(sin(p)*43758.5453123);
}
float3 barycentric(float2 p)
{
    float s = sqrt(3.0);
    return float3((float2(-1,+1) * p.x - p.y/s + 1.0)/2.0, p.y / s);
}
float remap(float x, float min1,float min2, float max1, float max2){
    float remapped = min2 + (x - min1) * (max2 - min2) / (max1 - min1);
    return remapped;
}
float mod(float n, float d){float a =n-d*int(n/d); return a <0?a+d:a;}
float2 mod(float2 n, float2 d){float2 a =n-d*int2(n/d); return a <0?a+d:a;}
float3 mod(float3 n, float3 d){float3 a =n-d*int3(n/d); return a <0?a+d:a;}
float4 mod(float4 n, float4 d){float4 a =n-d*int4(n/d); return a <0?a+d:a;}
float4 permute(float4 x){return mod(((x*34.0)+1.0)*x, 289.0);}
float4 permute(float4 x,float seed){return mod(((x*34.)+1.+seed)*x,289.);}
float  F1(float x) {return float(x);}
float2 F2(float x) {return float2(x,x);}
float3 F3(float x) {return float3(x,x,x);}
float4 F4(float x) {return float4(x,x,x,x);}
float2 vec2(float2 x) {return x;}
float2 vec2(float x,float y) {return float2(x,y);}
float3 vec3(float3 x) {return x;}
float2 vec2(float x,float y,float z) {return float3(x,y,z);}
float4 vec4(float4 x) {return x;}
float4 vec4(float3 x,float y) {return float4(x,y);}
float4 vec4(float x,float y,float z,float w) {return float4(x,y,z,w);}
float  fract(float x) {return frac(x);}
float2 fract(float2 x){return frac(x);}
float3 fract(float3 x){return frac(x);}
float4 fract(float4 x){return frac(x);}
float mix(float x, float y, float a)     {return x*(1-a)+y*a;}
float2 mix(float2 x, float2 y, float2 a) {return x*(1-a)+y*a;}
float3 mix(float3 x, float3 y, float3 a) {return x*(1-a)+y*a;}
float4 mix(float4 x, float4 y, float4 a) {return x*(1-a)+y*a;}
float2x2 rotate(float angle)
{
    return float2x2( cos(angle),-sin(angle),sin(angle),cos(angle) );
}