Shader "UI/AlphaOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0,1,0,1)
        _OutlineWidth ("Outline Width (px)", Range(0,8)) = 2

        // Unity UI Mask / Stencil support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UI clipping
                #ifdef UNITY_UI_CLIP_RECT
                i.color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                // If we're outside sprite alpha, try draw outline
                float a = c.a;
                if (a <= 0.001 && _OutlineWidth > 0.0)
                {
                    float2 stepUV = _MainTex_TexelSize.xy * _OutlineWidth;
                    // 8-neighborhood max alpha
                    float maxA = 0.0;
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2( stepUV.x, 0)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2(-stepUV.x, 0)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2(0,  stepUV.y)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2(0, -stepUV.y)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2( stepUV.x,  stepUV.y)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2(-stepUV.x,  stepUV.y)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2( stepUV.x, -stepUV.y)).a);
                    maxA = max(maxA, tex2D(_MainTex, i.uv + float2(-stepUV.x, -stepUV.y)).a);

                    if (maxA > 0.001)
                    {
                        fixed4 oc = _OutlineColor;
                        oc.a *= i.color.a; // respect vertex alpha
                        return oc;
                    }
                }

                return c;
            }
            ENDCG
        }
    }
}


