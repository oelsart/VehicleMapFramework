#version 450

layout(location = 0) in vec4 vColor;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _RestoreFactor; // @default = 1.0
    float _MaxRestore; // @default = 0.5
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _GrabVMFLightOverlay;

void main()
{
    vec2 grabUv = gl_FragCoord.xy / textureSize(_GrabVMFLightOverlay, 0);

    vec4 bg = texture(_GrabVMFLightOverlay, grabUv);

    // 除算
    vec3 shadowColor = max(uMaterial._Color.rgb, 0.001);
    vec3 restored = bg.rgb / shadowColor * uMaterial._RestoreFactor;

    // 白飛び防止
    restored = min(restored, uMaterial._MaxRestore);
    outColor = vec4(restored, vColor.a);
}