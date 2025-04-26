#version 400 core

uniform uint guid0;
uniform uint guid1;
uniform uint guid2;
uniform uint guid3;

out vec4 fragColour;

void main()
{
    float r = uintBitsToFloat(guid0);
    float g = uintBitsToFloat(guid1);
    float b = uintBitsToFloat(guid2);
   // float a = uintBitsToFloat();

    fragColour = vec4(r, g, b, 1);
}
