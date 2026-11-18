#include "../Ridah.hlsl"


void CircleGlow_float(float2 uv ,float scale,out float4 fragColor)
{	
	uv-=F2(.5f);
	uv*=scale;
    float a = atan2(uv.x,uv.y);
    float r = length(uv)*0.75;
    uv = float2(a/tau,r);
	
	//get the _color
	float xCol = (uv.x - (iTime / 3.0)) * 3.0;
	xCol = mod(xCol, 3.0);
	float3 horColour = float3(0.25, 0.25, 0.25);
	
	if (xCol < 1.0) {
		
		horColour.r += 1.0 - xCol;
		horColour.g += xCol;
	}
	else if (xCol < 2.0) {
		
		xCol -= 1.0;
		horColour.g += 1.0 - xCol;
		horColour.b += xCol;
	}
	else {
		
		xCol -= 2.0;
		horColour.b += 1.0 - xCol;
		horColour.r += xCol;
	}

	// draw _color beam
	uv = (2.0 * uv) - 1.0;
	float beamWidth = (0.7+0.5*cos(uv.x*10.0*tau*0.15*clamp(floor(5.0 + 10.0*cos(iTime)), 0.0, 10.0))) * abs(1.0 / (30.0 * uv.y));
	float3 horBeam = F3(beamWidth);
	fragColor = float4((( horBeam) * horColour), 1.0);
}


float3 hsv2rgb(float3 _color){
    float4 konvert = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 calc = abs(fract(_color.xxx + konvert.xyz) * 6.0 - konvert.www);
    return _color.z * mix(konvert.xxx, clamp(calc - konvert.xxx, 0.0, 1.0), _color.y);
}
float luma(float3 _color) {
  //return dot(_color, float3(0.299, 0.587, 0.114));
  return dot(_color, float3(0.199, 0.187, 0.214));
}
void CircleGlow1_float(float2 uv ,float scale,out float4 fragColor)
{	
	uv-=F2(.5f);
	uv*=scale;
    float3 _color = float3(0.0,0.0,0.0);
	
	float distance = length(uv);
	float ring = 1.0 / abs(distance - 0.5);
	
	float angle = atan2(uv.x, uv.y);
	_color += hsv2rgb( float3( ( angle + iTime * 2.5 ) / (PI * 2.0), 1.0, 1.0 ) ) * ring * 0.15;
	
	
    _color += max(luma(_color) - 1.0, 0.0);
    
    fragColor = float4(_color, 1.0);
}


float3 hash33(float3 p3)
{
	p3 = fract(p3 * float3(.1031,.11369,.13787));
    p3 += dot(p3, p3.yxz+19.19);
    return -1.0 + 2.0 * fract(float3((p3.x + p3.y)*p3.z, (p3.x+p3.z)*p3.y, (p3.y+p3.z)*p3.x));
}
float simplex_noise(float3 p)
{
    const float K1 = 0.333333333;
    const float K2 = 0.166666667;
    
    float3 i = floor(p + (p.x + p.y + p.z) * K1);
    float3 d0 = p - (i - (i.x + i.y + i.z) * K2);
        
    float3 e = step(F3(0.0), d0 - d0.yzx);
	float3 i1 = e * (1.0 - e.zxy);
	float3 i2 = 1.0 - e.zxy * (1.0 - e);
    
    float3 d1 = d0 - (i1 - 1.0 * K2);
    float3 d2 = d0 - (i2 - 2.0 * K2);
    float3 d3 = d0 - (1.0 - 3.0 * K2);
    
    float4 h = max(0.6 - float4(dot(d0, d0), dot(d1, d1), dot(d2, d2), dot(d3, d3)), 0.0);
    float4 n = h * h * h * h * float4(dot(d0, hash33(i)), dot(d1, hash33(i + i1)), dot(d2, hash33(i + i2)), dot(d3, hash33(i + 1.0)));
    
    return dot(F4(31.316), n);
}
void CircleGlow2_float(float2 uv ,float scale,out float4 fragColor)
{       
    uv-=.5;
    uv*=scale;
    float a = sin(atan2(uv.y, uv.x));
    float am = abs(a-.5)/4.;
    float l = length(uv);                         
    
    float m1 = clamp(.1/smoothstep(.0, 1.75, l), 0., 1.);
    float m2 = clamp(.1/smoothstep(.42, 0., l), 0., 1.);
    float s1 = (simplex_noise(float3(uv*2., 1. + iTime*.525))*(max(1.0 - l*1.75, 0.)) + .9);
    float s2 = (simplex_noise(float3(uv*1., 15. + iTime*.525))*(max(.0 + l*1., .025)) + 1.25);
    float s3 = (simplex_noise(float3(float2(am, am*100. + iTime*3.)*.15, 30. + iTime*.525))*(max(.0 + l*1., .25)) + 1.5);
    s3 *= smoothstep(0.0, .3345, l);    
    
    float sh = smoothstep(0.15, .35, l);
    
    
    float m = m1*m1*m2 * ((s1*s2*s3) * (1.-l)) * sh;
    //m = clamp(m, 0., 1.);
    
    float3 col = mix(float3(16,21,25)/255., (0.5 + 0.5*cos(iTime+uv.xyx*3.+float3(0,2,4))), m);
            
    fragColor = float4(col, 1.);
}


