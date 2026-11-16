#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Ridah.hlsl"
struct Attributes
{
	uint vertexID : SV_VertexID;
};

struct Varyings
{
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
};

Varyings Vertex(Attributes input)
{
	Varyings output;
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	output.pos = GetFullScreenTriangleVertexPosition(input.vertexID);
	output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
	return output;
}

TEXTURE2D_X(_PostSource);
SAMPLER(sampler_PostSource);

float _Speed;
float _BlockSize;
float _MaxRGBSplitX;
float _MaxRGBSplitY;
float FRandom(uint seed)
{
	return GenerateHashedRandomFloat(seed);
}
float randomNoise(float2 seed)
{
	return frac(sin(dot(seed * floor(_Time.y * _Speed), float2(17.13, 3.71))) * 43758.5453123);
}
float randomNoise(float seed)
{
	return randomNoise(float2(seed, 1.0));
}
float4 SampleTexture(float2 uv)
{
	return SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, uv);
}
float4 Fragment(Varyings i) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	
	half2 block = randomNoise(floor(i.uv * _BlockSize));

	float displaceNoise = pow(block.x, 20);
	float splitRGBNoise = pow(randomNoise(7.2341), 17.0);
	float offsetX = displaceNoise - splitRGBNoise * _MaxRGBSplitX;
	float offsetY = displaceNoise - splitRGBNoise * _MaxRGBSplitY;

	float noiseX = 0.05 * randomNoise(13.0);
	float noiseY = 0.05 * randomNoise(7.0);
	float2 offset = float2(offsetX * noiseX, offsetY * noiseY);

	half3 colorR = SampleTexture(i.uv);
	half3 colorG = SampleTexture(i.uv + offset);
	half3 colorB = SampleTexture(i.uv - offset);

	return half4(colorR.r, colorG.g, colorB.b, 1);
	
}


uniform TEXTURE2D_X(_NoiseTex);
SAMPLER(sampler_NoiseTex);
float _rgbAMPLITUDE;
float _rgbSPEED;
float4 rgbShift( in float2 p , in float4 shift) {
    shift *= 2.0*shift.w - 1.0;
    float2 rs = float2(shift.x,-shift.y);
    float2 gs = float2(shift.y,-shift.z);
    float2 bs = float2(shift.z,-shift.x);
    
    float r = SampleTexture(p+rs).x;
    float g = SampleTexture(p+gs).y;
    float b = SampleTexture(p+bs).z;
    
    return float4(r,g,b,1.0);
}
float4 vec4pow( in float4 v, in float p ) {
    return float4(pow(v.x,p),pow(v.y,p),pow(v.z,p),v.w); 
}
float4 RGBFragment(Varyings i) : SV_Target
{
	float2 p = i.uv;
    float4 c = float4(0.0,0.0,0.0,1.0);
    float4 noise  = SAMPLE_TEXTURE2D_X(_NoiseTex, sampler_NoiseTex, float2(_rgbSPEED*iTime,2.0*_rgbSPEED*iTime/25.0 ));
    float4 shift = vec4pow(noise,8.0)
        		*float4(_rgbAMPLITUDE,_rgbAMPLITUDE,_rgbAMPLITUDE,1.0);;
    
    c += rgbShift(p, shift);
    
	return c;
}


