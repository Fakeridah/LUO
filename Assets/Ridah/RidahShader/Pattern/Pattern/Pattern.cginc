#include "../Ridah.hlsl"

//Grid
float checkers1( in float3 p )
{
    float2 s = sign(frac(p*.5)-.5);
    return .5 - .5*s.x*s.y;
}
float checkers2( in float2 p )
{
    float2 q = floor(p);
    return mod(q.x+q.y,2.);
}
float checkersTextureGradBox( in float2 p, in float2 ddx, in float2 ddy )
{
	// filter kernel
	float2 w = max(abs(ddx), abs(ddy)) + 0.01;  
	// analytical integral (box filter)
	float2 i = 2.0*(abs(frac((p-0.5*w)/2.0)-0.5)-abs(frac((p+0.5*w)/2.0)-0.5))/w;
	// xor pattern
	return 0.5 - 0.5*i.x*i.y;                  
}
float2 p( in float2 x )
{
    float2 h = frac(x/2.0)-0.5;
    return x*0.5 + h*(1.0-2.0*abs(h));
}
float checkersGradTriangle( in float2 uv, in float2 ddx, in float2 ddy ,float fliter)
{
    float2 w = max(abs(ddx), abs(ddy)) + fliter;    // filter kernel
    float2 i = (p(uv+w)-2.0*p(uv)+p(uv-w))/(w*w); // analytical integral (triangle filter)
    return 0.5 - 0.5*i.x*i.y;                   // xor pattern
}
void BWGrid_float(float2 uv,float scale,float fliter, out float4 baseColor )
{
    uv = uv*scale   ;

    //fixed sb =checkers1(float3(i.uv*100,0));
    //fixed sb =checkersTextureGradBox(uv,ddx(uv),ddy(uv));
    half sb =checkersGradTriangle(uv,ddx(uv),ddy(uv),fliter);
    half4 col = half4(sb,sb,sb,0);
    baseColor= col;
}

float grid(in float2 fragCoord, float level,float size ,float smooth)
{
    float c = abs(mod((fragCoord.x / size), 2.) - 1.);
    c += abs(mod((fragCoord.y / size), 2.) - 1.);
    c /= 2.;
    
    level = 0.5 + level / 2.0;
    
    c = smoothstep(level - smooth, level + smooth, c);
    
    return c;
}
float doubleGrid(in float2 fragCoord, float level,float size ,float smooth)
{
    return grid(fragCoord, level, size , smooth) + grid(fragCoord + size, level,size , smooth);
}
void BWGrid2_float(float2 fragCoord ,float size,float smooth, float level,float rotation, out float4 fragColor )
{    
   Unity_Rotate_Degrees_float(fragCoord,F2(0),rotation,fragCoord);
    level = (cos(level) + 1.0) / 2.0;

    float c = doubleGrid(fragCoord + size, level,size,smooth);

    fragColor = float4(F3(c), 1.0);
}

bool bhash(int3 i)
{
    return (uhash(i) & 1u) != 0u;
}
void Kisrhombille_float(float2 I,float scale, float variation ,out float4 c )
{
    I *= scale;
    //float3 t = barycentric(float2(-1,0), float2(+1,0), float2(0,h), I*2.0*sqrt(3.0));
    float3 b = barycentric(I*4.0*sqrt(3.0));

    int3 t = int3(floor(b)); // tile coordinates
    bool flip = t.x + t.y + t.z == 0; // triangle direction
    float3 l = frac(b); // local coordinates
    if(!flip) l = 1.0 - l; // fix coordinates of every other triangles
    float3 o = l.yzx - l.zxy; // orthogonal local coordinates
    int x = o.x > 0.0 ? 1 : 0;
    int y = o.y > 0.0 ? 1 : 0;
    int z = o.z > 0.0 ? 1 : 0;
    c = F4(bhash(t*8+z*4+y*2+x*1 + int(variation)));

    c = sqrt(c); // gamma
}
//Grid


