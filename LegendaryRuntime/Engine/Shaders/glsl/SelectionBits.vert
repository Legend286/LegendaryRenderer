#version 400 core
layout(location = 0) in vec3 aPosition;

uniform mat4 viewProjection;
uniform mat4 model;

void main()
{
    vec4 currentPos = vec4(aPosition, 1.0f) * model * viewProjection;
    
    currentPos.z *= -1;
    
    gl_Position = currentPos;
}