float variation(float2 v1, float2 v2, float strength, float speed) {
	return sin(
        dot(normalize(v1), normalize(v2)) * strength + iTime * speed
    ) / 100.;
}
float3 paintCircle (float2 uv, float2 center, float rad, float width, float index) {
    float2 diff = center-uv;
    float len = length(diff);
    float scale = rad;
	float mult = mod(index, 2.) == 0. ? 1. : -1.; 
    len += variation(diff, float2(rad*mult, 1.0), 7.0*scale, 2.0);
    len -= variation(diff, float2(1.0, rad*mult), 7.0*scale, 2.0);
    float circle = smoothstep((rad-width)*scale, (rad)*scale, len) - smoothstep((rad)*scale, (rad+width)*scale, len);
    return F3(circle);
}
float3 paintRing(float2 uv, float2 center, float radius, float index){
     //paint _color circle
    float3 _color = paintCircle(uv, center, radius, 0.075, index);
    //this is where the blue _color is applied - change for different mood
    _color *= float3(0.3,0.85,1.0);
    //paint white circle
    _color += paintCircle(uv, center, radius, 0.015, index);
    return _color;
}
void CircleGlow3_float(float2 uv ,float scale,float2 _offset,out float4 fragColor)
{   
    uv-=_offset;
    uv*=scale;
    float numRings = 20.;
    float2 center = F2(0.5);
    float spacing = 1. / numRings;
    float slow = 30.;
    float cycleDur = 1.;
    float tunnelElongation = .25;
    float radius = mod(iTime/slow, cycleDur);
    float3 _color;

    //this provides the smooth fade black border, which we will mix in later
    float border = 0.25;
    float2 bl = smoothstep(0., border, uv); // bottom left
    float2 tr = smoothstep(0., border, 1.-uv); // top right
    float edges = bl.x * bl.y * tr.x * tr.y;

    //push in the left and right sides to make the warp square
    uv.x *= 1.5;
    uv.x -= 0.25; 
    
    //do the work
    for(float i=0.; i<numRings; i++){
   		_color += paintRing(uv, center, tunnelElongation*log(mod(radius + i * spacing, cycleDur)), i ); //these are the fast circles
        _color += paintRing(uv, center, log(mod(radius + i * spacing, cycleDur)), i); //these are essentially the same but move at a slower pace
    }

    //combined, these create a black fade around the edges of our screen
    _color = mix(_color, F3(0.), 1.-edges); 
    _color = mix(_color, F3(0.), distance(uv, center));
    //boom!
	fragColor = float4(_color, 1.0);
}


