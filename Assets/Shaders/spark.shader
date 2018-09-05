Shader "Custom/spark" {
	SubShader {
   		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		ZWrite Off
		Blend SrcAlpha One 				// alpha additive
		
        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
 			#pragma target 3.0
			#pragma multi_compile_instancing
 			#include "UnityCG.cginc"

 			struct appdata
            {
				float4 vertex : POSITION;
				uint instanceID : SV_InstanceID;
                uint vertexID : SV_VertexID;
#if defined(SHADER_API_PSSL)
                float2 texcoord : TEXCOORD0;
#endif
			};

 			struct v2f
 			{
 				float4 pos : SV_POSITION;
 				fixed4 color : COLOR;
#if defined(SHADER_API_PSSL)
                float2 texcoord1 : TEXCOORD1;
#endif
 			};
 			
			float4 _Color;
			float _CurrentTime;
            float _DT;
 			float4x4 _PrevInvMatrix;

            v2f vert(appdata v)
            {
				UNITY_SETUP_INSTANCE_ID(v);
                float start_time = unity_ObjectToWorld._m30;
                float elapsed = _CurrentTime - start_time;
                float edge = v.vertexID % 2;

                float flow_z = -48/2;
                float speed = 480;
            	float3 tv0 = v.vertex.xyz;
                tv0 *= elapsed * speed * _DT;
                tv0.z += elapsed * flow_z;
                float4 v0 = mul(UNITY_MATRIX_VP, float4(mul(unity_ObjectToWorld, float4(tv0, 1.0)).xyz, 1));

                float3 tv1 = v.vertex.xyz;
                tv1 *= (elapsed-_DT) * speed * _DT;
                tv1.z += (elapsed-_DT) * flow_z;
                tv1 = mul(UNITY_MATRIX_V, float4(mul(unity_ObjectToWorld, float4(tv1, 1.0)).xyz, 1)).xyz;
                float4 v1 = mul(_PrevInvMatrix, float4(tv1, 1));
                v1 = mul(UNITY_MATRIX_P, v1);
                
                float4 v01 = lerp(v0, v1, edge);

				v2f o;
				o.pos = v01;
				o.color = float4(_Color.rgb, (1-edge)*(1-elapsed));
#if defined(SHADER_API_PSSL)
                o.texcoord1 = v.texcoord;
#endif

            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }

            ENDCG
        }
    }
}
