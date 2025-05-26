#version 400 core
precision highp float;

in vec2 texCoord;

uniform vec4 projectionParameters;

uniform sampler2D screenTexture;
uniform sampler2D screenDepth;
uniform sampler2D screenNormal;
uniform sampler2D shadowMap;
uniform sampler2D ssaoNoise;

uniform sampler2D cubemap;

uniform sampler2D lightCookieTexture;

// point light shadowmaps
uniform sampler2D shadowMap0;
uniform sampler2D shadowMap1;
uniform sampler2D shadowMap2;
uniform sampler2D shadowMap3;
uniform sampler2D shadowMap4;
uniform sampler2D shadowMap5;

uniform int cascadeCount;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 shadowViewProjection;

// point light matrices
uniform mat4 shadowViewProjection0;
uniform mat4 shadowViewProjection1;
uniform mat4 shadowViewProjection2;
uniform mat4 shadowViewProjection3;
uniform mat4 shadowViewProjection4;
uniform mat4 shadowViewProjection5;

layout(location = 0) out vec4 FragColor;

// single light parameters
uniform vec3 spotLightDir; // world space direction of the spot light cone
uniform vec3 lightPosition; // world space light pos
uniform float lightRadius; // should be renamed to lightRange
uniform float lightIntensity; // light intensity
uniform vec3 spotLightCones; // x and y are min max derivitives, z is unused
uniform vec3 lightColour; // light colour
uniform int lightType; // int for light type, 0 = spot, 1 = point, 2 = directional
uniform int lightShadowsEnabled; // enable shadows or not for this light
uniform int lightEnableVolumetrics;
uniform float lightShadowBias; // shadow bias for this light
uniform int enableCookie;
uniform float lightShadowBiasNormal; // shadow normal bias
uniform int enableIESProfile; // enable IES profile 
uniform sampler2D IESProfileTexture;  // IES texture sampler

uniform vec3 cameraPosWS;

// shadow res
uniform float shadowResolution;

// screen res
uniform vec4 screenDimensions;

// DELETE THIS WHEN MOVING AO TO ACTUAL FILE
uniform vec4 SSAOParams;
// X = Radius, Y = Bias, Z = Number of Samples

// various attenuation functions here :)
float saturate(float x)
{
    return clamp(x, 0, 1); // thanks opengl for not having shorthand like directx
}
float Square(float x)
{
    return x * x; // this is useful for all sorts why doesn't gl have it :(
}

float CalcSpotAttenuation(vec3 spotDir, vec3 lightDir, vec2 spotAngles)
{
    float atten = Square(saturate(dot(-spotDir, lightDir) * spotAngles.x + spotAngles.y));
    return atten;
}

float CalcRangeAttenuation(vec3 lightPos, vec3 worldPos, float distanceSqr)
{
    float atten = Square(saturate(1.0f - Square(distanceSqr * lightRadius)));
    return atten;
}

float CalculateAttenuation(vec3 spotDir, vec3 lightDir, vec3 lightPos, vec3 worldPos, vec2 spotAngles)
{
    float spotAtten = 1.0f;
    
    if(lightType == 0)
    {
        spotAtten = CalcSpotAttenuation(spotDir, lightDir, spotAngles);
    }
    
    vec3 ray = lightPos - worldPos;
    float distanceSqr = max(dot(ray, ray), 0.000000001f);
    
    float rangeAtten = CalcRangeAttenuation(lightPos, worldPos, distanceSqr);
    
    return max(spotAtten * rangeAtten / distanceSqr, 0);
}

vec3 WorldPositionFromDepth(vec2 UV)
{
    float depth = max(texture(screenDepth, UV).r, 0.000000001f) * 2 - 1;
    vec2 screen = UV * 2 - 1;
    vec4 ndc = vec4(screen, depth, 1.0f);

    vec4 ndcToView =  ndc * inverse(projection);
    ndcToView.xyz /= ndcToView.w;

    vec4 viewToWorld = (vec4(ndcToView.xyz, 1.0f) * inverse(view));
    viewToWorld.xyz /= viewToWorld.w;
    
    vec3 world = viewToWorld.xyz;
    return viewToWorld.xyz;
}