float rgba_noise(float2 p)
{
    p -= floor(p / 289.0) * 289.0;
    p += float2(223.35734, 550.56781);
    p *= p;
    
    float xy = p.x * p.y;
    
    return float4(fract(xy * 0.00000012),
                     fract(xy * 0.00000543),
                     fract(xy * 0.00000192),
                     fract(xy * 0.00000423)).x;
}
float noise(float2 p,SamplerState _sampler,Texture2D iChannel0) {
    
    return SAMPLE_TEXTURE2D(iChannel0,_sampler, p*.05).x;
}
float fbm(float2 p,SamplerState _sampler,Texture2D iChannel0) {
    float a =1.;
    float f = 1.;
    return a*noise(p,_sampler,iChannel0 ) 
        + a*0.5 * noise(p*f*2. ,_sampler,iChannel0 ) 
        + a*0.25 * noise(p*f*4.,_sampler,iChannel0 )
        + a*0.1 * noise(p*f*8. ,_sampler,iChannel0 );
}
float circle(float2 p) {
    float r = length(p);
    float radius = 0.4;
    float height = 1.; 
    float width = 150.;
    
	return height - pow(r - radius, 2.) *width ;
}
void CircleGlow4_float(float2 uv ,float scale,SamplerState _sampler,Texture2D iChannel0,out float4 fragColor)
{
    uv-=.5;
    uv*=scale;
    
    float2 st  = float2(
            atan2(uv.y, uv.x) ,
            length(uv) * 1. + iTime * 0.1
        );

    
    st.x += st.y * 1.1;// - iTime * 0.3;
    st.x = mod(st.x , 2*PI);
    
    
    float n = fbm(st,_sampler,iChannel0 ) * 1.5 -1. ;
    n = max(n, 0.1);
    float _circle =  max(1.- circle(uv ), 0.)  ;
    
    float _color = n/_circle;
    float mask = smoothstep(0.48, 0.4, length(uv));
    
    _color *= mask;
    float3 rez = float3(1., 0.5, 0.25) * _color;
    // Output to screen
    fragColor = float4(rez,1.0);
}