//Line
void BWLine_float(float2 uv,float frequency,float thickness,float antialias,float rotation, out float4 baseColor )
{
    //float frequency = 10.;
    
    // Thickness should go from 1. to the frequency.
    //float thickness = 1.4;
    
    // Antialias must be a number between 0. and 0.99
    //float antialias = 0.5;
   Unity_Rotate_Degrees_float(uv,F2(0),rotation,uv);
    
    float coord = uv.y * frequency;
    
    float gradient1 = 1. - frac(coord);
    float zigzag1 = (gradient1) * step(frac(coord * 0.5), 0.5);
    
    float gradient2 = frac(coord);
    float zigzag2 = frac(coord) * step(frac((coord + 1.) * 0.5), 0.5);
    
    float zigzag = zigzag2 + zigzag1;
    
    float3 col = F3(smoothstep(1. - antialias, 1., zigzag * thickness));
    
    
    baseColor = float4(col, 1.0);
}
//Line

//Bayer
#define iterBayerMat 15
#define bayer2x2(a) (4-(a).x-((a).y<<1))%4
float GetBayerFromCoordLevel(float2 pixelpos) {
    int2 p=int2(pixelpos);
    int a=0;
    for(int i=0; i<iterBayerMat; i++) {
        a += bayer2x2(p >> (iterBayerMat-1-i) & 1) << (2*i);
    }
    return float(a)/float(2<<(iterBayerMat*2-1));
}
float2 rhombi(float2 pt) {
    float2x2 transform = float2x2(
        1.0, 0.0,
        //cot(60¡ã), csc(60¡ã)
        1.0 / tan(PI / 3.0), 1.0 / sin(PI / 3.0)
    );
    return mul( transform , pt);
}
void BWBayer_float(float2 uv,float aspect,float scale,float ratio,float rotation, out float4 baseColor)
{
    float2 vary_uv =.5+.5*cos(float2(ratio/10,0.5));
   Unity_Rotate_Degrees_float(uv,F2(0),rotation,uv);
    uv.x *= aspect;
    
    uv = rhombi(uv);
    
    uv *= scale;
    
    float2 box = floor(uv);

    float2 box_uv = frac(uv);
    
    box.x += 0.5*step(box_uv.y, box_uv.x); 
    box *= 4.;
    
    float dithering = GetBayerFromCoordLevel(box);
    
    if (dithering < 0.25)
        dithering *= 4.;
    else
        dithering = 4.*(dithering-0.5);
    dithering *= 0.5;
    
    baseColor = F4(dithering < vary_uv.x);
}
//Bayer

//Truchet
#define tpi (2.0*PI)
#define angle(p) mod(atan(p.x, p.y)/tpi, 1.0)
#define _color(offset) abs(sin(offset + float3(3, 7, 11) * PI/6.0))
float hash21(float2 i)
{
    i = frac(i * float2(27.526, 61.341));
    i += dot(i, i + 50.659);
    return frac(i.x * i.y);
}
float truchet(float2 uv,float variation)
{
    float2 id = float2(floor(uv));
    float2 gv = frac(uv) - 0.5;
    
    float n = hash21(id + 0.000001*(variation));
    
    if(n < 0.5) gv.x *= -1.0;
    
    float2 cv = gv - sign(gv.x + gv.y + 0.001) * 0.5;
    
    float cd = length(cv) - 0.5;
    
    float blur = 0.01;
    float width = 0.1;
    
    float mask = smoothstep(blur, -blur, abs(cd) - width);
    
    float checker = mod(id.x + id.y, 2.0) * 2.0 - 1.0;
    float angle = atan2(cv.x, cv.y);
    float flow = sin(variation + angle * checker);
    
    return mask;
}
void Truchet1_float(float2 uv ,float scale,float variation, out float4 baseColor)
{
   uv *= scale;
    float3 col = F3(0.0);
    
    float t = 0.25*variation;
    float2x2 rotate = float2x2(cos(t), sin(t), -sin(t), cos(t));
    
    col += truchet(uv,variation);
    
    col *= (uv.x + uv.y)*.1+1;//_color(0.1*(uv.x + uv.y) + variation);
    
    baseColor = float4(col,1.0);
}

