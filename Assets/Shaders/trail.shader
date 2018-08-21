Shader "Custom/trail" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
		// Blend SrcAlpha OneMinusSrcAlpha // alpha blending
        Blend SrcAlpha One

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            #define NODE_NUM 64

			#define COL_MAX 8
            float4 _color_table[COL_MAX];

            struct appdata_custom {
                uint vertexID : SV_VertexID;
                float4 vertex : POSITION;
				float4 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR0;
            };

            v2f vert(appdata_custom v)
            {
                int vid = v.vertexID;
				float4 tv = v.vertex;
				float3 eye = ObjSpaceViewDir(tv);
				float3 side = normalize(cross(eye, v.normal));
                float size = 0.2;
                float dir = (vid&0x1)==0 ? -1 : 1;
                tv.xyz += side * size * dir;
                float2 uv = float2((vid&0x1)==0 ? 0 : 1, vid%(NODE_NUM*0x2)<0x2 ? 0 : 0.5);
                int colidx = (int)v.texcoord.x;
                float alpha = v.texcoord.y;

                v2f o;
                o.pos = UnityObjectToClipPos(tv);
                o.texcoord = TRANSFORM_TEX(uv, _MainTex);
                o.color = float4(_color_table[colidx].rgb, alpha);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.texcoord) * i.color;
            }

            ENDCG
        }
    }
}
