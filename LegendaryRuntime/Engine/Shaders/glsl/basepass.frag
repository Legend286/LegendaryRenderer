#version 440 core
out vec4 FragColor;

in vec2 velocity;
in vec2 texCoord;
in vec3 normal;

void main()
{
    float light = dot(normalize(normal.xyz), normalize(vec3(0.5,0.2,0.1)));
    FragColor = vec4(normal * 0.5f + 0.5f, 1.0f);
}
