#version 400 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 shadowViewProjection;
uniform mat4 model;

out vec2 texCoord;

void main()
{
    vec4 currentPos = vec4(aPosition, 1.0f) * model * shadowViewProjection;
    
    texCoord = aTexCoord;
    currentPos.z *= -1;
    gl_Position = currentPos;
}