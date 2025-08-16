Shader "NmrgLibrary/NmrgMaskGenerator/MaskPadding"
{
    Properties
    {
        _MainTex ("Mask Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            
            fixed4 frag (v2f_img i) : SV_Target
            {
                // 中心ピクセルがマスクされていれば早期リターン
                fixed4 center = tex2D(_MainTex, i.uv);
                if (center.r > 0.5)
                {
                    return center;
                }

                float4 col = 0;
                
                // 3x3近傍をチェック（中心除く）
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }
                        
                        float2 offset = float2(dx, dy) * _MainTex_TexelSize.xy;
                        fixed4 neighbor = tex2D(_MainTex, i.uv + offset);
                    
                        if (neighbor.r > 0.5)
                        {
                            col = 1;
                        }
                    }
                }
                
                return col; // 黒色（非マスク）
            }
            ENDCG
        }
    }
}