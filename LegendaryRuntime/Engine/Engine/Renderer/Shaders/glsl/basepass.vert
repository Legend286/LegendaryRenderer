#version 400 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec4 aTangent;
layout(location = 3) in vec2 aTextureCoordinate;

uniform mat4 viewProjection;
uniform mat4 prevViewProjection;
uniform mat4 model;
uniform mat4 prev;


out vec2 texCoord;
out vec3 normal;
out vec4 tangent;
out vec3 worldPos;
out vec2 pos;

void main()
{
    vec4 currentPos = vec4(aPosition, 1.0f) * model * viewProjection;
    
    normal = aNormal * mat3(transpose(inverse(model)));
    tangent.xyz = aTangent.xyz * mat3(transpose(inverse(model)));
    tangent.w = aTangent.w;
   
    vec4 posW = vec4(aPosition,1.0f);
    worldPos = posW.xyz;
    
    texCoord = aTextureCoordinate;
    currentPos.z *= -1;
    
    
    gl_Position = currentPos;
}