#define e (1/1920*scale)
void Truchet2_float(float2 uv ,float scale,float variation, out float4 baseColor)
{
    float s = 0.05;
    float r = s * 1.25;

    float3 t = barycentric(uv*scale*sqrt(3.0));

    int3 m = int3(floor(t));
    bool flip = m.x + m.y + m.z == 0;
    t = frac(t);
    if(!flip) t = 1.0 - t;

    uint choice = uhash(m + int(floor(variation*10))) % 3u;
    t = choice == 2u ? t.zxy : choice == 1u ? t.yzx : t.xyz;

    float a = smoothstep(s-e,s+e,abs(abs(t.y-t.x)-1.0/3.0));
    float b = smoothstep(r-e,r+e,abs(abs(2.0*t.z-1.)-1.0/3.0));

    baseColor = F4(1.0 - mix(a, b, step(0.5, frac(variation*10/6.0))));

    baseColor = sqrt(baseColor);
}

float Circle(float2 uv, float2 p,float r, float blur ){
    float d = length(uv -p ); 
    float c = smoothstep(r, r-blur, d); 
    return c;

}
float sun(float2 uv, float battery)
{
 	float val = smoothstep(0.3, 0.29, length(uv));
 	float bloom = smoothstep(0.7, 0.0, length(uv));
    float cut = 3.0 * sin((uv.y + iTime * 0.2 * (battery + 0.02)) * 100.0) 
				+ clamp(uv.y * 14.0 + 1.0, -6.0, 6.0);
    cut = clamp(cut, 0.0, 1.0);
    return clamp(val * cut, 0.0, 1.0) + bloom * 0.6;
}
void Sunset_float(float2 uv ,float variation,float4 color1,float4 color2, out float4 fragColor)
{
    uv-=F2( .5f);
    float2 p = float2(.0, .0); 
    float val = Circle(uv,p,0.3, .05); 

     //float val = //smoothstep(0.3, 0.29, length(uv- 0.2));
     //float bloom = smoothstep(0.7, 0.0, length(uv-0.2));
     float battery = 1.0; 
     //float cut = 3.0 * sin((uv.y + iTime * 0.2 * (battery + 0.02)) * 100.0) 
		//		+ clamp(uv.y * 14.0 + 1.0, -6.0, 6.0);
     float cut = 3.0 * sin((uv.y + iTime *variation * (battery + 0.02)) * 100.0) 
				+ clamp(uv.y * 14.0 + 1.0, -6.0, 6.0); //<-- cr¨¦e une parti fixe en haut
     cut = clamp(cut, 0.0, 1.0);
      float3 c= float3(1.,1.,0.)* val; 
              
     float4 rampColor = lerp(color1,color2,-uv.y*2+.3);
    // Output to screen
    if((val*cut).x>0)
        fragColor = float4(rampColor.xyz,1.0);//+  
        else fragColor=0;
}


void SynthGrid_float( float2 uv ,float4 BG_COLOR,float4 GRID_COLOR,float4 GLOW_COLOR,float GRID_SIZE,
float GRID_LINE_SIZE,float GLOW_CONSTANT,float GLOW_MULTIPLIER, out float4 fragColor )
{
float GRID_SIZE_HALF=GRID_SIZE*.5f;
    float2 grid = abs(mod(uv + GRID_SIZE_HALF, GRID_SIZE) - GRID_SIZE_HALF) / fwidth(uv);
    float glowFactor = abs(cos(iTime)) * GLOW_MULTIPLIER + GLOW_CONSTANT;
    float synthline = min(grid.x, grid.y) / GRID_SIZE;
    float glow = min(grid.x, grid.y) / glowFactor;
    float3 lineColor = mix(BG_COLOR.rgb, GRID_COLOR.rgb, smoothstep(GRID_LINE_SIZE / GRID_SIZE, 0.0, synthline));
    float3 glowColor = mix(BG_COLOR.rgb, GLOW_COLOR.rgb, smoothstep(GRID_LINE_SIZE / GRID_SIZE, 0.0, glow));
    float3 color = mix(lineColor, glowColor, uv.x * (sin(iTime) + 1.0) / 1.5);

    fragColor = float4(color, 1.0);
}



