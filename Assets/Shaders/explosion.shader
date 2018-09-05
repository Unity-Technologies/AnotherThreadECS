Shader "Custom/explosion"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
   		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		ZWrite Off
		Cull Off
		Blend SrcAlpha One 				// alpha additive

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			float _CurrentTime;

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert(appdata v)
			{
				UNITY_SETUP_INSTANCE_ID(v);
// #if defined(UNITY_INSTANCING_ENABLED)
// 				float elapsed = _CurrentTime - _StartTimes[unity_InstanceID];
// #else
// 				float elapsed = 0.5;
// #endif
                float start_time = unity_ObjectToWorld._m30;
                float elapsed = _CurrentTime - start_time;
                
				// float4 pos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));
				float4 pos = unity_ObjectToWorld._m03_m13_m23_m33;
				float3 up = float3(0, 1, 0);
				float3 eye = normalize(UnityWorldSpaceViewDir(pos).xyz);
				float3 side = normalize(cross(eye, up));
				float3 vert = cross(eye, side);
				float width = 4;
				float3 tv = pos.xyz + (v.uv.y-0.5)*width*vert + (v.uv.x-0.5)*width*side;
				float rW = 1.0/8.0;
				float rH = 1.0/8.0;
				float fps = 60;
				float loop0 = 1.0/(fps*rW*rH);
				elapsed = clamp(elapsed, 0, loop0);
				float texu = floor(elapsed * fps) * rW - floor(elapsed*fps*rW);
				float texv = 1 - floor(elapsed * fps * rW) * rH;
				texu += v.uv.x * rW;
				texv += -v.uv.y * rH;

				v2f o;
				o.vertex = UnityWorldToClipPos(float4(tv, 1));
				o.uv = TRANSFORM_TEX(float4(texu, texv, 0, 0), _MainTex);
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
