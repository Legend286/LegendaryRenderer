#version 440 core
out vec4 FragColor;

in vec2 velocity;
in vec2 texCoord;
in vec3 normal;

void main()
{
    float light = dot(normalize(normal.xyz), normalize(vec3(0,0,-1)));
    FragColor = vec4(light,light,light, 1.0f);
}
