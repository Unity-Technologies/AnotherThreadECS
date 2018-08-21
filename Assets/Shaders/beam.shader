Shader "Custom/beam"
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

			uniform float4 _Color;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				// UNITY_VERTEX_INPUT_INSTANCE_ID
				uint instanceID : SV_InstanceID;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert(appdata v)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				float4 head_pos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));
				float4 normal = mul(unity_ObjectToWorld, float4(0, 0, 1, 0));
				float3 eye = normalize(UnityWorldSpaceViewDir(head_pos).xyz);
				float3 side = normalize(cross(eye, normal));
				float3 vert = cross(eye, side);
				float length = 0.75;
				float width = 0.25;
				float3 tv = head_pos.xyz + (v.uv.y-0.5)*length*vert + (v.uv.x-0.5)*width*side;

				v2f o;
				o.vertex = UnityWorldToClipPos(tv);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 albedo = tex2D(_MainTex, i.uv);
				fixed4 col = fixed4(lerp(_Color.rgb, albedo.rgb, albedo.a), albedo.a);
				// fixed4 col = tex2D(_MainTex, i.uv) * _Color;
				return col;
			}
			ENDCG
		}
	}
}
