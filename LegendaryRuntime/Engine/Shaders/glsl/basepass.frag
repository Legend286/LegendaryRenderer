#version 400 core

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 Normal;
layout(location = 2) out vec2 Velocity;

in vec3 velocity;
in vec3 velocityCamOnly;

in vec2 texCoord;
in vec3 normal;
in vec4 tangent;

uniform mat4 viewProjection;
uniform mat4 model;
uniform mat4 prev;
uniform mat4 prevViewProjection;

uniform vec3 albedoColour;
uniform vec3 materialParameters;

in vec3 worldPos;
in vec2 pos;

uniform float hasDiffuse;
uniform sampler2D diffuseTexture;

uniform float hasNormal;
uniform sampler2D normalTexture;

uniform float hasRoughness;
uniform sampler2D roughnessTexture;


void main()
{
    // Reconstruct TBN matrix
    vec3 T = normalize(tangent.xyz);
    vec3 N = normalize(normal);
    vec3 B = normalize(cross(N, T)) * tangent.w;
    mat3 tbn = mat3(T, B, N);

    vec3 tangentNormal = vec3(0,0,1);
    if(hasNormal > 0)
    {
        // Transform tangent-space normal to world space
        tangentNormal = normalize(texture(normalTexture, texCoord).rgb * 2.0 - 1.0);
        tangentNormal.g = -tangentNormal.g;
    }
    
    vec4 diffuse = vec4(1,1,1,1);
    
    if(hasDiffuse > 0)
    {
        diffuse = texture(diffuseTexture, texCoord).rgba;
        if(diffuse.a < 0.5f)
        {
            discard;
        }
    }
    
    diffuse.rgb *= albedoColour;
    
    vec4 roughness = vec4(1,0.2,0,1);
    if(hasRoughness > 0)
    {
        roughness = texture(roughnessTexture, texCoord).rgba;
    }
    
    vec3 norm = normalize(tbn * tangentNormal);
    
    if(tangent.w > 8)
    {
        norm = N;
    }
    
    vec4 currentPos = vec4(worldPos.xyz, 1.0f) * (model) * viewProjection;
    vec4 previousPos = vec4(worldPos.xyz, 1.0f) * (prev) * prevViewProjection;
    
    vec2 currentPosSS = (currentPos.xy / currentPos.w) * 2.0f + 0.5f;
    vec2 previousPosSS = (previousPos.xy / previousPos.w) * 2.0f + 0.5f;
    
    Velocity = vec2(currentPosSS.xy-previousPosSS.xy) * 0.5 + 0.5f;
    
    Normal.xyz = normalize(norm) * 0.5f + 0.5f;
    Normal.w = roughness.y;

    FragColor = vec4(diffuse.rgb, roughness.z);
}