float _mousePosX;
float _spScale;
float sat( float t ) {
	return clamp( t, 0.0, 1.0 );
}
float2 sat( float2 t ) {
	return clamp( t, 0.0, 1.0 );
}
float remap  ( float t, float a, float b ) {
	return sat( (t - a) / (b - a) );
}
float linterp( float t ) {
	return sat( 1.0 - abs( 2.0*t - 1.0 ) );
}
float3 spectrum_offset( float t ) {
    float t0 = 3.0 * t - 1.5;
	return clamp( float3( -t0, 1.0-abs(t0), t0), 0.0, 1.0);
}
float spRand( float2 n ) {
  return fract(sin(dot(n.xy, float2(12.9898, 78.233)))* 43758.5453);
}
float srand( float2 n ) {
	return spRand(n) * 2.0 - 1.0;
}
float mytrunc( float x, float num_levels )
{
	return floor(x*num_levels) / num_levels;
}
float2 mytrunc( float2 x, float num_levels )
{
	return floor(x*num_levels) / num_levels;
}
float4 SPFragment(Varyings i) : SV_Target
{
	float2 uv = i.uv;
	float4 fragColor=0; 
    uv.y = uv.y;
	
	float time = mod(iTime, 32.0); 

	float GLITCH = 0.1 + _mousePosX;
    
	float gnm = sat( GLITCH );
	float rnd0 = spRand( mytrunc( float2(time, time), 6.0 ) );
	float r0 = sat((1.0-gnm)*0.7 + rnd0);
	float rnd1 = spRand( float2(mytrunc( uv.x, 10.0*r0 ), time) ); //horz
	//float r1 = 1.0f - sat( (1.0f-gnm)*0.5f + rnd1 );
	float r1 = 0.5 - 0.5 * gnm + rnd1;
	r1 = 1.0 - max( 0.0, ((r1<1.0) ? r1 : 0.9999999) ); //note: weird ass bug on old drivers
	float rnd2 = spRand( float2(mytrunc( uv.y, 40.0*r1 ), time) ); //vert
	float r2 = sat( rnd2 );

	float rnd3 = spRand( float2(mytrunc( uv.y, 10.0*r0 ), time) );
	float r3 = (1.0-sat(rnd3+0.8)) - 0.1;

	float pxrnd = spRand( uv + time );

	float ofs = 0.05 * r2 * GLITCH * ( rnd0 > 0.5 ? 1.0 : -1.0 );
	ofs += 0.5 * pxrnd * ofs;

	uv.y += 0.1 * r3 * GLITCH;

    int NUM_SAMPLES = 10;
    float RCP_NUM_SAMPLES_F = 1.0 / float(NUM_SAMPLES);
    
	float4 sum = F4(0.0);
	float3 wsum = F3(0.0);
	for( int i=0; i<NUM_SAMPLES; ++i )
	{
		float t = float(i) * RCP_NUM_SAMPLES_F;
		uv.x = sat( uv.x + ofs * t*_spScale );
		float4 samplecol = SampleTexture(uv);
		float3 s = spectrum_offset( t );
		samplecol.rgb = samplecol.rgb * s;
		sum += samplecol;
		wsum += s;
	}
	sum.rgb /= wsum;
	sum.a *= RCP_NUM_SAMPLES_F;
	fragColor.a = sum.a;
	fragColor.rgb = sum.rgb;
	return fragColor;
}


float _cpGlitchAmount;
float4 posterize(float4 color, float numColors)
{
    return floor(color * numColors - 0.5) / numColors;
}
float2 quantize(float2 v, float steps)
{
    return floor(v * steps) / steps;
}
float dist(float2 a, float2 b)
{
    return sqrt(pow(b.x - a.x, 2.0) + pow(b.y - a.y, 2.0));
}
float4 CPFragment(Varyings i) : SV_Target
{   
	float2 uv = i.uv;
    float amount = pow(_cpGlitchAmount, 2.0);
    float2 pixel = 1.0 / iResolution.xy;    
    float4 color = SampleTexture(uv);
    float t = mod(mod(iTime, amount * 100.0 * (amount - 0.5)) * 109.0, 1.0);
    float4 postColor = posterize(color, 16.0);
    float4 a = posterize(SampleTexture(quantize(uv, 64.0 * t) + pixel * (postColor.rb - F2(.5)) * 100.0), 5.0).rbga;
    float4 b = posterize(SampleTexture(quantize(uv, 32.0 - t) + pixel * (postColor.rg - F2(.5)) * 1000.0), 4.0).gbra;
    float4 c = posterize(SampleTexture(quantize(uv, 16.0 + t) + pixel * (postColor.rg - F2(.5)) * 20.0), 16.0).bgra;
    return  mix(SampleTexture(uv + amount * (quantize((a * t - b + c - (t + t / 2.0) / 10.0).rg, 16.0) - F2(.5))
	* pixel * 100.0),(a + b + c) / 3.0,(0.5 - (dot(color, postColor) )) * amount);
}


