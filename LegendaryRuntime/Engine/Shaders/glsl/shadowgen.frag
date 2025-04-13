#version 400 core

uniform float hasDiffuse;
uniform sampler2D diffuseTexture;

in vec2 texCoord;

void main()
{
    if(hasDiffuse > 0)
    {
        if(texture(diffuseTexture, texCoord).a < 0.5f)
        {
            discard;
        }
    }
    
}