float2 random2_5(float2 st)
{
    st = float2( dot(st,float2(127.1,311.7)),
              dot(st,float2(29.5,183.3)) );
    return -1.0 + 2.0*fract(sin(st)*43758.5453123);
}
float random_5(float2 st)
{
    return fract(sin(dot(st.yx,float2(14.7891,43.123)))*312991.41235);
}
float random_5 (in float x)
{
    return fract(sin(x)*43758.5453123);
}
float v_noise(float2 st) {
    float2 i = floor(st);
    float2 f = fract(st);

    float2 u = f*f*(3.0-2.0*f);

    return mix( mix( dot( random2_5(i + float2(0.0,0.0) ), f - float2(0.0,0.0) ),
                     dot( random2_5(i + float2(1.0,0.0) ), f - float2(1.0,0.0) ), u.x),
                mix( dot( random2_5(i + float2(0.0,1.0) ), f - float2(0.0,1.0) ),
                     dot( random2_5(i + float2(1.0,1.0) ), f - float2(1.0,1.0) ), u.x), u.y);
}
float2 ratio(float2 st)
{
    return  float2(
            max(st.x/st.y,1.0),
            max(st.y/st.x,1.0)
            );
}
float3 time()
{
    float period = mod(iTime,6);
    float3 t = float3(fract(iTime/6),period, 1.0-fract(period));
    return t;       // return fract(length),period,period phase
}
float scene(float2 st, float3 t)
{
    st = st * 2.0 - 1.0;

    float seed = 29192.173;
    float center = length(st-0.5) - 0.5;

    float n_scale = 0.12;

    float n_1 = v_noise(st + sin(PI*t.x)) * n_scale;
    float n_2 = v_noise(st+seed - sin(PI*t.x)) * n_scale;

    float d = 1.0;
    for(int i = 1; i <= 8; i++)
    {
        float spread = 1.0 / float(i);
        float speed = ceil(3.0*spread);
        float r = random_5(float(i)*5.0 + seed);
        float r_scalar = r * 2.0 - 1.0;

        float2 pos = st - F2(0.0);
            pos +=  mul(F2(0.01) , rotate(TWO_PI * r_scalar + TWO_PI * t.x * speed * sign(r_scalar)));
            pos = mul(pos,rotate(TWO_PI * r_scalar + TWO_PI * t.x * speed * sign(r_scalar) + v_noise(pos + float(i) + iTime)));
            pos += mix(n_1,n_2,0.5+0.5*sin(TWO_PI*t.x*speed));

        float s = .45 + .126 * r;

        float a = atan2(pos.y,pos.x)/PI;
            a = abs(a);
            a = smoothstep(0.0,1.0,a);

        float c = length(pos);
            c = abs(c-s);
            c -= 0.0004 + .0125 * a;

        d = min(d,c);
    }

    return d;
}
void CircleGlow5_float(float2 uv ,float scale,float2 _offset,out float4 fragColor)
{
    // timing
    float3 t = time();
    uv-=_offset; 
    uv*=scale;
    float2 st = uv;

    st = st * 2.0 - 1.0;
    st = st * (1.0 + .03 * sin(TWO_PI*t.x));
    st = st * 0.5 + 0.5;

    // scene
    float s = scene(st, t);

    // aa
    float pixelSmoothing = 2.0;
    float aa = ratio(iResolution.xy).x/iResolution.x*pixelSmoothing;
    
    // _color
    float3 _color = F3(0.08);
        _color = mix(_color,F3(1.0),1.0-smoothstep(-aa,aa,s));
        _color = 1.0 - _color;

    // vignette
    float size = length(st-.5)-2.33;
    float vignette = (size) * 0.75 + random_5(st) *.08;        
    _color = mix(_color,float3(0.0, 0.0, 0.0),vignette+.5);
	
    float d = v_noise(st*7.0+iTime*0.25);
    
    fragColor = float4(_color,mix(.25,.25+.75*d,1.0-smoothstep(-aa,aa,s)));
}


float3 palette( in float t, in float3 a, in float3 b, in float3 c, in float3 d )
{
    return a + b*cos( 6.28318*(c*t+d) );
}
void PolarLight_float(float2 uv ,float scale,float2 _offset,out float4 fragColor)
{
	float time = iTime * .095 + 23.0;
	uv-=_offset;
    uv*=scale;

    float2 p = mod(uv*TAU, TAU) - 213.0;
	float2 i = float2(p);
	float c = 1.0;
	float inten = .005;

	for (int n = 0; n < 8; n++) {
		float t = time * (1.0 - (3.5 / float(n + 1)));
		i = p + float2(cos(t - i.x) + sin(t + i.y), sin(t - i.y) + cos(t + i.x));
		c += 1.0 / length(float2(p.x / (sin(i.x + t) / inten), p.y / (cos(i.y + t) / inten)));
	}
    
	c /= float(8);
	c = 1.17 - pow(c, 1.4);
	float3 colour = F3(pow(abs(c), 10.0));
    colour *= 1.1;
    colour = clamp(colour + F3(0.095), 0.0, 1.0);
  
    float palettePhase = abs(sin(uv.x * uv.y + iTime * 0.05)) + iTime * 0.1;
    float3 col = palette(palettePhase, float3(0.5, 0.5, 0.5), float3(0.5, 0.5, 0.5), float3(2.0, 1.0, 0.0), float3(0.50, 0.20, 0.25));
    col *=  colour;
 
	fragColor = float4(col, 1.0);
}


float rand(float2 n) {
    return fract(sin(dot(n, float2(12.9898,12.1414))) * 83758.5453);
}
float noise_6(float2 n) {
    const float2 d = float2(0.0, 1.0);
    float2 b = floor(n);
    float2 f = smoothstep(F2(0.0), F2(1.0), fract(n));
    return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}
