Shader "Custom/sprite" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
		Blend SrcAlpha OneMinusSrcAlpha // alpha blending
        // Blend SrcAlpha One

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            // #pragma target 3.5
            // #pragma multi_compile_instancing
            #pragma multi_compile INSTANCING_ON

            #ifndef INSTANCING_ON
            #error
            #endif

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            #define BULK_NUM 1023
			#define TYPE_MAX 3
			#define COL_MAX 8
            float4 _uvscale_table[TYPE_MAX*2];
            
            struct appdata_custom {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
                float4 vertex : POSITION;
#if defined(SHADER_API_PSSL)
                float2 texcoord : TEXCOORD0;
#endif
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 texcoord : TEXCOORD0;
#if defined(SHADER_API_PSSL)
                float2 texcoord1 : TEXCOORD1;
#endif
                float4 color : COLOR0;
            };

            /*
             * Model Matrix (4x4)
             *        0,    1,    2,    3
             * 0:  type,(N/A),(N/A),pos.x
             * 1: (N/A),(N/A),(N/A),pos.y
             * 2: (N/A),(N/A),(N/A),pos.z
             * 3: col.r,col.g,col.b,col.a
             */

            v2f vert(appdata_custom v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                // int id = UNITY_GET_INSTANCE_ID(v);
                int vid = v.vertexID;

                float4x4 model_matrix = unity_ObjectToWorld;
                int type = (int)model_matrix._m00;
                float2 scale = _uvscale_table[type*0x2+0x1].xy;
                float3 vec = float3(v.vertex.xy * scale, v.vertex.z);
                v2f o;

                float3 model_pos = model_matrix._m03_m13_m23;
                o.pos = mul(UNITY_MATRIX_VP, float4(model_pos + vec, 1));
                float4 uv0 = _uvscale_table[type*0x2+0x0];
                // float2 uv = float2((vid&0x1) == 0 ? uv0.x : uv0.z, (vid/2) == 0 ? uv0.y : uv0.w); // doesn't work.
                float2 uv = float2((vid&0x1) == 0 ? uv0.x : uv0.z, (vid/0x2) == 0 ? uv0.y : uv0.w); // works correctly.
                    
                o.texcoord = TRANSFORM_TEX(uv, _MainTex);
#if defined(SHADER_API_PSSL)
                o.texcoord1 = v.texcoord;
#endif
                o.color = model_matrix._m30_m31_m32_m33;
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
