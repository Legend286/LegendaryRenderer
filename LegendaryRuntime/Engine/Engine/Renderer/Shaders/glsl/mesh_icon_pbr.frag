#version 330 core
out vec4 FragColor;

in vec3 FragPos_World;
in vec2 TexCoords_FS;
in mat3 TBN_FS; // Transforms from tangent space to world space

// Material Textures
uniform sampler2D albedoMap;
uniform sampler2D normalMap;
uniform sampler2D rmaMap; // R: Roughness, G: Metallic, B: AO

// Texture presence flags
uniform bool hasAlbedoMap;
uniform bool hasNormalMap;
uniform bool hasRmaMap;

// Default material properties
uniform vec3 u_AlbedoDefault = vec3(0.8, 0.8, 0.8);
uniform float u_RoughnessDefault = 0.5;
uniform float u_MetallicDefault = 0.0;
uniform float u_AoDefault = 1.0;

// Lighting parameters (simplified for icons)
uniform vec3 u_CameraPosWorld;
uniform vec3 u_LightDirWorld = normalize(vec3(0.5, 0.5, 1.0)); // Example fixed light direction
uniform vec3 u_LightColor = vec3(1.5, 1.5, 1.5);          // Slightly brighter light for icons
uniform vec3 u_AmbientLightColor = vec3(0.05, 0.05, 0.07); // Cool ambient

const float PI = 3.14159265359;

// GGX Normal Distribution Function (NDF)
float D_GGX(float NdotH, float roughness) {
    float a = roughness * roughness;
    a = max(a, 0.001); // Prevent division by zero / issues with a=0
    float a2 = a * a;
    float denom = (NdotH * NdotH) * (a2 - 1.0) + 1.0;
    return a2 / (PI * denom * denom);
}

// Geometry function (Smith GGX)
float G_Smith(float NdotV, float NdotL, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0; // Disney's reparameterization of k for G_Smith
    k = max(k, 0.001); // Prevent k=0 issues

    float G_V = NdotV / (NdotV * (1.0 - k) + k);
    float G_L = NdotL / (NdotL * (1.0 - k) + k);

    return G_V * G_L;
}

// Fresnel (Schlick's approximation)
vec3 F_Schlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

void main()
{
    // Sample material properties
    vec3 albedo = u_AlbedoDefault;
    if (hasAlbedoMap) {
        albedo = texture(albedoMap, TexCoords_FS).rgb;
    }

    float roughness = u_RoughnessDefault;
    float metallic = u_MetallicDefault;
    float ao = u_AoDefault;

    if (hasRmaMap) {
        vec4 rmaSample = texture(rmaMap, TexCoords_FS);
        roughness = rmaSample.r; // R channel: Roughness
        metallic = rmaSample.g;  // G channel: Metallic
        ao = rmaSample.b;        // B channel: AO (adjust if your packing is different, e.g. .a)
    }

    // Normal mapping
    vec3 N = normalize(TBN_FS[2]); // Default normal from TBN (world space object normal)
    if (hasNormalMap) {
        vec3 tangentNormal = texture(normalMap, TexCoords_FS).rgb * 2.0 - 1.0;
        tangentNormal.g = -tangentNormal.g; // Your convention
        N = normalize(TBN_FS * tangentNormal); // Transform sampled normal from tangent to world space
    }
    N = normalize(N); // Ensure N is normalized after all operations

    // Lighting vectors
    vec3 V = normalize(u_CameraPosWorld - FragPos_World);
    vec3 L = normalize(u_LightDirWorld); // Already normalized uniform
    vec3 H = normalize(L + V);

    // PBR Calculations
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.001); // Avoid division by zero for G_Smith
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    // Direct lighting terms
    float D_term = D_GGX(NdotH, roughness);
    float G_term = G_Smith(NdotV, NdotL, roughness);
    vec3  F_term = F_Schlick(HdotV, F0);

    vec3 kS = F_term;
    vec3 kD = vec3(1.0) - kS;
    kD *= (1.0 - metallic); // No diffuse reflection for pure metals

    vec3 numerator    = D_term * G_term * F_term;
    float denominator = 4.0 * NdotV * NdotL + 0.001; // Add epsilon
    vec3 specular     = numerator / denominator;

    // Radiance (incoming light color * intensity * NdotL)
    vec3 radiance = u_LightColor * NdotL; // Assuming u_LightColor already includes intensity
    
    // Combine diffuse and specular for direct lighting
    vec3 directLighting = (kD * albedo / PI + specular) * radiance;
    
    // Ambient lighting (using AO)
    vec3 ambient = u_AmbientLightColor * albedo * ao;

    vec3 color = directLighting + ambient;

    // Gamma correction (simple approximation, if needed - typically FBOs are linear)
    // color = pow(color, vec3(1.0/2.2)); 

    FragColor = vec4(color, 1.0);
} 