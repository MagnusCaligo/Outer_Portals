Shader "Portal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _ViewportRect ("Viewport Rect", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        CGPROGRAM
		#pragma surface surf Lambert

        sampler2D _MainTex;
        float4 _ViewportRect;

        struct Input
        {
            float4 screenPos;
        };

		void surf (Input i, inout SurfaceOutput o)
        {
            float2 uv = i.screenPos.xy / i.screenPos.w;

            //uv.x = ((uv.x * (_ScreenParams.z)) * (_ViewportRect.z )) + _ViewportRect.x;
            //uv.y = ((uv.y * (_ScreenParams.w)) * (_ViewportRect.w )) + _ViewportRect.y;

            uv.x = ((uv.x - _ViewportRect.x) * (_ViewportRect.z));
            uv.y = ((uv.y - _ViewportRect.y) * (_ViewportRect.w));

            fixed4 col = tex2D(_MainTex, uv);
            o.Emission = col;
        }
        ENDCG
    }
}
