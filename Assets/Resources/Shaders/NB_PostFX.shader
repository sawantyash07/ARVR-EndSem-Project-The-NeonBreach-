// Neon Breach post-processing for the Built-in Render Pipeline (no URP needed):
// pass 0 bloom prefilter (soft-knee threshold + 2x downsample), pass 1 downsample, pass 2 additive tent upsample,
// pass 3 composite: chromatic aberration, bloom, exposure, highlight soft-clip, contrast, saturation, split-tone grade,
// tint (slow-motion / double-damage) and vignette.
Shader "NeonBreach/NB_PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _BloomTex;
    float4 _MainTex_TexelSize;
    float _Threshold;
    float _Knee;
    float _Intensity;
    float _Saturation;
    float _Contrast;
    float _Vignette;
    float _Chroma;
    float _Exposure;
    float4 _Tint;
    float4 _Lift;
    float4 _Gain;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half b = max(c.r, max(c.g, c.b));
        half soft = b - _Threshold + _Knee;
        soft = clamp(soft, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 0.00001);
        half contrib = max(soft, b - _Threshold) / max(b, 0.00001);
        return c * contrib;
    }

    half4 FragPrefilter(v2f i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
    half4 FragDown(v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
    half4 FragUp(v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }

    // Highlight-only soft clip: leaves darks and mid-tones untouched (the project uses gamma colour space,
    // where a full filmic curve would lift the shadows and wash the image out) and rolls HDR values off to 1.
    half3 SoftClip(half3 x)
    {
        half3 e = max(x - 0.8, 0);
        return min(x, 0.8) + e / (1 + e * 5);
    }

    half4 FragComposite(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 dc = uv - 0.5;
        half3 col;
        if (_Chroma > 0.001)
        {
            float2 off = dc * _Chroma * 0.025;
            col.r = tex2D(_MainTex, uv - off).r;
            col.g = tex2D(_MainTex, uv).g;
            col.b = tex2D(_MainTex, uv + off).b;
        }
        else col = tex2D(_MainTex, uv).rgb;

        col += tex2D(_BloomTex, uv).rgb * _Intensity;
        col *= _Exposure;
        col = SoftClip(col);

        col = (col - 0.5) * _Contrast + 0.5;
        half l = dot(col, half3(0.2126, 0.7152, 0.0722));
        col = lerp(l.xxx, col, _Saturation);
        col = col * _Gain.rgb + _Lift.rgb * (1 - col);
        col = lerp(col, col * _Tint.rgb, _Tint.a);

        float v = 1 - dot(dc, dc) * _Vignette * 2;
        col *= saturate(v);
        return half4(saturate(col), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragComposite
            ENDCG
        }
    }
    Fallback Off
}