vec3 ViewPositionFromDepth(vec2 UV)
{
    float depth = texture(screenDepth, UV).r;
    vec2 screen = UV * 2 - 1;
    
    vec4 ndc = vec4(screen, depth, 1.0f);
    
    vec4 ndcToView = ndc * inverse(projection);
    ndcToView.xyz /= ndcToView.w;
    
    return ndcToView.xyz;
}

const float PI = 3.14159265359;

vec2 EquirectangularUVFromReflectionVector(vec3 direction)
{
    float theta = atan(direction.z, direction.x);
    float phi = asin(direction.y);
    
    float u = theta / (2.0f * PI) + 0.5f;
    float v = phi / PI + 0.5f;
    
    return vec2(u, v);
}

// Computes radical inverse of bits for Hammersley sequence.
float RadicalInverse_VdC(uint bits) {
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10; // Divide by 2^32
}

// Returns a 2D Hammersley point in [0,1]^2 for sample i of N total samples.
vec2 Hammersley(uint i, uint N) {
    return vec2(float(i) / float(N), RadicalInverse_VdC(i));
}

// Importance sample a microfacet normal (half vector) based on the GGX distribution.
vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness) {
    float a = roughness * roughness;
    
    a = clamp(a, 0.001f, 1.0f);

    // Convert random numbers to spherical coordinates with GGX distribution.
    float phi = 2.0 * PI * Xi.x;
    // Compute cos(theta) using the inverse CDF of the GGX distribution.
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

    // Tangent space vector.
    vec3 H_tangent;
    H_tangent.x = cos(phi) * sinTheta;
    H_tangent.y = sin(phi) * sinTheta;
    H_tangent.z = cosTheta;

    // Create an orthonormal basis (T, B, N)
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);

    // Transform from tangent space to world space.
    vec3 H = tangent * H_tangent.x + bitangent * H_tangent.y + N * H_tangent.z;
    return normalize(H);
}

// Integrates the specular reflection from the environment cubemap using importance sampling.
vec3 integrateSpecularEnvironment(vec3 N, vec3 V, float roughness, vec2 noiseUV) {
    const int SAMPLE_COUNT = 64;  // Adjust sample count for performance vs. quality.
    vec3 prefilteredColor = vec3(0.0);
    float totalWeight = 0.0;

    for (int i = 0; i < SAMPLE_COUNT; ++i) {
        // Generate a quasi-random sample using the Hammersley sequence.
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);

        // Compute the half vector using GGX importance sampling.
        vec3 H = ImportanceSampleGGX(Xi, N, roughness);

        // Compute the reflection direction L from V and H.
        vec3 L = normalize(2.0 * dot(V, H) * H - V);
        
        // Discard samples that fall below the horizon.
        float NdotL = max(dot(N, L), 0.0);
        
        vec2 R = EquirectangularUVFromReflectionVector(L);
        
        if (NdotL > 0.0) {
            // Sample the environment cubemap. Here we use level 0 since mipmapping is absent.
            vec3 sampleColor = texture(cubemap, R + noiseUV).rgb;

            // Weight the sample by NdotL (the cosine of the angle) for a cosine-weighted average.
            prefilteredColor += sampleColor * NdotL;
            totalWeight += NdotL;
        }
    }

    // Average the accumulated color.
    return prefilteredColor / totalWeight;
}

// GGX Normal Distribution Function (NDF)
float D_GGX(float NdotH, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float denom = (NdotH * NdotH) * (a2 - 1.0) + 1.0;
    return a2 / (PI * denom * denom);
}

// Geometry function (Smith GGX)
float G_Smith(float NdotV, float NdotL, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float G_V = NdotV / (NdotV * (1.0 - k) + k);
    float G_L = NdotL / (NdotL * (1.0 - k) + k);

    return G_V * G_L;
}

// Fresnel (Schlick's approximation)
vec3 F_Schlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

