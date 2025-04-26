#version 400 core

in vec2 texCoord;

uniform sampler2D sourceTexture;

layout(location = 0) out vec4 FragColor;

void main()
{
    vec4 result = texture(sourceTexture, texCoord);
    
    FragColor = result;
}