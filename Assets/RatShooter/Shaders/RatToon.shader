// Rat Shooter - stylised "realistic cartoon" lighting for the Built-in Render Pipeline.
// Soft two-tone light ramp (real shadow maps from the sun), tinted cartoon shadows, a crisp specular highlight,
// rim light, vertex colours (macro colour / ambient occlusion) and optional wind sway for crops and grass
// (vertex colour alpha = how much a vertex may sway). GPU instancing for _Color.
Shader "RatShooter/Toon"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _ShadeTint ("Shadow Tint", Color) = (0.62,0.68,0.95,1)
        _RampSmooth ("Ramp Softness", Range(0.01,1)) = 0.22
        _RampOffset ("Ramp Offset", Range(-0.6,0.6)) = 0.0
        _HighlightColor ("Highlight Colour", Color) = (1,1,1,1)
        _Gloss ("Highlight Size", Range(2,256)) = 40
        _SpecAmount ("Highlight Strength", Range(0,1)) = 0.15
        _RimAmount ("Rim Light", Range(0,1)) = 0.3
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _VertexColorAmount ("Vertex Colour", Range(0,1)) = 1
        _Wind ("Wind Sway", Range(0,0.6)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _FogAmount ("Fog Amount", Range(0,1)) = 1
        _FlipBackNormals ("Flip Back-face Normals", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Lighting.cginc"
    #include "AutoLight.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    fixed4 _ShadeTint;
    half _RampSmooth;
    half _RampOffset;
    fixed4 _HighlightColor;
    half _Gloss;
    half _SpecAmount;
    half _RimAmount;
    half _RimPower;
    half _VertexColorAmount;
    half _Wind;
    fixed4 _EmissionColor;
    half _FogAmount;
    half _FlipBackNormals;

    UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
    UNITY_INSTANCING_BUFFER_END(Props)

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        fixed4 color : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    // gentle two-frequency gust, phase varies over the world so a field ripples
    void ApplyWind(inout float4 vertex, fixed4 color)
    {
        if (_Wind > 0.0001)
        {
            float3 wp = mul(unity_ObjectToWorld, vertex).xyz;
            float t = _Time.y;
            float s = sin(t * 1.6 + wp.x * 0.31 + wp.z * 0.23) * 0.7 + sin(t * 3.7 + wp.x * 1.3 + wp.z * 0.9) * 0.3;
            float3 off = float3(s, 0, s * 0.55) * (_Wind * color.a);
            vertex.xyz += mul((float3x3)unity_WorldToObject, off);
        }
    }

    // forward-lighting code only (the shadow caster variants define SHADOWS_DEPTH / SHADOWS_CUBE and have no shadow coords)
    #if !defined(UNITY_PASS_SHADOWCASTER) && !defined(SHADOWS_DEPTH) && !defined(SHADOWS_CUBE)
    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 worldNormal : TEXCOORD1;
        float3 worldPos : TEXCOORD2;
        fixed4 color : COLOR;
        half3 ambient : TEXCOORD3;
        SHADOW_COORDS(4)
        UNITY_FOG_COORDS(5)
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_TRANSFER_INSTANCE_ID(v, o);
        ApplyWind(v.vertex, v.color);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        o.pos = UnityWorldToClipPos(o.worldPos);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.worldNormal = UnityObjectToWorldNormal(v.normal);
        o.color = lerp(fixed4(1, 1, 1, 1), fixed4(v.color.rgb, 1), _VertexColorAmount);
        o.ambient = ShadeSH9(float4(o.worldNormal, 1));
        TRANSFER_SHADOW(o);
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    half3 ToonLight(v2f i, half atten, fixed3 albedo, bool basePass, fixed facing)
    {
        float3 n = normalize(i.worldNormal) * (facing > 0 ? 1 : lerp(1, -1, _FlipBackNormals));
        float3 l = normalize(UnityWorldSpaceLightDir(i.worldPos));
        float3 vdir = normalize(UnityWorldSpaceViewDir(i.worldPos));
        half ndl = dot(n, l);
        half ramp = smoothstep(_RampOffset - _RampSmooth, _RampOffset + _RampSmooth, ndl);
        half lit = ramp * atten;

        half3 h = normalize(l + vdir);
        half spec = pow(saturate(dot(n, h)), _Gloss);
        spec = smoothstep(0.35, 0.65, spec) * _SpecAmount * lit;

        half3 col = albedo * _LightColor0.rgb * lit + _HighlightColor.rgb * _LightColor0.rgb * spec;
        if (basePass)
        {
            // cartoon shadows: ambient, pushed towards a cool tint where the sun does not reach
            col += albedo * i.ambient * lerp(_ShadeTint.rgb, half3(1, 1, 1), lit);
            half rim = pow(1 - saturate(dot(n, vdir)), _RimPower) * _RimAmount;
            col += rim * lerp(half3(1, 1, 1), albedo, 0.45) * lerp(i.ambient, _LightColor0.rgb, 0.5) * (0.35 + 0.65 * lit);
        }
        return col;
    }
    #endif
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBase
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            fixed4 fragBase(v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 tex = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * i.color;
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                half3 col = ToonLight(i, atten, tex.rgb, true, facing > 0 ? 1 : -1) + _EmissionColor.rgb;
                fixed4 c = fixed4(col, 1);
                fixed4 fogged = c;
                UNITY_APPLY_FOG(i.fogCoord, fogged);
                return lerp(c, fogged, _FogAmount);
            }
            ENDCG
        }

        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdd
            #pragma target 3.0
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            fixed4 fragAdd(v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 tex = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * i.color;
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                fixed4 c = fixed4(ToonLight(i, atten, tex.rgb, false, facing > 0 ? 1 : -1), 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 3.0
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            struct v2fs
            {
                V2F_SHADOW_CASTER;
            };

            v2fs vertShadow(appdata v)
            {
                v2fs o;
                UNITY_SETUP_INSTANCE_ID(v);
                ApplyWind(v.vertex, v.color);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragShadow(v2fs i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}