float3 ramp(float t) {
	return t <= .5 ? float3( 1. - t * 1.4, .2, 1.05 ) / t : float3( .3 * (1. - t) * 2., .2, 1.05 ) / t;
}
float2 polarMap(float2 uv, float shift, float inner) {

    uv = F2(0.5) - uv;
    
    
    float px = 1.0 - fract(atan2(uv.y, uv.x) / 6.28 + 0.25) + shift;
    float py = (sqrt(uv.x * uv.x + uv.y * uv.y) * (1.0 + inner * 2.0) - inner) * 2.0;
    
    return float2(px, py);
}
float fire(float2 n) {
    return noise_6(n) + noise_6(n * 2.1) * .6 + noise_6(n * 5.4) * .42;
}
float shade(float2 uv, float t) {
    uv.x += uv.y < .5 ? 23.0 + t * .035 : -11.0 + t * .03;    
    uv.y = abs(uv.y - .5);
    uv.x *= 35.0;
    
    float q = fire(uv - t * .013) / 2.0;
    float2 r = float2(fire(uv + q / 2.0 + t - uv.x - uv.y), fire(uv + q - t));
    
    return pow((r.y + r.y) * max(.0, uv.y) + .1, 4.0);
}
float3 _color(float grad,float m2) {
    
    grad =sqrt( grad);
    float3 _color = float3(1.0 / (pow(float3(0.5, 0.0, .1) + 2.61, F3(2.0))));
    float3 color2 = _color;
    _color = ramp(grad);
    _color /= (m2 + max(F3(0), _color));
    
    return _color;

}
void CircleGlow6_float(float2 uv ,float scale,float2 _offset,float m1,float m2,out float4 fragColor) 
{
    uv-=_offset;
    uv*=scale;
    float t = iTime;
    float ff = 1.0 - uv.y;
    uv.x -= (iResolution.x / iResolution.y - 1.0) / 2.0;
    float2 uv2 = uv;
    uv2.y = 1.0 - uv2.y;
   	uv = polarMap(uv, 1.3, m1);
   	uv2 = polarMap(uv2, 1.9, m1);

    float3 c1 = _color(shade(uv, t),m2) * ff;
    float3 c2 = _color(shade(uv2, t),m2) * (1.0 - ff);
    
    fragColor = float4(c1 + c2, 1.0);
}