// Main PBR lighting function
vec3 PBR(vec3 N, vec3 V, vec3 L, vec3 albedo, float metallic, float roughness, vec3 F0, vec3 lightColor, float lightIntensity) {
    vec3 H = normalize(V + L);

    // Fresnel reflectance at normal incidence
    vec3 baseF0 = vec3(0.04); // Default F0 for non-metals
    F0 = mix(baseF0, albedo, metallic); // Blend F0 based on metallic

    // Calculate dot products
    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.0);
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    // Specular calculations
    float D = D_GGX(NdotH, roughness); // roughness
    float G = G_Smith(NdotV, NdotL, roughness); // roughness
    vec3 F = F_Schlick(HdotV, F0);
    
    /*
    vec3 ReflectionVector = reflect(-V, N);
    vec2 reflectionUV = EquirectangularUVFromReflectionVector(ReflectionVector);
    vec2 noiseUV = texture(ssaoNoise, texCoord * (screenDimensions.xy / 16)).rg * 2 - 1;
    vec3 refl = vec3(0.0f);//;integrateSpecularEnvironment(N, V, clamp(roughness, 0.01, 1.0f), noiseUV * (0.075f*roughness));
*/

    vec3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);
    
  //  specular += refl * F;


    // Diffuse calculations
    vec3 kD = (1.0 - F) * (1.0 - metallic); // Diffuse term
    vec3 diffuse = (kD * albedo) / PI;

    // Combine diffuse and specular
    vec3 lighting = (diffuse + specular) * lightColor * lightIntensity * NdotL;

    return lighting;
}

float linearizeDepth(float depth)
{
    float u_Near = projectionParameters.x;
    float u_Far = projectionParameters.y;

    depth = 1.0 - depth;
    depth = depth * 2.0 - 1.0;
    return (2.0 * u_Near * u_Far) / (u_Far + u_Near - depth * (u_Far - u_Near));
}

float NormalOrientedAmbientOcclusion(vec2 UV, vec3 vsNormal)
{
    // Define the number of samples in the kernel
    const int kernelSize = 32;

    // Hardcoded SSAO kernel samples (each vec3 is a sample offset within the hemisphere)
    // These samples are oriented to lie in the hemisphere (z > 0) and are intended for SSAO.
    vec3 ssaoKernel[32] = vec3[](
    vec3(-0.0921, -0.0257, 0.0328),
    vec3( 0.1043, -0.0675, 0.0889),
    vec3(-0.1234,  0.1456, 0.1023),
    vec3( 0.0876,  0.1923, 0.1567),
    vec3(-0.1587, -0.1123, 0.1345),
    vec3( 0.1345, -0.0789, 0.1893),
    vec3(-0.1023,  0.1345, 0.0876),
    vec3( 0.1178,  0.0894, 0.1523),
    vec3(-0.1523, -0.1089, 0.1734),
    vec3( 0.1378, -0.1587, 0.1056),
    vec3(-0.1298,  0.1534, 0.1178),
    vec3( 0.0987,  0.1298, 0.1456),
    vec3(-0.1376, -0.1432, 0.1789),
    vec3( 0.1623, -0.1845, 0.1023),
    vec3(-0.1489,  0.1623, 0.1287),
    vec3( 0.1334,  0.1389, 0.1576),
    vec3(-0.2213, -0.1756, 0.3124),
    vec3( 0.1987, -0.2345, 0.2783),
    vec3(-0.1876,  0.2134, 0.2998),
    vec3( 0.2543,  0.1987, 0.3124),
    vec3(-0.2311, -0.2105, 0.2893),
    vec3( 0.2675, -0.1987, 0.3214),
    vec3(-0.2456,  0.2675, 0.2893),
    vec3( 0.2783,  0.2456, 0.3124),
    vec3(-0.3124, -0.2213, 0.3345),
    vec3( 0.3214, -0.2675, 0.2783),
    vec3(-0.2893,  0.3124, 0.2675),
    vec3( 0.3124,  0.2893, 0.3214),
    vec3(-0.3345, -0.2783, 0.3124),
    vec3( 0.2998, -0.3124, 0.2675),
    vec3(-0.3214,  0.2783, 0.3124),
    vec3( 0.3124,  0.3214, 0.3345)
    );
    
    vec2 noiseScale = screenDimensions.xy / 32.0f;
    
    float depth = texture(screenDepth, UV).r;
    
    vec3 fragPos = ViewPositionFromDepth(UV);
    
   
    float radius = SSAOParams.x;
    
    float occlusion = 0.0f;
    int sampleCount = int(SSAOParams.z);
    
    float rndFlip = 1.0f;
    for(int i = 0; i < sampleCount; i++)
    {
        vec3 randomVec = normalize(texture(ssaoNoise, UV * noiseScale + ((vec2(i,i)*(noiseScale))))).xyz * 2 - 1;
        randomVec.z = 0.0f;
        randomVec *= rndFlip;
        vec3 tangent = normalize(randomVec - vsNormal.xyz * dot(randomVec, vsNormal.xyz));
        vec3 bitangent = cross(vsNormal.xyz, tangent);
        mat3 TBN = mat3(tangent, bitangent, vsNormal.xyz);

        
        vec3 samplePos = fragPos + (TBN * normalize(ssaoKernel[i]*8) * radius);
        vec4 offset = vec4(samplePos, 1.0f) * projection;
        offset.xyz /= offset.w;
        
        vec2 sampleUV = offset.xy * 0.5f + 0.5f;
        
        float sampleDepth = texture(screenDepth, sampleUV).r;
        
        vec3 sampleViewPos = ViewPositionFromDepth(sampleUV);
        
        rndFlip *= -1;
        
        if(abs(samplePos.z+radius - (sampleViewPos.z)) >= (radius))
        {
            occlusion += 1;
        }
        
    }
    
    occlusion = 1.0f - (occlusion / float(sampleCount));

  //  return vsNormal.z;
    return 1-occlusion;
}

