#version 410 core

uniform float hasDiffuse;
uniform sampler2D diffuseTexture;
// uniform int tileSize; // No longer directly used by this simplified shader
// uniform int atlasResolution; // No longer directly used by this simplified shader

in vec2 texCoord;
// in vec4 atlasInfo;    // No longer used by this simplified shader if discard is out
// in vec4 tileClip;     // No longer used by this simplified shader
// flat in int lightIdx;
// flat in int faceIdx;

void main()
{
    // // Convert fragment coord to atlas UV space
    // vec2 atlasUV = gl_FragCoord.xy / float(atlasResolution);
    // 
    // // Check if fragment is within this instance's tile bounds
    // if (atlasUV.x < atlasInfo.z || atlasUV.x > (atlasInfo.z + atlasInfo.x) ||
    //     atlasUV.y < atlasInfo.w || atlasUV.y > (atlasInfo.w + atlasInfo.y))
    // {
    //     discard;
    // }
    
    // Alpha testing for materials with diffuse textures
    if (hasDiffuse > 0.0)
    {
        float alpha = texture(diffuseTexture, texCoord).a;
        if (alpha < 0.5) // common alpha threshold
        {
            discard;
        }
    }
    
    // No color output needed for depth-only shadow rendering
    // The depth buffer will automatically store the fragment depth
} 