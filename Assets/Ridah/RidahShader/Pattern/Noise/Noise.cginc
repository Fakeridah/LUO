#include "../Ridah.hlsl"
//simple noise
void SimpleNoise_float(float2 uv ,float scale,float speed,bool move,out float4 fragColor)
{   uv*=scale;
    uv.x -= 1.5;
    if(move)speed *= _Time.y;
    float d = sin(pow(smoothstep(0.0, 0.4, abs(length(frac(uv * length(uv-2.0))-0.5)-0.3)), 0.8) * speed* 10.0);

    fragColor = float4(d, d, d, 1);
}


void SimpleNoise2_float(float2 uv ,float scale,float speed,out float4 fragColor)
{
    uv*=scale;
    float3 _color = float3(sin(uv.xy * 8000. * length(uv) + 4. * _Time.y*speed), 1.0);

    // Output to screen
    fragColor = float4(F3(_color.r), 1.0);
}


float noise(in float2 st)
{
    float2 i = floor(st);
    float2 f = frac(st);
    float a = random(i);
    float b = random(i + float2(1.0, 0.0));
    float c = random(i + float2(0.0, 1.0));
    float d = random(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
    
}
void SimpleNoise3_float(float2 uv ,float scale,float speedV,float speedH,out float4 fragColor)
{
    uv *= scale;
    uv.y+= _Time.y/5*speedV;
    float n = noise(uv + mix(20.f,16.f,sin(_Time.y*speedH)) * random(uv*.004));

    // Output to screen
    fragColor = F4(n);
}


float3 palette( in float t, in float3 a, in float3 b, in float3 c, in float3 d )
{
    float speed = 0.1;
    return a + b*cos( 6.28318*(c*(t*speed)+d) );
}
void SimpleNoise4_float(float2 uv ,float scale,float speed,float2 _offset,out float4 fragColor)
{
    uv-=_offset;
    uv*=scale;
    float timeSeed = 5000.;
    uv = fract(uv/3.)-0.5;
    float d = length(uv);
    speed *= iTime;
    float circle = 0.;
    circle = sin(d*9.0*(speed+timeSeed))/9.0;
    
    circle = abs(circle);
    circle = smoothstep(0.0,0.05,circle);

    float3 color = palette(4.5, F3(1),  F3(1),  F3(1),  F3(1));
    //float3 
    color *= 0.4/circle;
    color = clamp(color,0,1);
    fragColor =  float4(color, 1.0);

}


void TurnOffTV_float(float2 uv,float4 TO,float2 _Offset,float SCALE_SPEED,bool playOnce,float ttime,SamplerState _sampler,Texture2D iChannel0,out float4 fragColor)
{
    float4 FADE_COLOR = float4(0.0, 0.0, 0.0, 1.0f);
    float _BUFFER = 2.0f;

    float onePixelScale = (_ScreenParams.y - 1.0f) /_ScreenParams.y;
    
    float time = mod(iTime * 4.0f, 1.0f / SCALE_SPEED + 1.0f + _BUFFER * 2.0f);
    if(playOnce)
    {time = mod(ttime * 4.0f, 1.0f / SCALE_SPEED + 1.0f + _BUFFER * 2.0f);
    if((ttime) * 4.0f>1.0f / SCALE_SPEED + 1.0f + _BUFFER * 2.0f) {fragColor=0;return;}}
    time = clamp(time- _BUFFER, 0.0, 1.0f / SCALE_SPEED + 1.0f);
    
    float scaleTime = clamp(time * SCALE_SPEED, 0.0f, onePixelScale);
    float fadeTime = clamp(time - onePixelScale / SCALE_SPEED, 0.0f, 1.0f);
        
    float2 scaledUV = float2(
        (uv.x - 0.5f) * (1.0f - scaleTime) + 0.5f,
        (uv.y - 0.5f) / (1.0f - scaleTime) + 0.5f
    )*TO.xy+TO.zw;
    
    uv-=_Offset;
	float4 textureColor = SAMPLE_TEXTURE2D(iChannel0,_sampler, scaledUV) + float4(scaleTime, scaleTime, scaleTime, 0);
    float fadeOutLevel = 1.0f - fadeTime;
    float cropPixel = min(
        clamp(
            sign(
                abs(scaleTime / 2.0f - 0.5) 
                - abs(uv.y - 0.5f)
            )
            , 0.0f, 1.0f
        ), 
        clamp(
            sign(
                1.0f - fadeTime
                - abs(uv.x - 0.5f)
            ),
            0.0f, 
            1.0f
        )
    );
    
    fragColor = mix(
        FADE_COLOR, 
        mix(FADE_COLOR, textureColor, fadeOutLevel), 
        cropPixel
    );
}


float2 fade(float2 t){return t*t*t*(t*(t*6.-15.)+10.);}
float cnoise(float2 P,float seed){
    float4 _Pi=floor(P.xyxy)+float4(0.,0.,1.,1.);
    float4 Pf=fract(P.xyxy)-float4(0.,0.,1.,1.);
    _Pi=mod(_Pi,289.);// To avoid truncation effects in permutation
    float4 ix=_Pi.xzxz;
    float4 iy=_Pi.yyww;
    float4 fx=Pf.xzxz;
    float4 fy=Pf.yyww;
    float4 i=permute(permute(ix,seed)+iy,seed);
    float4 gx=2.*fract(i*.0243902439)-1.;// 1/41 = 0.024...
    float4 gy=abs(gx)-.5;
    float4 tx=floor(gx+.5);
    gx=gx-tx;
    float2 g00=float2(gx.x,gy.x);
    float2 g10=float2(gx.y,gy.y);
    float2 g01=float2(gx.z,gy.z);
    float2 g11=float2(gx.w,gy.w);
    float4 norm=1.79284291400159-.85373472095314*
    float4(dot(g00,g00),dot(g01,g01),dot(g10,g10),dot(g11,g11));
    g00*=norm.x;
    g01*=norm.y;
    g10*=norm.z;
    g11*=norm.w;
    float n00=dot(g00,float2(fx.x,fy.x));
    float n10=dot(g10,float2(fx.y,fy.y));
    float n01=dot(g01,float2(fx.z,fy.z));
    float n11=dot(g11,float2(fx.w,fy.w));
    float2 fade_xy=fade(Pf.xy);
    float2 n_x=mix(float2(n00,n01),float2(n10,n11),fade_xy.x);
    float n_xy=mix(n_x.x,n_x.y,fade_xy.y);
    return 2.3*n_xy;
}
float cdistance(float2 a,float2 b){
    return sqrt(pow(a.x-b.x,2.)+pow(a.y-b.y,2.));
}
void SimplePerl_float(float2 uv,float scale,float2 _offset,float speed,out float4 fragColor)
{
    uv*=scale;
    uv+=_offset;
    uv*=cnoise(uv,1.)+cnoise(uv+F2(0.6*iTime*speed),1.);
    
    float d=10.*cos(-0.1*iTime*speed+10.*cdistance(uv,F2(0.)));
    d=smoothstep(.5,.6,d);
    
    fragColor=float4(d,d,d,1.);
}


float noise( in float3 x ) { // in [0,1]
    float3 p = floor(x);
    float3 f = fract(x);

    f = f*f*(3.-2.*f);

    float n = p.x + p.y*57. + 113.*p.z;

    float res = mix(mix(mix( hash(n+  0.), hash(n+  1.),f.x),
                        mix( hash(n+ 57.), hash(n+ 58.),f.x),f.y),
                    mix(mix( hash(n+113.), hash(n+114.),f.x),
                        mix( hash(n+170.), hash(n+171.),f.x),f.y),f.x);
    return res;
}
float fbm(float x, float y) {
    float total = 0.0;
    float persistence = 0.5f;
    //int octaves = 8;
    float freq = 2.f;
    float amp = 0.5f;
    for(int i = 1; i <= 6; i++) {
        total += noise(float3(x * freq, y * freq, 1.0)) * amp;

        freq *= 2.f;
        amp *= persistence;
    }
    return total;
}
float sfbm(in float3 x) {
    return fbm(x.x, x.y) * 2.0 - 1.0;
}
float rippleNoise(float dist, float rippleIntensity, float rippleScale,float speed) {
    return rippleScale * (sin(dist * rippleIntensity - iTime * 10.0*speed) / (dist * rippleIntensity));
}
float3 maths(float2 uv) {
    float3 color = F3(0.0);
      for(float i = 0.0; i < 128.0; i+=1.0)
      {
        float phase = sin(i*546.13 + 1.0)*0.5 + 0.5;
        float size = pow( sin(i*651.74 + 5.0)*0.5 + 0.5, 4.0 );
        float pox =  sin(i*0.55 + 4.1) * _ScreenParams.x / _ScreenParams.y;
        float rad = size + sin(phase+size)/1.5;
        float2  pos = float2( pox + sin(iTime/20.+ phase + size), -1.0-rad + (2.0+3.0*rad)*mod(phase + 0.2*(iTime/8.) * (0.5+size),1.0));
        float dis = length(uv - pos);
        float3  col = mix( float3(0.194*sin(iTime/6.0)+0.3,0.2,0.3*phase), float3(1.1*sin(iTime/9.0)+0.3,0.2*phase,0.4), 0.5+0.5*sin(i));
        float f = length(uv-pos)/rad;
        f = sqrt(clamp(1.0 + (sin((iTime)*size)*0.5)*f,0.0,1.0));
        color += col.zyx *(1.0-smoothstep( rad*0.15, rad, dis ));
      }
      color *= sqrt(1.5-0.5*length(uv));
      return color;
}
float2 uvTransform(float2 uv, float2 uv0,float speed) {
    // === RIPPLES ===
	float2 rPos1 = float2(-0.5, -0.7);
	float2 rPos2 = float2(0.3, 0.7);
	float2 rPos3 = float2(0.5, -0.5);
	float2 rPos4 = float2(0.01, 0.01);
    float2 rPos5 = float2(0.8, -0.2);
	float rStr1 = 12.0;
	float rStr2 = 8.0;
	float rStr3 = 5.0;
	float rStr4 = 65.0;
    float rStr5 = 10.0;
	
	// === FINAL ===
	float rLength1 = length(uv + rPos1); // distance of current pixel from ripple center
	float rLength2 = length(uv + rPos2); // distance of current pixel from ripple center
	float rLength3 = length(uv + rPos3); // distance of current pixel from ripple center
	float rLength4 = length(uv + rPos4); // distance of current pixel from ripple center
    float rLength5 = length(uv + rPos5);
	float2 result = uv0
		+ (rPos1 / rLength1) * cos(rLength1 * rStr1 -iTime*speed * (rStr1 / 3.0)) * (rStr1 / 400.0)
		+ (rPos2 / rLength2) * cos(rLength2 * rStr2 -iTime*speed * (rStr2 / 3.0)) * (rStr2 / 400.0)
		+ (rPos3 / rLength3) * cos(rLength3 * rStr3 -iTime*speed * (rStr3 / 3.0)) * (rStr3 / 400.0)
		+ (rPos4 / rLength4) * cos(rLength4 * rStr4 -iTime*speed * (rStr4 / 3.0)) * (rStr4 / 400.0)
        + (rPos5 / rLength5) * cos(rLength5 * rStr5 -0.5 * iTime*speed * (rStr5 / 3.0)) * (rStr5 / 400.0);
    return result;
}
void SimpleRipple_float(float2 uv,float2 scale,float2 _offset,float speed,float2 screen,out float4 fragColor)
{
    float2 uv0 = float2(uv.x*screen.x,uv.y*screen.y) / iResolution.xy*scale;
    uv*=scale;
    // UV transform.
    uv = uvTransform(uv, uv0,speed);
   uv+=_offset;
    float3 col = F3(1.0);
    
    //col = maths(uv);
    
    for (float i = 0.0; i < 32.0; i+=1.0) {
        float d = 1.5 * sfbm(0.05 * F3(fbm(uv.x, 0.01*(cos(iTime*speed)) * uv.y)));
        d = abs(d);
        d = 0.1 / d;
        col *= pow(hash(0.5 * d), 0.01);
    }
    
    col *= sqrt(1.5 - 0.5*length(uv));
     
    // Apply a ripple shader.
    float intensity = 0.0;
    for (int x = 0; x < 2; x++) {
        for (int y = 0; y < 2; y++) {
            // Random place on screen space?
            float2 centre = float2(0.5 + float(x) * 0.25, 0.375 + float(y) * 0.25);
            float dist = distance(col.xy, centre);
            intensity += rippleNoise(dist, 42.  + 100. * float(x), 2.5 + 5. * float(y),speed);
        }
    }
    intensity *= 5.5;
    
    fragColor = clamp( float4(col.x * intensity, col.y * intensity, col.z * intensity,1.0),0,1);
}


float4 Noise3D(float3 x )
{
    // grid
    x *= 2.0;
    float3 p = floor(x);
    float3 w = fract(x);
    
    // quintic interpolant
    float3 u = w*w*w*(w*(w*6.0-15.0)+10.0);
    float3 du = 30.0*w*w*(w*(w-2.0)+1.0);
    
    // gradients
    float3 ga = hash( p+float3(0.0,0.0,0.0) );
    float3 gb = hash( p+float3(1.0,0.0,0.0) );
    float3 gc = hash( p+float3(0.0,1.0,0.0) );
    float3 gd = hash( p+float3(1.0,1.0,0.0) );
    float3 ge = hash( p+float3(0.0,0.0,1.0) );
    float3 gf = hash( p+float3(1.0,0.0,1.0) );
    float3 gg = hash( p+float3(0.0,1.0,1.0) );
    float3 gh = hash( p+float3(1.0,1.0,1.0) );
    
    // projections
    float va = dot( ga, w-float3(0.0,0.0,0.0) );
    float vb = dot( gb, w-float3(1.0,0.0,0.0) );
    float vc = dot( gc, w-float3(0.0,1.0,0.0) );
    float vd = dot( gd, w-float3(1.0,1.0,0.0) );
    float ve = dot( ge, w-float3(0.0,0.0,1.0) );
    float vf = dot( gf, w-float3(1.0,0.0,1.0) );
    float vg = dot( gg, w-float3(0.0,1.0,1.0) );
    float vh = dot( gh, w-float3(1.0,1.0,1.0) );
	
    // interpolation
    float v = va + 
              u.x*(vb-va) + 
              u.y*(vc-va) + 
              u.z*(ve-va) + 
              u.x*u.y*(va-vb-vc+vd) + 
              u.y*u.z*(va-vc-ve+vg) + 
              u.z*u.x*(va-vb-ve+vf) + 
              u.x*u.y*u.z*(-va+vb+vc-vd+ve-vf-vg+vh);
              
    float3 d = ga + 
             u.x*(gb-ga) + 
             u.y*(gc-ga) + 
             u.z*(ge-ga) + 
             u.x*u.y*(ga-gb-gc+gd) + 
             u.y*u.z*(ga-gc-ge+gg) + 
             u.z*u.x*(ga-gb-ge+gf) + 
             u.x*u.y*u.z*(-ga+gb+gc-gd+ge-gf-gg+gh) +   
             
             du * (float3(vb-va,vc-va,ve-va) + 
                   u.yzx*float3(va-vb-vc+vd,va-vc-ve+vg,va-vb-ve+vf) + 
                   u.zxy*float3(va-vb-ve+vf,va-vb-vc+vd,va-vc-ve+vg) + 
                   u.yzx*u.zxy*(-va+vb+vc-vd+ve-vf-vg+vh) );
                   
    return float4( v, d );                   
}
float4 hexagon( float2 p ) 
{
	float2 q = float2( p.x*2.0*0.5773503, p.y + p.x*0.5773503 );
	
	float2 pii = floor(q);
	float2 pf = fract(q);

	float v = mod(pii.x + pii.y, 3.0);

	float ca = step(1.0,v);
	float cb = step(2.0,v);
	float2  ma = step(pf.xy,pf.yx);
	
    // distance to borders
    float2 k = 1.0-pf.yx + ca*(pf.x+pf.y-1.0) + cb*(pf.yx-2.0*pf.xy);
	float e = dot( ma,  k);

	// distance to center	
	p = float2( q.x + floor(0.5+p.y/1.5), 4.0*p.y/3.0 )*0.5 + 0.5;
	float f = length( (fract(p) - 0.5)*float2(1.0,0.85) );		
	
	return float4((fract(p) - 0.5)*float2(1.0,0.85), e, f );
}
void SimpleHex_float(float2 uv,float2 scale,float speed,out float4 fragColor)
{
    float2 p = uv*scale;
    float3 rd = float3(0.0, 0.0, -1.0);
    
    float4 hex = hexagon(p*15.0);
    float di = (hex.w);
    
    float rod_x = hex.x;
    float rod_y = hex.y;
    float3 n = float3(0.0, 0.0, 1.0);
    if(di < 0.5)
    {
        float z = sqrt(1.0 - di*di);
        n = normalize(float3(rod_x, rod_y, -z));
        n = mix(float3(0.0, 0.0, -1.0), n, smoothstep(0.0, 0.3, hex.z));
    }
        
    float refrac = 0.2;
    float3 refracted_ray = mix(n, rd, refrac);
    float z_dist = 0.8 / (refracted_ray.z + 0.00001);
    float3 pos = float3(p, 0.0) + z_dist*refracted_ray;
    
    float g = 1.0 - abs(n.z);
    g = g * 0.5 / (g * 0.5 - g + 1.0);
    float glass = (1.0-1.2*g);

    float3 c3 = mix(F3(0.0), F3(1.0), smoothstep(-0.05, 0.0, Noise3D(float3(pos.xy, iTime*speed)).x));
    

    float3 col = glass - 1.0 + c3;
    fragColor = float4(col,1.0);
}


float2 random2d(float2 x) {
    return fract(sin(float2(dot(x,float2(127.1,311.7)),dot(x,float2(269.5,183.3))))*43758.5453);
}
float gradient_noise(float2 x) {
    float2 i = floor(x);
    float2 f = fract(x);
    float2 u = f*f*(3.0-2.0*f);

    return mix( mix( dot( random2d(i + float2(0.0,0.0) ), f - float2(0.0,0.0) ),
                     dot( random2d(i + float2(1.0,0.0) ), f - float2(1.0,0.0) ), u.x),
                mix( dot( random2d(i + float2(0.0,1.0) ), f - float2(0.0,1.0) ),
                     dot( random2d(i + float2(1.0,1.0) ), f - float2(1.0,1.0) ), u.x), u.y) * 1.5;// * 0.5 + 0.5;
}
void SimpleOscillate_float(float2 uv,float2 scale,float speed,out float4 fragColor){
    float3 _color = F3(0.0);
    uv*=scale;
    float zoom = 5.0;
    uv *= zoom;
    uv = mul(uv,rotate(gradient_noise(uv)));
    
    float2 uv_i = floor(uv);
    float2 uv_f = fract(uv);
    
    float m_dist = 1.0;
    for (int j = -1; j < 2; ++j){
        for (int i = -1; i < 2; ++i){
            float2 n = uv_i + float2(float(i), float(j));
            float2 pt = random2d(n);
            pt = 0.5 + 0.5*sin(iTime*speed + 6.0 * pt);
            float2 v = n + pt - uv;
            float d = length(v);
            m_dist = min(m_dist, d);
        }
    }
    
    _color = F3(m_dist + random(uv));
    //_color = float3(m_dist + random(float2(0.0)));
    
    fragColor = float4(_color, 1.0);
}



#define T (60.*iTime)
uint pcg_hash(uint n) {
    n = n * 747796405u + 2891336453u;
    n = ((n >> ((n >> 28) + 4u)) ^ n) * 277803737u;
    n ^= (n >> 22);
    return n;
}

float N1(uint x) {return float(pcg_hash(x)) / exp2(32.);}
float N2(float a, float b) {
    a = mod(a, exp2(15.));
    b = mod(b, exp2(15.));
    
    uint cord = (uint(a) << 16) + uint(b);
    
    //return N1(cord);
    
    uint corda = cord + (1u << 16);
    uint cordb = cord + 1u;
    uint cordab = corda + 1u;
    
    float noisea1 = mix(N1(cord), N1(corda), fract(a));
    float noisea2 = mix(N1(cordb), N1(cordab), fract(a));
    
    return mix(noisea1, noisea2, fract(b));
}
float sparkler(float r, float v) {
    r *= 50.; v *= 50.;
    
    float col = N2(r - T, v);
    return col;
    return pow(col, pow(r/4., 2.8));
}
float N3(float3 p) {
    uint3 up = uint3(p);
    uint hash = pcg_hash((up.x << 21u) + (up.y << 11u) + up.z);
    return float(hash) / exp2(32.);
}

float N3bilinear(float3 p) {
    float2
        angdif = float2(1,0),
        angfrac = fract(p.xy),
        angcmp = abs(p.xy - exp2(10.));
    
    if (angcmp.x > angcmp.y) {
        angdif = angdif.yx;
        angfrac = angfrac.yx;
    }
    
    return mix(
        mix(N3(p), N3(float3(p.xy, p.z+1.)), fract(p.z)),
        mix(N3(float3(p.xy+angdif, p.z+1.)), N3(float3(p.xy+angdif, p.z+1.)), fract(p.z)),
        angfrac.y
    );
}
float sparkler2(float2 uv) {
    float2 angle = 50.*normalize(uv) + exp2(10.);
    float dist = 50.*length(uv);
    
    float col = N3bilinear(float3(angle, mod(dist-T, exp2(21.))));
    return col;
    return pow(col, pow(dist/4., 2.8));
}
void TrigNoise_float(float2 uv,float2 Scale ,float scale,out float4 fragColor)
{
uv-=Scale;
uv*=scale;
    float col = 0.;
    
    float r = length(uv);
    float v = mod(atan2(uv.y, uv.x), TAU);
    
    
    //col = sparkler(r, v);
    col = sparkler2(uv);
    
    fragColor = float4(F3(col), 1.0);
}
