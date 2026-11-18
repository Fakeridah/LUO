//shader graph
inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
{
    float angle = dot(uv, float2(12.9898, 78.233));
    #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
        // 'sin()' has bad precision on Mali GPUs for inputs > 10000
        angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
    #endif
    return frac(sin(angle)*43758.5453);
}
inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
{
    return (1.0-t)*a + (t*b);
}
inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);

    uv = abs(frac(uv) - 0.5);
    float2 c0 = i + float2(0.0, 0.0);
    float2 c1 = i + float2(1.0, 0.0);
    float2 c2 = i + float2(0.0, 1.0);
    float2 c3 = i + float2(1.0, 1.0);
    float r0 = Unity_SimpleNoise_RandomValue_float(c0);
    float r1 = Unity_SimpleNoise_RandomValue_float(c1);
    float r2 = Unity_SimpleNoise_RandomValue_float(c2);
    float r3 = Unity_SimpleNoise_RandomValue_float(c3);

    float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
    float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
    float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
    return t;
}
void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
{
    float t = 0.0;

    float freq = pow(2.0, float(0));
    float amp = pow(0.5, float(3-0));
    t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

    freq = pow(2.0, float(1));
    amp = pow(0.5, float(3-1));
    t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

    freq = pow(2.0, float(2));
    amp = pow(0.5, float(3-2));
    t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

    Out = t;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}
void Unity_Rectangle_float(float2 UV, float Width, float Height, out float Out)
{
    float2 d = abs(UV * 2 - 1) - float2(Width, Height);
    d = 1 - d / fwidth(d);
    Out = saturate(min(d.x, d.y));
}

void Unity_Rotate_Radians_float(float2 UV, float2 Center, float Rotation, out float2 Out)
{
    UV -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;
    UV.xy = mul(UV.xy, rMatrix);
    UV += Center;
    Out = UV;
}
void Unity_Rotate_Degrees_float(float2 UV, float2 Center, float Rotation, out float2 Out)
{
    Rotation = Rotation * (3.1415926f/180.0f);
    UV -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;
    UV.xy = mul(UV.xy, rMatrix);
    UV += Center;
    Out = UV;
}
void Unity_PolarCoordinates_float(float2 UV, float2 Center, float RadialScale, float LengthScale, out float2 Out)
{
    float2 delta = UV - Center;
    float radius = length(delta) * 2 * RadialScale;
    float angle = atan2(delta.x, delta.y) * 1.0/6.28 * LengthScale;
    Out = float2(radius, angle);
}

//shadertoy
#define iTime _Time.y
#define iResolution _ScreenParams
//#define HALF_PI 1.57079632675
//#define TWO_PI 6.283185307
#define TAU 6.28318530718
#define Tau 6.28318530718
#define tau 6.28318530718
#define PI 3.1415926535
#define Pi 3.1415926535
#define pi 3.1415926535
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