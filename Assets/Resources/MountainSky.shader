Shader "Wuxia/Mountain Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct SkyVertex
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            SkyVertex Vert(float4 position : POSITION)
            {
                SkyVertex output;
                output.position = UnityObjectToClipPos(position);
                output.direction = position.xyz;
                return output;
            }

            half4 Frag(SkyVertex input) : SV_Target
            {
                half altitude = normalize(input.direction).y;
                half gradient = smoothstep(-0.65h, 0.60h, altitude);
                return half4(lerp(half3(.62h, .69h, .73h), half3(.30h, .46h, .61h), gradient), 1);
            }
            ENDCG
        }
    }
}
