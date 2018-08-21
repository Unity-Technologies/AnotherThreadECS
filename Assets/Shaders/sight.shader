Shader "Custom/sight" {
	SubShader {
   		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		ZWrite Off
		Cull Off
        Blend SrcAlpha OneMinusSrcAlpha // alpha blending
		// Blend SrcAlpha One 				// alpha additive
		
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
			float _StartTimes[1023];
            float _DT;

            v2f vert(appdata v)
            {
				UNITY_SETUP_INSTANCE_ID(v);
#if defined(UNITY_INSTANCING_ENABLED)
				float elapsed = _CurrentTime - _StartTimes[unity_InstanceID];
#else
				float elapsed = 1;
#endif
                float dt = max(_DT, 1.0/60);

                float4x4 model_matrix = unity_ObjectToWorld;
                float3 model_pos = float3(model_matrix._m00_m10, 0);
                // float3 prev_model_pos = model_pos;
                float3 prev_model_pos = float3(model_matrix._m01_m11, 0);

                int vid = v.vertexID;
                int outflg = (vid/0x4)%0x2;
                int ydir = ((vid/0x2)%0x2)*2-1;
                int xdir = (vid%0x2)*2-1;
                
                float size = 1500/2;
                float min_size = 16;
                float time = 0.3;
                float radius = max((time - elapsed)*size, min_size);
                float prev_radius = max((time - (elapsed - dt*1))*size, min_size);
                float3 tv_in = float3(radius*xdir,
                                      radius*ydir,
                                      0);
                float3 tv_out = float3(prev_radius*xdir,
                                       prev_radius*ydir,
                                       0);
                float3 rect = lerp(tv_in, tv_out, outflg);
                float3 pos = lerp(model_pos, prev_model_pos, outflg);
				v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + rect, 1));
				o.color = float4(model_matrix._m03_m13_m23, 1*((1-outflg)*0.5));
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