float GetShadowAttenuation(mat4 shadowViewProj, sampler2D shadowMapTex, vec3 pos, vec3 normal, vec3 lightDir, float biasMultiplier, const int useShadowFiltering)
{
    if(lightShadowsEnabled == 1)
    {
        vec4 shadowPos = vec4(pos, 1.0f) * shadowViewProj;
        shadowPos.xyz /= shadowPos.w;

        shadowPos.xyz = shadowPos.xyz * 0.5f + 0.5f;

        
        
        //shadowPos.xy = clamp(shadowPos.xy, 0, 1);

        // PCF Parameters
        int pcfSamples = 16;         // Number of Poisson disk samples
        
        if(useShadowFiltering == 0)
        {
            pcfSamples = 1;
        }
        
        float lightSize = 0.0001;     // Size of the sampling region for soft shadows

        // Poisson Disk Samples (precomputed offsets)
        vec2 poissonDisk[16] = vec2[](
        vec2(-0.94201624, -0.39906216), vec2(0.94558609, -0.76890725),
        vec2(-0.094184101, -0.92938870), vec2(0.34495938, 0.29387760),
        vec2(-0.91588581, 0.45771432), vec2(-0.81544232, -0.87912464),
        vec2(-0.38277543, 0.27676845), vec2(0.97484398, 0.75648379),
        vec2(0.44323325, -0.97511554), vec2(0.53742981, -0.47373420),
        vec2(-0.26496911, -0.41893023), vec2(0.79197514, 0.19090188),
        vec2(-0.24188840, 0.99706507), vec2(-0.81409955, 0.91437590),
        vec2(0.19984126, 0.78641367), vec2(0.14383161, -0.14100790)
        );

        // Random rotation based on fragment position
        float rotationAngle = fract(sin(dot(shadowPos.xy, vec2(12.9898, 78.233))) * 43758.5453) * 6.283185; // Random angle in radians
        mat2 rotationMatrix = mat2(cos(rotationAngle), -sin(rotationAngle),
        sin(rotationAngle), cos(rotationAngle));

        float shadowFactor = 0.0;

        float avgDepth = 3;

        // Average depth for bias adjustment
    
   /* for(int i = 0; i < pcfSamples; i++)
    {
        avgDepth += abs(texture(shadowMapTex, shadowPos.xy + (rotationMatrix * (poissonDisk[i])) * lightSize * 8).r);
    }

        avgDepth /= pcfSamples;

        avgDepth = abs(shadowPos.z - avgDepth);*/

        const float MAX_BIAS_DISTANCE = 100;
        const float MAX_BIAS_DISTANCE_MUL = 30000.0;

        float biasAdjustment = pow(distance(cameraPosWS, pos), 1.0f) / MAX_BIAS_DISTANCE;
        biasAdjustment = clamp(biasAdjustment, 1, MAX_BIAS_DISTANCE);
        biasAdjustment += MAX_BIAS_DISTANCE_MUL;

        // Adaptive bias based on normal and light direction
        float normalBiasFactor = clamp(dot(normal, -lightDir), 0.0, 1.0);
        float bias = lightShadowBiasNormal * normalBiasFactor; // Lower the bias multiplier
        bias = bias + (lightShadowBias * biasMultiplier);

        // Loop over Poisson disk samples
        for (int i = 0; i < pcfSamples; i++) {
            vec2 offset = vec2(0);
            if(useShadowFiltering == 1)
            {
                offset = rotationMatrix * ((poissonDisk[i] * (1 / shadowResolution)));
            }
            vec2 samplePos = shadowPos.xy + offset;
            
            float sampleDepth = texture(shadowMapTex, samplePos).r;
            
            if ((sampleDepth < shadowPos.z - bias))
            { 
                shadowFactor += 0.0; // Lit
            }
            else
            {
                shadowFactor += 1.0f;
            }
        }

        shadowFactor /= float(pcfSamples); // Average the samples

        if (shadowPos.x <= 0.0f || shadowPos.x >= 1.0f || shadowPos.y <= 0.0f || shadowPos.y >= 1.0f)
        {
            shadowFactor = 1.0;
        }
        
        return 1 - clamp(shadowFactor, 0, 1);
    }
    else
    {
        return 0;
    }


}

