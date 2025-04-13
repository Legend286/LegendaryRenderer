#version 400 core

in vec2 texCoord;  // Texture coordinates (or screen space coordinates)
uniform mat4 view;
uniform mat4 proj;

out vec4 FragColor;

uniform vec3 sunDirection;  // Direction of the sun (normalized)
uniform vec3 zenithColor;   // Color of the zenith (sky top)
uniform vec3 horizonColor;  // Color of the horizon (sky bottom)
uniform vec3 sunColor;      // Color of the sun
uniform float sunSize;      // Size of the sun disk (in radians)
uniform float horizonHeight; // Height where the horizon color starts

const float PI = 3.14159265359;

// Sun disk intensity
float SunDisk(float cosTheta, float sunSize)
{
    return smoothstep(cos(sunSize), cos(sunSize + 0.05), cosTheta);
}

void main()
{
    // Convert screen space coordinates to view direction
    mat4 invProj = inverse(proj);
    vec4 screenToWorld = vec4(texCoord.xy * 2.0 - 1.0, 1.0, 1.0) * invProj;
    screenToWorld.xyz /= screenToWorld.w;
    screenToWorld = screenToWorld * inverse(view);

    vec3 viewDir = normalize(screenToWorld.xyz);

    // Calculate the cosine of the angle between the view direction and the zenith (up direction)
    float cosTheta = pow(clamp(dot(viewDir, vec3(0.0, 1.0, 0.0)),0,1), horizonHeight);  // Assuming the zenith is along the Y-axis

    // Interpolate between zenith and horizon colors based on the view direction
    vec3 skyColor = mix(horizonColor, zenithColor, cosTheta * 0.5 + 0.5);

    // Calculate the sun disk intensity
    float sunDiskIntensity = 1-SunDisk(dot(viewDir, sunDirection), sunSize);

    // Add the sun color to the sky color, based on the sun disk intensity
    vec3 finalColor = skyColor + sunColor * sunDiskIntensity;

    // Output the final color
    FragColor = vec4(finalColor, 1.0);
}

