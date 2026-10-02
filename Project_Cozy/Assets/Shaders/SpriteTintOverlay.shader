// 장식 설치 모드에서 들고 다니는 장식(고스트)에 쓰는 스프라이트 셰이더.
// 기획: "아이템 투명도 70%, 위에 컬러 덧입히기: #009000(가능) / #FF0000(불가) + 투명도 30%".
// SpriteRenderer.color는 곱하기라 "색을 덧입히기"가 안 된다 — 흰 부분만 그 색이 되고 어두운 부분은 그대로 남는다.
// 그래서 원본 색과 덧입힐 색을 비율로 섞는 셰이더를 따로 둔다. 머티리얼은 에셋으로 두지 않고 코드가 만든다.
Shader "Cozy/SpriteTintOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _OverlayColor ("Overlay Color", Color) = (0, 0.5647, 0, 1)
        _OverlayAmount ("Overlay Amount", Range(0, 1)) = 0.3
        _Alpha ("Alpha", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _OverlayColor;
                half  _OverlayAmount;
                half  _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                c.rgb = lerp(c.rgb, _OverlayColor.rgb, _OverlayAmount);
                c.a *= _Alpha;
                return c;
            }
            ENDHLSL
        }
    }
}