float3 mod289(float3 x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}
float4 mod289(float4 x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}
float4 taylorInvSqrt(float4 r)
{
    return 1.79284291400159 - 0.85373472095314 * r;
}
float snoise(float3 v)
{ 
    const float2  C = float2(1.0/6.0, 1.0/3.0) ;
    const float4  D = float4(0.0, 0.5, 1.0, 2.0);

    // First corner
    float3 i  = floor(v + dot(v, C.yyy) );
    float3 x0 =   v - i + dot(i, C.xxx) ;

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min( g.xyz, l.zxy );
    float3 i2 = max( g.xyz, l.zxy );
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y
    float3 x3 = x0 - D.yyy;      // -1.0+3.0*C.x = -0.5 = -D.y

    // Permutations
    i = mod289(i); 
    float4 p = 
        permute
        (
            permute
            ( 
                permute
                (
                    i.z + float4(0.0, i1.z, i2.z, 1.0)
                )
                + i.y + float4(0.0, i1.y, i2.y, 1.0 )
            )
            + i.x + float4(0.0, i1.x, i2.x, 1.0 )
        );

    // Gradients: 7x7 points over a square, mapped onto an octahedron.
    // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float n_ = 0.142857142857; // 1.0/7.0
    float3  ns = n_ * D.wyz - D.xzx;

    float4 j = p - 49.0 * floor(p * ns.z * ns.z);  //  mod(p,7*7)

    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_ );    // mod(j,N)

    float4 x = x_ *ns.x + ns.yyyy;
    float4 y = y_ *ns.x + ns.yyyy;
    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4( x.xy, y.xy );
    float4 b1 = float4( x.zw, y.zw );

    //float4 s0 = float4(lessThan(b0,0.0))*2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1,0.0))*2.0 - 1.0;
    float4 s0 = floor(b0)*2.0 + 1.0;
    float4 s1 = floor(b1)*2.0 + 1.0;
    float4 sh = -step(h, F4(0.0));

    float4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ;
    float4 a1 = b1.xzyw + s1.xzyw*sh.zzww ;

    float3 p0 = float3(a0.xy,h.x);
    float3 p1 = float3(a0.zw,h.y);
    float3 p2 = float3(a1.xy,h.z);
    float3 p3 = float3(a1.zw,h.w);

    //Normalise gradients
    float4 norm = taylorInvSqrt(float4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;

    // Mix final noise value
    float4 m = max(0.6 - float4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
    m = m * m;
    return 42.0 * dot( m*m, float4( dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3) ) );
}
float fbm1(float3 p, float theta, float f, float lac, float r)
{
    float3x3 mtx = float3x3(
        cos(theta), -sin(theta), 0.0,
        sin(theta), cos(theta), 0.0,
        0.0, 0.0, 1.0);

    float frequency = f;
    float lacunarity = lac;
    float roughness = r;
    float amp = 1.0;
    float total_amp = 0.0;

    float accum = 0.0;
    float3 X = p * frequency;

    accum += amp * snoise(X);
    X *= (lacunarity + (snoise(X) + 0.1) * 0.006);
    X = mul(mtx , X);

    total_amp += amp;
    amp *= roughness;

    return accum / total_amp;
}
float turbulence(float val)
{
    float n = 1.0 - abs(val);
    return n * n;
}
float pattern(in float3 p, inout float3 q, inout float3 r)
{
    q.x = fbm1( p + 0.0, 0.0, 1.0, 2.0, 0.33 );
    q.y = fbm1( p + 6.0, 0.0, 1.0, 2.0, 0.33 );

    r.x = fbm1( p + q - 2.4, 0.0, 1.0, 3.0, 0.5 );
    r.y = fbm1( p + q + 8.2, 0.0, 1.0, 3.0, 0.5 );

    q.x = turbulence( q.x );
    q.y = turbulence( q.y );

    float f = fbm1( p + (1.0 * r), 0.0, 1.0, 2.0, 0.5);

    return f;
}
void PatternFBM_float(float2 uv ,float scale,float2 _offset,out float4 fragColor)
{
    uv-=_offset;
    uv*=scale;

    float t = iTime * 0.1;

    float3 spectrum[4];
    spectrum[0] = float3(0.94, 0.02, 0.03);
    spectrum[1] = float3(0.04, 0.04, 0.22);
    spectrum[2] = float3(1.00, 0.80, 1.00);
    spectrum[3] = float3(0.20, 0.40, 0.50);

    uv -= 0.5;
    uv *= 3.5;

    float3 p = float3(uv.x, uv.y, t);
    float3 q = F3(0.0);
    float3 r = F3(0.0);
    float f = pattern(p, q, r);

    float3 col = F3(0.0);
    col = mix(spectrum[1], spectrum[3], pow(length(q), 4.0));
    col = mix(col, spectrum[0], pow(length(r), 1.4));
    col = mix(col, spectrum[2], f);

    col = pow(col, F3(2.0));
    float sb = (col.x+col.y+col.z)/3.0;
    fragColor = float4(sb,sb,sb, 1.0);
}


float GetTime()
{
    return iTime;
}
float GetTotalTime()
{
    return mod(GetTime(), 14.0) / 14.0;
}

float Acceptable(in float3 col)
{
    if (col.g > 0.4 && col.r < 0.4 && col.b < 0.4)
    {
        return 0.0;
    }
    return 1.0;
}

float Grid(in float2 pos)
{
    float v = step(0.2, mod(pos.y * 100.0, 0.75));
    return v;
}

float Intensivity(in float3 orig)
{
    return 0.299 * orig.r + 0.587 * orig.g + 0.114 * orig.b;
}

