#version 400 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 shadowViewProjection;
uniform int UseInstancing;
uniform mat4[6] shadowInstanceMatrices;
uniform mat4 model;

out vec2 texCoord;

void main()
{
    mat4 viewProjShadow = shadowViewProjection;
    
    if(UseInstancing == 1)
    {
        // Use instancing for shadow generation
        viewProjShadow = shadowInstanceMatrices[gl_InstanceID];
    }
    
    vec4 currentPos = vec4(aPosition, 1.0f) * model * viewProjShadow;
    
    texCoord = aTexCoord;
    currentPos.z *= -1;
    gl_Position = currentPos;
}