float CalculateAttenuationFactor(mat4 shadowViewProj, vec3 pos)
{
    vec4 shadowPos0 = vec4(pos, 1.0f) * shadowViewProj;
    shadowPos0.xyz /= shadowPos0.w;

    shadowPos0.xyz = shadowPos0.xyz * 0.5f + 0.5f;

    return smoothstep(0.95, 1.0f, clamp(distance(vec2(0.5f), shadowPos0.xy)*2, 0.0f, 1.0f));
}

float GetShadowAttenuationPoint(vec3 pos, vec3 normal, vec3 lightDir, const int useShadowFiltering)
{
    float shadowFactor = 1.0f;
    shadowFactor = shadowFactor * GetShadowAttenuation(shadowViewProjection0, shadowMap0, pos, normal, lightDir, 1.0f, useShadowFiltering);
    shadowFactor = shadowFactor * GetShadowAttenuation(shadowViewProjection1, shadowMap1, pos, normal, lightDir, 1.0f, useShadowFiltering);
    shadowFactor = shadowFactor * GetShadowAttenuation(shadowViewProjection2, shadowMap2, pos, normal, lightDir, 1.0f, useShadowFiltering);
    shadowFactor = shadowFactor * GetShadowAttenuation(shadowViewProjection3, shadowMap3, pos, normal, lightDir, 1.0f, useShadowFiltering);
    shadowFactor = shadowFactor * GetShadowAttenuation(shadowViewProjection4, shadowMap4, pos, normal, lightDir, 1.0f, useShadowFiltering);
    shadowFactor = shadowFactor * GetShadowAttenuation(shadowViewProjection5, shadowMap5, pos, normal, lightDir, 1.0f, useShadowFiltering);
    
    return saturate(shadowFactor);
}
float GetShadowAttenuationCSM(vec3 pos, vec3 normal, vec3 lightDir)
{
    float shadowFactor = 0.0f;

    if(cascadeCount > 5)
    {
        float mixCascade = CalculateAttenuationFactor(shadowViewProjection5, pos);
        shadowFactor = mix(shadowFactor, GetShadowAttenuation(shadowViewProjection5, shadowMap5, pos, normal, -lightDir, 50.0f, 1), 1-mixCascade);
    }

    if(cascadeCount > 4)
    {
        float mixCascade = CalculateAttenuationFactor(shadowViewProjection4, pos);
        shadowFactor = mix(shadowFactor, GetShadowAttenuation(shadowViewProjection4, shadowMap4, pos, normal, -lightDir, 40.0f, 1), 1-mixCascade);
    }
    if(cascadeCount > 3)
    {
        float mixCascade = CalculateAttenuationFactor(shadowViewProjection3, pos);
        shadowFactor = mix(shadowFactor, GetShadowAttenuation(shadowViewProjection3, shadowMap3, pos, normal, -lightDir, 30.0f, 1), 1-mixCascade);
    }
    if(cascadeCount > 2)
    {
        float mixCascade = CalculateAttenuationFactor(shadowViewProjection2, pos);
        shadowFactor = mix(shadowFactor, GetShadowAttenuation(shadowViewProjection2, shadowMap2, pos, normal, -lightDir, 20.0f, 1), 1-mixCascade);
    }
    if(cascadeCount > 1)
    {
        float mixCascade = CalculateAttenuationFactor(shadowViewProjection1, pos);
        shadowFactor = mix(shadowFactor, GetShadowAttenuation(shadowViewProjection1, shadowMap1, pos, normal, -lightDir, 10.0f, 1), 1-mixCascade);
    }
    if (cascadeCount > 0)
    {
        float mixCascade = CalculateAttenuationFactor(shadowViewProjection0, pos);

        shadowFactor = mix(shadowFactor, GetShadowAttenuation(shadowViewProjection0, shadowMap0, pos, normal, -lightDir, 1.0f, 1), 1-mixCascade);
    }

    return shadowFactor;
}