void findRatio (in float2 res, out float2 rat) {
    float2 r = res;
    float gcf;
    for(float i = 1.; i <= r.x && i <= r.y; ++i)  
    {  
        if (mod(r.x,float(i)) == 0. && mod(r.y,float(i)) == 0.)  
            gcf = float(i);
    }
    r /= gcf;
    rat = r;
}
float2 hash(in float2 x) {
   float2 p = fract(sin(x.yx*3000.)*54321.);
   return fract(p);
}
float4 StretchFragment(Varyings i) : SV_Target
{
    float2 ires = iResolution.xy;
    float2 rRatio;
    findRatio(ires,rRatio);
    
    float2 uv =i.uv;
    float2 uv2 = fract(uv*rRatio*iResolution.x*16.);
    
    float2 rand = (hash(uv+iTime)-0.5)*0.2;
    rand.y /= 10.;
	rand.x/=_cpGlitchAmount;
    
    float3 col1 = SampleTexture(uv+rand/4.).rgb;
    
    float disp = dot(col1,float3(5.,5.,5.));
    uv2 /= (disp-0.5)*0.1+rand;
    uv2 += fract((disp-0.5)*10.);
    
    
    float3 col2 = SampleTexture(uv2).rgb*col1/12.;
    
    col1 -= col2;
    
    
    return float4(col1,1.0);
}


float2 curve(float2 uv)
{
	uv = (uv - 0.5) * 2.0;
	uv *= 1.1;	
	uv.x *= 1.0 + pow((abs(uv.y) / 5.0), 2.0);
	uv.y *= 1.0 + pow((abs(uv.x) / 4.0), 2.0);
	uv  = (uv / 2.0) + 0.5;
	uv =  uv *0.92 + 0.04;
	return uv;
}
float4 LineFragment(Varyings i) : SV_Target
{
    //Curve
    float2 uv = i.uv;
	uv = lerp(uv, curve( uv ),(_cpGlitchAmount-.01)/4.99);
    
    float3 col;

    // Chromatic
    col.r = SampleTexture(float2(uv.x+0.003,uv.y)).x;
    col.g = SampleTexture(float2(uv.x+0.000,uv.y)).y;
    col.b = SampleTexture(float2(uv.x-0.003,uv.y)).z;

    col *= step(0.0, uv.x) * step(0.0, uv.y);
    col *= 1.0 - step(1.0, uv.x) * 1.0 - step(1.0, uv.y);

    col *= 0.5 + 0.5*16.0*uv.x*uv.y*(1.0-uv.x)*(1.0-uv.y);
    col *= float3(0.95,1.05,0.95);

    col *= 0.9+0.1*sin(10.0*iTime+uv.y*700.0);

    col *= 0.99+0.01*sin(110.0*iTime);

   return float4(col,1.0);
}


float2 oghash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(166.1031, 147.1030, 142.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}
float simplexNoise(float2 uv)
{
    const float k1 = 0.366025f;
    const float k2 = 0.211324f;

    int2 idx = floor(uv + (uv.x + uv.y) * k1);
    float2 a = uv - (float2(idx) - float(idx.x + idx.y) * k2);
    int2 tb = a.y > a.x ? int2(0, 1) : int2(1, 0);
    float2 b = a - tb + k2;
    float2 c = a - 1.f + k2 * 2.f;
    
    float3 kernel = max(0.5f - float3(dot(a, a), dot(b, b), dot(c, c)), 0.f);
    float3 noise = kernel * kernel * kernel * kernel * 
    float3(dot(a, oghash(idx)*2.f-1.), 
           dot(b, oghash(idx + tb)*2.-1.), 
           dot(c, oghash(idx + 1.f)*2.-1.));
    
    return dot(F3(70.f), noise);
}
float verticalLine(float2 uv, float time)
{
    float uvX = uv.x + time*0.0000003;
    float2 xHash = oghash(float2(uvX,uvX));
    float vertical = step(0.9999996,sin(uvX*1000.0 * (xHash.x*0.01+0.01)));
    
    float uvY = uv.y + time*0.000001;
    float2 yHash = oghash(float2(uvY,uvY));
    vertical *= sin(uvY*1000.0 * (yHash.x));   
    
    return saturate(1.0 - vertical);
}
float4 OldSchoolFragment(Varyings i) : SV_Target
{
    float2 uv = i.uv;
    float t = iTime;
    
    float3 col = float3(0.0,0.0,0.0);
    col += verticalLine(uv-1.0,t)*0.5;
    col += (1.0 - verticalLine(uv-3.0,t*5.0))*0.5;
    col *= smoothstep(0.9,0.83, simplexNoise((uv + oghash(float2(t,t))*154.4541-154.4541)*10.0));
    col *= 1.f - oghash(uv + t * 0.01f).x * 0.25;
    col *= smoothstep(1.5 + oghash(floor(float2(t,t)*15.)).x*0.3,0.0, length(uv-0.5));
   
    float3 tex = SampleTexture(uv).rgb;
    tex = F3( (tex.r + tex.g + tex.b)/3.0 );
    
    return float4(col * tex,1.0);
}