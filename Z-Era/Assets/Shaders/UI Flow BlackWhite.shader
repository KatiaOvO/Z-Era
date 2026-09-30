Shader "Custom/UI Flow BlackWhite"
{
	Properties
	{
		[PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Header(Flow)]
		_ColorDark ("暗端颜色", Color) = (0,0,0,1)
		_ColorBright ("亮端颜色", Color) = (1,1,1,1)
		_FlowSpeed ("流速", Float) = 0.25
		_FlowScale ("波纹密度", Float) = 6
		_FlowBalance ("黑白比例(0偏黑-0.5均衡-1偏白)", Range(0,1)) = 0.5
		_FlowContrast ("明暗对比", Range(0.5,4)) = 2
		_Intensity ("效果强度(0原图-1纯流动)", Range(0,1)) = 1
		_FlowTime ("脚本驱动的流动时间(-1=用_Time)", Float) = -1

		[Toggle(_USE_SPRITE_ALPHA)] _UseSpriteAlpha ("仅Sprite不透明区域内流动", Float) = 1
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
			#pragma shader_feature_local _USE_SPRITE_ALPHA

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
			};

			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			fixed4 _ColorDark;
			fixed4 _ColorBright;
			float _FlowSpeed;
			float _FlowScale;
			float _FlowBalance;
			float _FlowContrast;
			float _Intensity;
			float _FlowTime;

			v2f vert(appdata_t IN)
			{
				v2f OUT;
				OUT.worldPosition = IN.vertex;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
				OUT.color = IN.color * _Color;
				return OUT;
			}

			// 网格随机值：同一格点结果恒定，噪声因此连续可插值
			float hash1(float2 p)
			{
				return frac(sin(dot(p, float2(127.1, 311.7)))
					* 43758.5453);
			}

			// 值噪声：格点随机值之间的平滑双线性插值
			float valueNoise(float2 p)
			{
				float2 i = floor(p);
				float2 f = frac(p);

				// fade 曲线让插值在格点处平滑，避免网格感
				float2 u = f * f * (3.0 - 2.0 * f);

				float a = hash1(i);
				float b = hash1(i + float2(1, 0));
				float c = hash1(i + float2(0, 1));
				float d = hash1(i + float2(1, 1));

				return lerp(
					lerp(a, b, u.x),
					lerp(c, d, u.x),
					u.y);
			}

			// 三个倍频的噪声叠加（fbm）：大波形上叠中小细节，
			// 波纹因此不呈现单一频率的规律感
			float fbm(float2 p)
			{
				float v = 0;
				float amp = 0.5;

				for (int octave = 0; octave < 3; octave++)
				{
					v += amp * valueNoise(p);

					// 缩放并偏移到下一个倍频，偏移量打破层间相关性
					p = p * 2.03 + float2(17.3, 9.1);
					amp *= 0.5;
				}

				return v;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				// 基色：sprite 原色（logo 本图为白色），作为
				// 强度为 0 或透明区域外的回退
				fixed4 baseColor =
					(tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd)
					* IN.color;

				// RectMask2D / 父级 Mask 的裁剪
				baseColor.a *= UnityGet2DClipping(
					IN.worldPosition.xy,
					_ClipRect);

				// 流动时间：脚本驱动（unscaled，暂停时仍流动）优先，
				// 未驱动（< 0）时回退到 _Time
				float t = _FlowTime >= 0 ? _FlowTime : _Time.y;

				float2 p = IN.texcoord * _FlowScale;

				// 两组不同密度、不同方向漂移的噪声加权叠加：
				// 波形既有大起伏又有细碎扰动，看不出平移规律
				float n1 = fbm(p +
					float2(t * _FlowSpeed, t * _FlowSpeed * 0.7));
				float n2 = fbm(p * 1.7 -
					float2(t * _FlowSpeed * 0.6, t * _FlowSpeed * 0.4));

				float n = saturate(n1 * 0.65 + n2 * 0.35);

				// 黑白比例：整体抬升/压低噪声值，0 全黑、0.5 原样、
				// 1 全白；在对比调整之前生效
				n = saturate(n + (_FlowBalance - 0.5));

				// 拉开明暗对比：大于 1 时黑白更分明，接近 0.5 时柔和
				n = saturate((n - 0.5) * _FlowContrast + 0.5);

				fixed3 flowColor = lerp(
					_ColorDark.rgb,
					_ColorBright.rgb,
					n);

				// 仅在 sprite 不透明区域内流动时，用基色 Alpha 作遮罩
				float alphaMask = 1;
				#ifdef _USE_SPRITE_ALPHA
				alphaMask = baseColor.a;
				#endif

				fixed4 col = baseColor;
				col.rgb = lerp(
					baseColor.rgb,
					flowColor,
					_Intensity * alphaMask);
				col.a = baseColor.a;
				return col;
			}
		ENDCG
		}
	}
}