float3 Blue(in float2 uv,float3 rgb)
{
    float3 orig = rgb;
    
    uv.y += 0.5 * GetTotalTime();
    
    float v = Acceptable(orig);
    float i = Intensivity(orig);
    
    float3 backColor = F3(0.0);
    float3 blue = float3(0.0, 1.0, 1.0);
    float3 back = backColor * 0.75;
    
    float res = Grid(uv) * v;
    float resI = (res * (max(i, 0.55) - 0.05) * 1.55);
    float3 resColor = F3(resI) * blue * 1.15;
    return resColor + back * (1.0 - res);
}

float2 _Line(in float2 pos)
{
    float t = GetTotalTime();
    float v = sin((pos.y + t * 4.0) * 3.14 * 4.0);
    pos.x += 0.0015 * sin(v * 3.14) * cos(v * 3.14 * 2.0);
    pos.y += 0.0055 * cos(v * 3.14) * sin(v * 3.14 * 2.0);
    return pos;
}

float3 Factor(in float2 uv, float stepV, int stepI, int stepJ,float3 rgb)
{
    float3 resCol = F3(0.0);
    int steps = 0;
    for (int i = -stepI; i <= stepI; ++i)
    {
        for (int j = -stepJ; j <= stepJ; ++j)
        {
            resCol += Blue(uv + float2(stepV * float(i), stepV * float(j)),rgb); 
            ++steps;
        }
    }
    resCol /= float(steps);
    return resCol;
}
    
void mainImage( out float4 fragColor, in float2 fragCoord )
{
}
float circle(float2 st, float radius){
    float2 l = st - F2(0.5);
    return 1.0 - smoothstep(0.99 * radius, radius * 1.01, dot(l,l)*4.0);
}
float semicircle(float2 st, float radius) {
    float2 l = st;
    float pct = 1.0 - smoothstep(0.95 * radius * radius, 1.05 * radius * radius, dot(l,l));
    
    pct *= step(0.0, l.y);
    return pct;
}
float rectangle(float2 st, float2 size) {
    size = 0.5 - 0.5 * size;
    float2 uv = smoothstep(size, size + F2(1e-4), st);
    uv *= smoothstep(size, size + F2(1e-4), F2(1.0) - st);
    return uv.x * uv.y;
}
float bagua(float2 uv, float n) {
    float pct = 0.0;
    float2 st = uv;
    pct = rectangle(st + float2(0.0, 0.15), float2(0.5, 0.08));
    pct += rectangle(st, float2(0.5, 0.08));
    pct += rectangle(st + float2(0.0, -0.15), float2(0.5, 0.08));

    pct -= (step(0.1,n) * step(n,1.0) + step(3.1,n) * step(n,4.1) + step(4.1,n) * step(n,5.) + step(6.1,n) * step(n,7.)) * rectangle(st + float2(0.0,0.15), float2(0.1, 0.08)); // 1,4,5,7 
    pct -= (step(1.1,n) * step(n,2.1) + step(3.1,n) * step(n,4.1) + step(5.1,n) * step(n,6.) + step(6.1,n) * step(n,7.)) * rectangle(st, float2(0.1, 0.08));      // 2,4,6,7
    pct -= (step(2.1,n) * step(n,3.1) + step(4.1,n) * step(n,5.1) + step(5.1,n) * step(n,6.) + step(6.1,n) * step(n,7.)) * rectangle(st + float2(0.0,-0.15), float2(0.1, 0.08)); // 3,5,6,7

    return pct;
}
float2x2 rotate2d(float angle) {
    return float2x2(cos(angle), -sin(angle),
                sin(angle), cos(angle));
}
float2x2 scale(float2 scale){
    return float2x2(scale.x, 0.0,
                0.0, scale.y);
}
void PatternTaichi_float(float2 uv ,float2 scale,float2 _offset,out float4 fragColor)
{
    uv = _Line(uv);
    uv -= _offset;
    uv *= scale;
    float3 _color = F3(0.0);    
    float pct = 0.0;
    
    float2 st = 2.0 * uv - 1.0;
    st = mul(rotate2d(-0.5 * iTime) ,st);
    pct = semicircle(st, 0.5);
    pct += semicircle(mul(rotate2d(PI) , (st + float2(-0.25, 0.0))), 0.25);
    pct -= semicircle((st + float2(0.25, 0.0)), 0.25);   
    pct -= circle(st + float2(0.25, 0.5),0.02);
    pct += circle(st + float2(0.75, 0.5),0.02);
    
    st = 2.0 * uv - 1.0;
    st = mul(2.0 * st , rotate2d(-0.5 * iTime));
    
    float2 offset = -float2(-0.5, -2.);
    pct += bagua(st- float2(-0.5, -2.), 2.0);
    pct += bagua(mul(st , rotate2d(PI * 0.25)) + offset, 3.0);
    pct += bagua(mul(st , rotate2d(PI * 0.5)) + offset, 0.0);
    pct += bagua(mul(st , rotate2d(PI * 0.75)) + offset, 1.0);
    pct += bagua(mul(st , rotate2d(PI)) + offset, 5.0);
    pct += bagua(mul(st , rotate2d(PI * 1.25)) + offset, 4.0);
    pct += bagua(mul(st , rotate2d(PI * 1.5)) + offset, 7.0);
    pct += bagua(mul(st , rotate2d(PI * 1.75)) + offset, 6.0);   
    
    _color = F3(pct);
    fragColor = float4(_color, 1.);

    
    
    
    float3 resCol = Factor(uv, 0.015, 6, 1,_color);
    resCol += Factor(uv, 0.015, 4, 4,_color);
    resCol += Factor(uv, 0.004, 2, 2,_color);
    resCol += Factor(uv, 0.001, 1, 2,_color);
    
    resCol /= 4.0;
    resCol= F3((resCol.r+resCol.g+resCol.b)/3.);
    
    
    fragColor = float4(_color, _color.r);
}




