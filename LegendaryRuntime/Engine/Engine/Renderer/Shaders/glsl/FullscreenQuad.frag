#version 400 core

in vec2 texCoord;
out vec4 FragColour;

void main()
{
    FragColour = vec4(texCoord.xy, 0.0f, 1.0f);
}