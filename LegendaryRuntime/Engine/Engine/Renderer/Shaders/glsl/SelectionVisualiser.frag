#version 400 core

in vec2 texCoord;
out vec4 FragColour;

uniform sampler2D selectionMask;
uniform sampler2D selectionDepth;
uniform sampler2D sceneColour;
uniform sampler2D sceneDepth;
uniform sampler2D selectionTexture;
// xy = width, height, zw = 1/width, 1/height
uniform vec4 screenDimensions;

void main()
{
    float selMask  = texture(selectionMask, texCoord).r;
    float selDepth = texture(selectionDepth, texCoord).r;

   
    vec2 texelSize = 1.0 / screenDimensions.xy;

   
    float visibleEdge = 0.0f;
    float occludedEdge = 0.0f;
    float fill = 0.0f;

    const vec2 offsets[8] = vec2[](
    vec2(-1, -1), vec2( 1,  1), vec2(-1,  1), vec2( 1, -1),
    vec2( 1,  0), vec2(-1,  0), vec2( 0,  1), vec2( 0, -1)
    );

    // Loop through each neighbor.
    for (int i = 0; i < 8; i++)
    {
       
        vec2 sampleCoord = texCoord + offsets[i] * texelSize;

      
        float neighborMask = texture(selectionMask, sampleCoord).r;
        
        float testDepth = texture(sceneDepth, texCoord).r;

        if(selDepth >= testDepth + 0.000001f)
        {
            fill = 0.5f * selMask;
        }
        
        fill *= texture(selectionTexture, texCoord.xy * (screenDimensions.xy / 4)).r;
        
        if (selMask < neighborMask)
        {
            float neighborSceneDepth = texture(sceneDepth, sampleCoord).r;
            
            if (selDepth <= neighborSceneDepth + 0.000001)
            {
                visibleEdge = 1.0f;
            }
            else
            {
                occludedEdge = 1;
            }
        }
    }

    vec4 sceneCol = texture(sceneColour, texCoord);

   
    vec4 outlineColour  = vec4(192, 63, 99, 255.0) / 255.0f;
    vec4 interiorColour = vec4(204, 102, 130, 255.0) / 255.0f;

    if (visibleEdge > 0.1f)
    {
        FragColour = outlineColour;
    }

    else if (occludedEdge > 0.1f)
    {
        FragColour = interiorColour;
    }
    else
    {
        FragColour = mix(sceneCol, interiorColour, 0.5f * fill);
    }
}