bool intersectRaySphere(vec3 rayOrigin, vec3 rayDir, vec3 spherePos, float radius, out float t0, out float t1)
{
    t0 = 0.0f;
    t1 = 0.0f;

    vec3 oc = rayOrigin - spherePos; 
    float a = dot(rayDir, rayDir); 
    float b = 2.0f * dot(oc, rayDir);
    float c = dot(oc, oc) - radius * radius;
    
    const float A_EPSILON = 1e-7f;

    if (abs(a) < A_EPSILON) {
       
        if (c <= 0.0f) { 
            
            t0 = 0.0f;
            t1 = 0.0f;
            return true;
        }
      
        return false;
    }

    float discriminant = b * b - 4.0f * a * c;

    if (discriminant < 0.0f) {
        return false;
    }
    
    float sqrtD = sqrt(discriminant);
    
    float inv2a = 1.0f / (2.0f * a);
    float tNear = (-b - sqrtD) * inv2a;
    float tFar  = (-b + sqrtD) * inv2a;
    
    if (tNear > tFar) {
        float temp = tNear;
        tNear = tFar;
        tFar = temp;
    }
    
    if (tFar < 0.0f) {
        return false;
    }

    t0 = max(tNear, 0.0f);
    t1 = tFar;
    
    if (t0 > t1) {
        return false;
    }

    return true;
}


bool intersectRayCone(vec3 rayOrigin, vec3 rayDir, vec3 coneOrigin, vec3 coneDir, float coneAngle, float coneLength, out float t0, out float t1)
{
    float cosAngle = cos(coneAngle);
    float cos2 = cosAngle * cosAngle;
    
    vec3 co = rayOrigin - coneOrigin;
    
    float vDotD = dot(rayDir, coneDir);
    float coDotD = dot(co, coneDir);
    
    float a = vDotD * vDotD - cos2;
    float b = 2.0 * (vDotD * coDotD - dot(rayDir, co) * cos2);
    float c = coDotD * coDotD - dot(co, co) * cos2;
    
    float discr = b * b - 4.0 * a * c;
    if(discr < 0.0) return false;
    
    float sqrtDiscr = sqrt(discr);
    float tmp0 = (-b - sqrtDiscr) / (2.0 * a);
    float tmp1 = (-b + sqrtDiscr) / (2.0 * a);
    
    if(tmp0 > tmp1)
    {
        float tmp = tmp0;
        tmp0 = tmp1;
        tmp1 = tmp;
    }
    
    const float EPS = 1e-4;
    vec3 hit0 = rayOrigin * rayDir * tmp0;
    vec3 hit1 = rayOrigin + rayDir * tmp1;
    
    float h0 = dot(hit0 - coneOrigin, coneDir);
    float h1 = dot(hit1 - coneOrigin, coneDir);
    
    bool valid0 = (h0 >= 0.0f && h0 <= coneLength);
    bool valid1 = (h1 >= 0.0f && h1 <= coneLength);
    
    if(!valid0 && !valid1) return false;
    
    if(!valid0 && valid1)
    {
        tmp0 = EPS;
    }
    
    t0 = tmp0;
    t1 = tmp1;
    return true;
    
    return true;
}

