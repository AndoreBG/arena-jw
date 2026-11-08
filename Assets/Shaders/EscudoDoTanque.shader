// Escudo do tanque — shader autoral do trabalho.
//
// Efeito: quando o tanque leva dano, uma casca de energia aparece por cima do casco e some
// em menos de um segundo. A casca tem:
//   - contorno (rim light) mais forte nas bordas, que é onde a esfera de shield "pega" a luz;
//   - uma grade hexagonal animada, para não ser só um brilho chapado;
//   - pulsação a partir do tempo (_Time.y), então a animação não depende de script;
//   - cor e intensidade controladas por script (EscudoDoTanque.cs), para o flash acompanhar
//     o dano e a vida restante.
//
// Escrito em HLSL para o URP (Universal Render Pipeline), que é o pipeline do projeto.
// Não é Shader Graph: um .shadergraph é um grafo serializado com GUIDs de nó, impossível de
// escrever à mão com segurança. Este arquivo é texto legível e o efeito é o mesmo.
//
// Como o projeto usa URP, este shader declara um SubShader "UniversalPipeline" com as tags
// exigidas (RenderPipeline, RenderType, Queue) e o include Lighting.hlsl. Foi mantido
// deliberadamente simples para ser fácil de explicar na apresentação: nada de geometria extra,
// nada de passe de profundidade — só superfície transparente com blending aditivo.

Shader "Arena/EscudoDoTanque"
{
    Properties
    {
        [HDR] _CorDoEscudo ("Cor do escudo", Color) = (0.25, 0.75, 1.0, 1.0)
        _Intensidade ("Intensidade do brilho", Range(0, 5)) = 1.5
        _Opacidade ("Opacidade", Range(0, 1)) = 0.55
        _ForcaDoContorno ("Força do contorno", Range(0, 8)) = 3.0
        _EscalaDaGrade ("Escala da grade", Range(1, 60)) = 18.0
        _VelocidadeDaGrade ("Velocidade da grade", Range(0, 4)) = 0.6
        _ForcaDosFresnel ("Força do fresnel", Range(0.5, 8)) = 3.5
    }

    // O SubShader e' agnostico de pipeline DE PROPOSITO: sem a tag "RenderPipeline", ele e'
    // usado tanto pelo URP quanto pelo built-in. Era "UniversalPipeline" e por isso o material
    // ficava magento (rosa) se o projeto estivesse no built-in — o FallBack nao resgata isso,
    // porque o URP/built-in nao cai para o FallBack quando o SubShader so' nao combina.
    // O include do Core.hlsl abaixo so' existe em projeto com URP instalado (e' o caso).
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        // Aditivo: o escudo ilumina o que está atrás em vez de esconder. Combina com energia.
        // Aditivo com order alto: desenha depois do tanque, sem escrever profundidade.
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Back
        Offset 0, -1

        Pass
        {
            Name "EscudoAditivo"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Atributos
            {
                float4 posicao : POSITION;
                float3 normal  : NORMAL;
                float2 uv      : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posicaoNaTela : SV_POSITION;
                float3 normalNoMundo : TEXCOORD0;
                float3 posicaoNoMundo : TEXCOORD1;
                float2 uv            : TEXCOORD2;
            };

            // Declaradas no bloco CBUFFER para funcionar com SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                float4 _CorDoEscudo;
                float  _Intensidade;
                float  _Opacidade;
                float  _ForcaDoContorno;
                float  _EscalaDaGrade;
                float  _VelocidadeDaGrade;
                float  _ForcaDosFresnel;
            CBUFFER_END

            Varyings vert(Atributos entrada)
            {
                Varyings saida;

                VertexPositionInputs posicoes = GetVertexPositionInputs(entrada.posicao.xyz);
                saida.posicaoNaTela = posicoes.positionCS;
                saida.posicaoNoMundo = posicoes.positionWS;
                saida.normalNoMundo = TransformObjectToWorldNormal(entrada.normal);
                saida.uv = entrada.uv;

                return saida;
            }

            /// Grade hexagonal aproximada a partir das coordenadas de tela/uv.
            /// Não é um hexágono perfeito (seria necessário mais matemática), mas dá o padrão
            /// de células que se espera de um escudo de energia — e é legível na apresentação.
            float Grade(float2 uv)
            {
                float2 celula = uv * _EscalaDaGrade;
                float2 deslocamento = float2(sin(celula.y * 1.7 + _Time.y * _VelocidadeDaGrade),
                                             cos(celula.x * 1.7 + _Time.y * _VelocidadeDaGrade)) * 0.35;

                float2 ponto = celula + deslocamento;
                float2 id = floor(ponto);
                float2 local = frac(ponto) - 0.5;

                // Distância até a borda da célula: pequeno perto da borda = linha acesa.
                float distancia = length(local);
                float linha = 1.0 - smoothstep(0.18, 0.34, distancia);

                // Cada célula acende em um instante diferente (id como fase).
                float fase = frac((id.x + id.y) * 0.137 + _Time.y * _VelocidadeDaGrade * 0.25);
                float pulso = 0.55 + 0.45 * sin(fase * 6.2831853);

                return linha * pulso;
            }

            half4 frag(Varyings entrada) : SV_Target
            {
                float3 normal = normalize(entrada.normalNoMundo);
                // Direcao do fragmento ate a camera. GetCameraPositionWS() vem do Core.hlsl e
                // e' a forma documentada no URP. Ler o uniform de posicao da camera direto
                // tambem funcionaria, mas DECLARA-LO aqui causa "redefinition": o
                // UnityInput.hlsl (incluido pelo Core.hlsl) ja o declara uma vez.
                float3 vista = normalize(GetCameraPositionWS() - entrada.posicaoNoMundo);

                // Fresnel: 1 nas bordas (normal perpendicular à vista), 0 de frente.
                float borda = pow(1.0 - saturate(dot(normal, vista)), _ForcaDosFresnel);

                float grade = Grade(entrada.uv * 4.0);
                float contorno = borda * _ForcaDoContorno;

                // Pulsação global leve, só para o escudo nunca ficar totalmente parado.
                float pulso = 0.85 + 0.15 * sin(_Time.y * 2.0);

                float brilho = (grade * 0.6 + contorno + 0.25) * _Intensidade * pulso;
                float alfa = saturate((grade * 0.5 + borda * 0.8 + 0.15) * _Opacidade);

                return half4(_CorDoEscudo.rgb * brilho, alfa);
            }
            ENDHLSL
        }
    }

    // Se o URP não estiver ativo, cai para um efeito equivalente com o pipeline built-in,
    // para a cena nunca ficar com material rosa (shader ausente).
    FallBack "Sprites/Default"
}
