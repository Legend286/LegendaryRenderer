#version 330 core
out vec4 FragColor;

in vec3 FragPos_World;
in vec3 Normal_World;
in vec2 TexCoords_VS;

uniform sampler2D diffuseTexture;
uniform bool hasDiffuseTexture;

// Simple hardcoded lighting for icons
uniform vec3 lightDir_World = normalize(vec3(0.6, 0.7, 0.8)); 
uniform vec3 lightColor = vec3(1.0, 1.0, 0.95); // Slightly warm white
uniform vec3 ambientColor = vec3(0.25, 0.25, 0.3);
uniform vec3 defaultColor = vec3(0.7, 0.7, 0.7); // Default color if no texture

void main()
{
    vec3 norm = normalize(Normal_World);
    float diff = max(dot(norm, lightDir_World), 0.0);
    vec3 diffuse = diff * lightColor;
    
    vec3 surfaceColor = defaultColor;
    if (hasDiffuseTexture)
    {
        surfaceColor = texture(diffuseTexture, TexCoords_VS).rgb;
    }

    vec3 result = (ambientColor + diffuse) * surfaceColor;
    FragColor = vec4(result, 1.0);
} 