float Framenoise(float2 n) {
    const float2 d = float2(0.0, 1.0);
    float2 b = floor(n);
    float2 f = mix(F2(0.0), F2(1.0), frac(n));
    return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);   
}

float3 Frameramp(float t) {
	return t <= .5 ? float3( 1. - t * 1.4, .2, 1.05 ) / t : float3( .3 * (1. - t) * 2., .2, 1.05 ) / t;
}
float3 getLine(float3 _color,float3 col, float2 fc, float2x2 mtx, float shift){
    float t = iTime;
    float2 uv = mul(mtx,(fc / iResolution.xy));
    
    uv.x += uv.y < .5 ? 23.0 + t * .35 : -11.0 + t * .3;    
    uv.y = abs(uv.y - shift);
    uv *= 5.0;
    
    float q = fire(uv - t * .013) / 2.0;
    float2 r = float2(fire(uv + q / 2.0 + t - uv.x - uv.y), fire(uv + q - t));
    float3 color = float3(1.0 / (pow(float3(0.8, 0.0, .1) + 1.61, F3(4.0))));
    
    float grad = pow((r.y + r.y) * max(.0, uv.y) + .1, 4.0);
    color = Frameramp(grad);
    color /= (1.50 + max(F3(0), color));
    
    if(color.b < .00000005)
        color = F3(.0);
    
    return mix(col, color+_color, color.b);
}
void PatternFrame_float(float4 _color,float2 uv ,float2 scale,float2 _offset,out float4 fragColor)
{ 
    float3 color = 0.;
    color = getLine(_color.rgb,color, uv* _ScreenParams,float2x2(1., 1., 0., 1.), 1.02);
    color = getLine(_color.rgb,color, uv* _ScreenParams,float2x2(1., 1., 1., 0.), 1.02);
    color = getLine(_color.rgb,color, uv* _ScreenParams,float2x2(1., 1., 0., 1.), -0.02);
    color = getLine(_color.rgb,color, uv* _ScreenParams,float2x2(1., 1., 1., 0.), -0.02);

    fragColor = vec4(color, 1.0);
}