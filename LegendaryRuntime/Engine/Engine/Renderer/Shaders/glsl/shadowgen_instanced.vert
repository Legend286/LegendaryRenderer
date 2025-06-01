#version 410 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;

// Instance data from UBO (compatible with older OpenGL versions)
layout(std140) uniform InstanceData
{
    mat4 modelMatrix[64];
    mat4 lightViewProjection[64];
    vec4 atlasScaleOffset[64];  // xy = scale, zw = offset
    vec4 tileBounds[64];        // xyxy format for tile bounds
    int lightIndex[64];
    int faceIndex[64];
};

uniform int useInstancedRendering;
uniform int instanceCount;
uniform int tileSize;

// Legacy uniforms for backward compatibility
uniform mat4 shadowViewProjection;
uniform mat4 model;

out vec2 texCoord;
out vec4 atlasInfo;    // xy = scale, zw = offset
out vec4 tileClip;     // tile bounds for clipping
flat out int lightIdx;
flat out int faceIdx;

void main()
{
    vec4 worldPos;
    mat4 viewProjMatrix;
    
    if (useInstancedRendering == 1 && gl_InstanceID < instanceCount)
    {
        // Instanced rendering path - bounds check to prevent accessing invalid data
        if (gl_InstanceID >= 64) // MAX_SHADOW_INSTANCES_PER_MESH
        {
            // Invalid instance, position far away to clip
            gl_Position = vec4(-1000.0, -1000.0, -1000.0, 1.0);
            return;
        }
        
        worldPos = vec4(aPosition, 1.0) * transpose(modelMatrix[gl_InstanceID]);
        viewProjMatrix = (lightViewProjection[gl_InstanceID]);
        
        // Pass atlas and tile information to fragment shader
        atlasInfo = atlasScaleOffset[gl_InstanceID];
        tileClip = tileBounds[gl_InstanceID];
        lightIdx = lightIndex[gl_InstanceID];
        faceIdx = faceIndex[gl_InstanceID];
        
        // Transform to shadow space
        vec4 shadowPos = worldPos * transpose(viewProjMatrix);
        
        // Convert from NDC [-1,1] to [0,1] range
        shadowPos.xy = shadowPos.xy * 0.5 + 0.5;
        
        // Apply atlas scale and bias to map to the correct tile
        shadowPos.xy = shadowPos.xy * atlasInfo.xy + atlasInfo.zw;
        
        // Convert back to NDC [-1,1] range for final output
        shadowPos.xy = shadowPos.xy * 2.0 - 1.0;
        
        shadowPos.z *= -1;
        gl_Position = shadowPos;
    }
    else
    {
        // Legacy single-instance path
        worldPos = model * vec4(aPosition, 1.0);
        viewProjMatrix = shadowViewProjection;
        atlasInfo = vec4(1.0, 1.0, 0.0, 0.0);  // Full atlas
        tileClip = vec4(0.0, 0.0, 1.0, 1.0);   // No clipping
        lightIdx = 0;
        faceIdx = -1;
        
        gl_Position = (viewProjMatrix) * worldPos;
    }
    
    texCoord = aTexCoord;
} 