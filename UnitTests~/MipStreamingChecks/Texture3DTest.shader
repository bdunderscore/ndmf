Shader "Hidden/NDMF/Texture3DTest"
{
    Properties
    {
        _VolumeTex("Volume Texture", 3D) = "" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler3D _VolumeTex;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }

            fixed4 frag(v2f i) : SV_Target { return fixed4(1,1,1,1); }
            ENDCG
        }
    }
}
