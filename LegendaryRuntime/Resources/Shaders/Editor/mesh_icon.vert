#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 3) in vec2 aTexCoords; // Note: Location 3, as per our 12-float layout (Pos, Norm, Tan4, UV)

out vec3 FragPos_World;
out vec3 Normal_World;
out vec2 TexCoords_VS;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    FragPos_World = vec3(model * vec4(aPos, 1.0));
    Normal_World = mat3(transpose(inverse(model))) * aNormal; // Transform normal to world space
    TexCoords_VS = aTexCoords;
    gl_Position = projection * view * vec4(FragPos_World, 1.0);
} 