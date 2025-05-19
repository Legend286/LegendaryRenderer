#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec4 aTangent; // Tangent XYZ, Bitangent sign W
layout (location = 3) in vec2 aTexCoords;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 FragPos_World;
out vec2 TexCoords_FS;
out mat3 TBN_FS;

void main()
{
    vec4 worldPos = model * vec4(aPos, 1.0);
    FragPos_World = worldPos.xyz / worldPos.w;

    TexCoords_FS = aTexCoords;

    // Calculate TBN matrix for transforming normal from tangent space to world space
    vec3 N_world = normalize(mat3(transpose(inverse(model))) * aNormal);
    vec3 T_world = normalize(mat3(model) * aTangent.xyz); // Transform tangent to world space
    
    // Re-orthogonalize T with respect to N in world space
    T_world = normalize(T_world - dot(T_world, N_world) * N_world);
    
    vec3 B_world = cross(N_world, T_world) * aTangent.w; // Calculate bitangent in world space with correct sign

    TBN_FS = mat3(T_world, B_world, N_world);

    gl_Position = projection * view * worldPos;
} 