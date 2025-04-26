#version 400 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;

out vec2 texCoord;

uniform mat4 viewProjection;
uniform mat4 model;
void main()
{
    vec4 currentPos = vec4(aPosition, 1.0f) * model * viewProjection;
    currentPos.z *= -1;
    texCoord = aTexCoord;
    gl_Position = currentPos;
}