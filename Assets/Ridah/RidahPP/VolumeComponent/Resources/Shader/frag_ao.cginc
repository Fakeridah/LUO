
half frag_ao (v2f_ao i, int sampleCount, float3 samples[INPUT_SAMPLE_COUNT])
{
	// read random normal from noise texture
    half3 randN = tex2D (_RandomTexture, i.uvr).xyz * 2.0 - 1.0;    
    // read scene depth/normal
    //float4 depthnormal = LoadSceneDepth( i.uv);
    float3 viewNorm = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, UnityStereoTransformScreenSpaceTex(i.uv));
    viewNorm = mul(_WorldToCameraMatrix,viewNorm);
    float depth =  SampleSceneDepth(i.uv);
    depth = Linear01Depth(depth,_ZBufferParams);
    depth *= _ProjectionParams.z;
    float scale = _Params.x / depth;
    // accumulated occlusion factor
    float occ = 0.0;
    for (int s = 0; s < sampleCount; ++s)
    {
    	// Reflect sample direction around a random vector
        half3 randomDir = reflect(samples[s], randN);
        
        // Make it point to the upper hemisphere
        half flip = (dot(viewNorm,randomDir)<0) ? 1.0 : -1.0;
        randomDir *= -flip;
        // Add a bit of normal to reduce self shadowing
        randomDir += viewNorm * 0.3;
        if(depth>999)//!!!!!!!!!!!!!!!
        randomDir =0;

        float2 offset = randomDir.xy * scale;
        float sD = depth - (randomDir.z * _Params.x);

		// Sample depth at offset location
        //float4 sampleND = SampleSceneDepth(i.uv + offset);
        float sampleD = SampleSceneDepth(i.uv + offset);
        float3 sampleN = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, UnityStereoTransformScreenSpaceTex(i.uv+offset));
        //DecodeDepthNormal (sampleND, sampleD, sampleN);
        sampleN = mul(_WorldToCameraMatrix,sampleN);
        sampleD = Linear01Depth(sampleD,_ZBufferParams);
        sampleD *= _ProjectionParams.z;
        float zd = saturate(sD-sampleD);
        if (zd > _Params.y) {
        	// This sample occludes, contribute to occlusion
	        occ += pow(1-zd,_Params.z); // sc2
	        //occ += 1.0-saturate(pow(1.0 - zd, 11.0) + zd); // nullsq
        	//occ += 1.0/(1.0+zd*zd*10); // iq
        }        
    }
    occ /= sampleCount;
    return 1-occ;
}

