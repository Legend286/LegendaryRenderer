#version 330 core
out vec4 FragColor;

in vec2 velocity;

void main()
{
    FragColor = vec4(velocity.xy, 0, 1.0f);
}
