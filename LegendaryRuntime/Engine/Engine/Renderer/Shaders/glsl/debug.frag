#version 400 core

layout(location = 0) out vec4 FragColour;

uniform float hasDiffuse;
uniform sampler2D diffuseTexture;

in vec2 texCoord;

void main()
{   
    float mask = 1;
    if(hasDiffuse > 0)
    {
        if(texture(diffuseTexture, texCoord).a < 0.5)
        {
            discard;
        }
    }
    
    FragColour = vec4(1,1,1,1); // White color
}
