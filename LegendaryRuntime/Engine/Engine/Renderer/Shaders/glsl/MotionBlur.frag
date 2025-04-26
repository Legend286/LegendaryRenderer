#version 400 core

in vec2 texCoord;

uniform sampler2D sourceTexture;
uniform sampler2D velocityTexture;
uniform sampler2D depthTexture;

uniform mat4 viewProjection;
uniform mat4 prevViewProjection;

layout(location = 0) out vec4 FragColour;

vec3 WorldPositionFromDepth(vec2 UV)
{
    float depth = texture(depthTexture, UV).r * 2 - 1;
    vec2 screen = UV * 2 - 1;
    vec4 ndc = vec4(screen, depth, 1.0f);

    vec4 ndcToWorld = ndc * inverse(viewProjection);
    
    ndcToWorld.xyz /= ndcToWorld.w;
    
    vec3 world = ndcToWorld.xyz;
    
    return world;
}

uniform float exposureValue; // Auto exposure value

// Filmic tone mapping operator (Hable's curve)
vec3 FilmicToneMapping(vec3 colour)
{
    // Scale by exposure
    colour *= exposureValue;

    // Filmic tone mapping constants; tweak these for your desired look
    float A = 0.15;
    float B = 0.50;
    float C = 0.10;
    float D = 0.20;
    float E = 0.02;
    float F = 0.30;

    // Apply Hable's tone mapping curve
    colour = ((colour * (A * colour + C * B) + D * E) / (colour * (A * colour + B) + D * F)) - E / F;

    return colour;
}

void main()
{
    vec2 vel = clamp(texture(velocityTexture, texCoord).xy, 0, 1) * 2 - 1;
    
    vec4 blur = vec4(0);

    float depth = texture(depthTexture, texCoord).r;
 
    if(depth >= 1.0f)
    {
        vec3 worldPos = WorldPositionFromDepth(texCoord);
        
        vec4 currentPos = vec4(worldPos.xyz, 1.0f) * viewProjection;
        vec4 previousPos = vec4(worldPos.xyz, 1.0f) * prevViewProjection;

        vec2 currentPosSS = (currentPos.xy / currentPos.w) * 2.0f + 0.5f;
        vec2 previousPosSS = (previousPos.xy / previousPos.w) * 2.0f + 0.5f;

        vel = vec2(currentPosSS.xy-previousPosSS.xy);
    }
  
    for (int i = -32; i <= 32; i++)
    {
        blur += texture(sourceTexture, texCoord + float(i) / 500 * vel) / 65;
    }
    
    vec4 result = vec4(blur.xyz, 1.0f);
    
    result.xyz = FilmicToneMapping(result.xyz);
    
    FragColour = result;
}