void main()
{
    vec3 pos = WorldPositionFromDepth(texCoord);

    vec3 lightDir = normalize(lightPosition - pos);

    float attenuation = 1.0f;
    
    if(lightType == 0 || lightType == 1)
    {
        attenuation = CalculateAttenuation(spotLightDir, lightDir, lightPosition, pos, spotLightCones.xy);
    }
    vec4 volumetrics = vec4(0);
    vec4 albMat = texture(screenTexture, texCoord).rgba;
    
    vec3 albedo = albMat.rgb;

    vec4 norm = texture(screenNormal, texCoord).rgba;
    vec3 normal = normalize(norm.xyz * 2 - 1);
    float shadowFactor = 1;
    float radius = inversesqrt(lightRadius);
    float metallic = albMat.w;
    float roughness = norm.w;

    vec3 viewDir = normalize(pos - cameraPosWS);

    vec4 lightCookie = vec4(1);
    
    if (lightType == 0 || lightType == 4)
    {
        if(lightType == 0)
        {
            shadowFactor = GetShadowAttenuation(shadowViewProjection, shadowMap, pos, normal, lightDir, 1.0f, 1);

            float start, end;

            // Reconstruct the actual radius from inverse squared radius
            float radius = inversesqrt(lightRadius);

            float cosOuter = clamp(-spotLightCones.y / spotLightCones.x, 0.0, 1.0);
            
            if (intersectRayCone(cameraPosWS, viewDir, lightPosition, spotLightDir, cosOuter, radius, start, end) && lightEnableVolumetrics == 1)
            {
                
                // Clamp t-values to avoid stepping behind the camera
                start = max(start, 0.001);
                float depth = 1 - texture(screenDepth, texCoord).r;

                depth = linearizeDepth(depth);

                end = min(end, depth);
                
                
                // Compute start/end world positions 
                vec3 startMarch = cameraPosWS + normalize(viewDir) * start;
                vec3 endMarch = cameraPosWS + normalize(viewDir) * end;
                
                float rayLength = max(length(startMarch - endMarch), 0.00001f);
                int steps = 32;

                FragColor = vec4(1, rayLength, 0, 1);
                return;

                float stepSize = max(rayLength / float(steps), 0.00001f);
                vec3 rayDir = normalize(viewDir);
                vec3 rayStep = rayDir * stepSize;
                float noise = fract(sin(dot(texCoord * vec2(12.9898, 78.233), vec2(1.0))) * 43758.5453);
                float jitter = (noise - 0.5) * stepSize * 0.5;
                vec3 rayPos = startMarch + rayDir * jitter;
                volumetrics = vec4(0.0);// Reset debug output
                for (int i = 0; i < steps; i++)
                {
                    vec3 dire = rayPos - lightPosition;
                    vec3 shadowVol = vec3(1.0f);
                    if (lightShadowsEnabled == 1)
                    {
                        shadowVol = vec3(1 - GetShadowAttenuation(shadowViewProjection, shadowMap, rayPos, dire, dire, 8.0f, 0));
                    }
                    
                    volumetrics += vec4(vec3(shadowVol * CalculateAttenuation(spotLightDir, lightDir, lightPosition, rayPos, spotLightCones.xy)), 1.0f);//* CalculateAttenuation(spotLightDir, lightDir, lightPosition, rayPos, spotLightCones.xy)), 1.0f);
                    rayPos += rayStep;
                }

                volumetrics /= steps;
                volumetrics *= lightIntensity * vec4(lightColour, 1.0f);
            }
        }
        
        if (lightType == 4)
        {
            lightDir = -spotLightDir;
        }
        if(enableCookie > 0)
        {
            vec4 projCoords = vec4(pos, 1.0f) * shadowViewProjection;
            projCoords.xyz /= projCoords.w;
            projCoords.xyz = projCoords.xyz * 0.5f + 0.5f;
            projCoords.xy = clamp(projCoords.xy, 0, 1);
            lightCookie = texture(lightCookieTexture, projCoords.xy);
        }
        
    }
    else if (lightType == 1)
    {
        shadowFactor = GetShadowAttenuationPoint(pos, normal, lightDir, 1);

        float start, end;

        // Reconstruct the actual radius from inverse squared radius


        
        if (intersectRaySphere(cameraPosWS, viewDir, lightPosition, radius, start, end) && lightEnableVolumetrics == 1)
        {
            // Clamp t-values to avoid stepping behind the camera
            start = max(start, 0.001);
            float depth = 1-texture(screenDepth, texCoord).r;
            
            depth = linearizeDepth(depth);
            
            end = min(end, depth);
         
            // Compute start/end world positions 
            vec3 startMarch = cameraPosWS + normalize(viewDir) * start;
            vec3 endMarch   = cameraPosWS + normalize(viewDir) * end;
            
            float rayLength = max(distance(startMarch, endMarch), 0.00001f);
            int steps = 2;
            
            steps = int(clamp(float(steps * rayLength),8, 50));
            
            
            float stepSize = max(rayLength / float(steps), 0.001f);
            vec3 rayDir = normalize(-viewDir);
            vec3 rayStep = rayDir * stepSize;

            // Small random offset (e.g. ±0.5 step)
            float noise = fract(sin(dot(texCoord * vec2(12.9898, 78.233), vec2(1.0))) * 43758.5453);
            float jitter = (noise - 0.5); // range [-0.5, +0.5]
            vec3 rayPos = endMarch + rayStep * jitter; // start from slightly offset point
            
            volumetrics = vec4(0.0);// Reset debug output
            for (int i = 0; i < steps; i++)
            {
                vec3 dire = rayPos - lightPosition;
                vec3 shadowVol = vec3 (1.0f);
                if(lightShadowsEnabled == 1)
                {
                    shadowVol = vec3(1-GetShadowAttenuationPoint(rayPos, dire, dire, 0));
                }
                volumetrics += vec4(vec3(shadowVol * CalculateAttenuation(spotLightDir, -dire, lightPosition, rayPos, spotLightCones.xy)), 1.0f);
                rayPos += rayStep ;
            }
           
            volumetrics /= steps;
            volumetrics *= lightIntensity * vec4(lightColour,1.0f);
        }
    }
    else if (lightType == 2)
    {
        lightDir = spotLightDir;
    
        if (cascadeCount > 0)
        {
            shadowFactor = GetShadowAttenuationCSM(pos, normal, -lightDir);
        }
    }
  

    vec3 light = PBR(normal, -viewDir, lightDir, albedo, metallic, roughness, vec3(1, 1, 1), lightColour*lightCookie.rgb, lightIntensity) * attenuation;
    vec3 n = normalize(texture(screenNormal, texCoord).rgb);
    vec3 vsNormal = (vec4(n * 2 - 1, 0.0f) * (view)).xyz;

    //float ao = NormalOrientedAmbientOcclusion(texCoord, normalize(vsNormal));
    
   
   
    FragColor = vec4(((light * (1-(shadowFactor)))) + 0.015f * ((dot(normal, lightDir)*0.5+0.5)*(((lightColour*lightCookie.rgb)*albedo))),1.0f) + volumetrics;

    /*
    vec3 ReflectionVector = viewDir;
    vec2 reflectionUV = EquirectangularUVFromReflectionVector(ReflectionVector);
    vec3 refl = texture(cubemap, reflectionUV).rgb;

    if(texture(screenDepth, texCoord).r >= 0.9999999f)
    {
        FragColor = vec4(refl, 1.0f);
    }
*/
    
}
