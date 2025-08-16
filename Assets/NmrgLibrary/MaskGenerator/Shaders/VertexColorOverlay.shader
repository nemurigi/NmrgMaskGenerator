Shader "NmrgLibrary/NmrgMaskGenerator/SceneOverlay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OverlayColor ("Overlay Color", Color) = (1, 0.4, 0.4, 0.7)
        _WireframeWidth ("Wireframe Width", Range(0.001, 0.01)) = 0.002
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Overlay"
            "IgnoreProjector"="True"
        }
        
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            
            struct v2g
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            
            struct g2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 barycentric : TEXCOORD1;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _OverlayColor;
            float _WireframeWidth;
            
            v2g vert (appdata v)
            {
                v2g o;
                o.vertex = v.vertex;
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> output)
            {
                // マスクされた三角形のみ処理
                float mask = max(max(input[0].color.r, input[1].color.r), input[2].color.r);
                if (mask < 0.5) return;
                
                g2f o;
                
                // 1つ目の頂点
                o.vertex = UnityObjectToClipPos(input[0].vertex);
                o.color = input[0].color;
                o.uv = input[0].uv;
                o.barycentric = float3(1, 0, 0);
                output.Append(o);
                
                // 2つ目の頂点
                o.vertex = UnityObjectToClipPos(input[1].vertex);
                o.color = input[1].color;
                o.uv = input[1].uv;
                o.barycentric = float3(0, 1, 0);
                output.Append(o);
                
                // 3つ目の頂点
                o.vertex = UnityObjectToClipPos(input[2].vertex);
                o.color = input[2].color;
                o.uv = input[2].uv;
                o.barycentric = float3(0, 0, 1);
                output.Append(o);
                
                output.RestartStrip();
            }
            
            fixed4 frag (g2f i) : SV_Target
            {
                // スクリーン座標での偏微分を使用
                float3 bary = i.barycentric;
                float3 d = fwidth(bary);
                
                // 各エッジからの距離
                float3 edge = smoothstep(float3(0,0,0), d * _WireframeWidth, bary);
                float minEdge = min(min(edge.x, edge.y), edge.z);
                
                float wireframe = 1.0 - minEdge;
                
                // マスク判定
                float mask = i.color.r;
                float alpha = mask * wireframe;
                
                return float4(_OverlayColor.rgb, alpha);
            }
            ENDCG
        }
    }
}