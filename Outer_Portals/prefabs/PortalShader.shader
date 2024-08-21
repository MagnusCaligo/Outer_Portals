Shader "Portal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ViewportRect ("Viewport Rect", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _ViewportRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;

                //uv.x = ((uv.x * (_ScreenParams.z)) * (_ViewportRect.z )) + _ViewportRect.x;
                //uv.y = ((uv.y * (_ScreenParams.w)) * (_ViewportRect.w )) + _ViewportRect.y;

                uv.x = ((uv.x - _ViewportRect.x) * (_ViewportRect.z));
                uv.y = ((uv.y - _ViewportRect.y) * (_ViewportRect.w));

                fixed4 col = tex2D(_MainTex, uv);
                return col;
            }
            ENDCG
        }
    }
}
