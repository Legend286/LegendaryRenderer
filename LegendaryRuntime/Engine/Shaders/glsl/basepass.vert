#version 440 core
layout (location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTextureCoordinate;
layout(location = 2) in vec3 aNormal;



uniform mat4 viewProjection;
uniform mat4 model;
uniform mat4 prevModel;
uniform mat4 prevViewProjection;

out vec2 texCoord;
out vec3 normal;
out vec2 velocity;

void main()
{
//
    vec4 currentPos = vec4(aPosition, 1.0f) * model * viewProjection;
    vec4 previousPos = vec4(aPosition, 1.0f) * prevModel * prevViewProjection;
    
    vec2 currentPosSS = (currentPos.xy / currentPos.w) * 2.0f + 0.5f;
    vec2 previousPosSS = (previousPos.xy / previousPos.w) * 2.0f + 0.5f;
    
    velocity = (previousPosSS - currentPosSS) * 0.5f + 0.5f;
//

    mat4 inv = inverse(model);

    normal = aNormal * mat3(transpose(inverse(model)));
    texCoord = aTextureCoordinate;
    gl_Position = currentPos;
}