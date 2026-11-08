#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta o escudo do tanque DENTRO DO EDITOR, para ele existir sempre na cena — sem nada ser
/// criado em tempo de execução.
///
/// <para>
/// Antes, o <c>EscudoDoTanque</c> montava a esfera e o material em <c>Runtime</c> (via
/// <c>GameObject.CreatePrimitive</c> e <c>Shader.Find</c>). O efeito funcionava, mas:
/// </para>
/// <list type="bullet">
///   <item>não aparecia na Hierarquia, então não dava para conferir tamanho nem posição olhando a cena;</item>
///   <item>dependia de <c>Shader.Find</c>, que só acha shaders incluídos no build ou em Always Included;</item>
///   <item>qualquer erro de montagem só se descobria apertando Play.</item>
/// </list>
///
/// <para>
/// Este menu cria a Hierarquia de verdade:
/// </para>
/// <code>
/// Tanque (tag Player)
/// └── EscudoDoTanque        (componente com material e casca já ligados)
///     └── Casca             (MeshRenderer com a esfera, escala e posição calculadas)
/// </code>
///
/// <para>
/// Depois de rodar uma vez, o escudo é um objeto comum da cena: pode ser movido, escalado e
/// ajustado à mão. A criação em runtime deixa de acontecer porque o componente já está lá
/// (<c>CriarNo</c> acha o existente e devolve).
/// </para>
/// </summary>
public static class EscudoDoTanqueSetup
{
    private const string NomeDoShader = "Arena/EscudoDoTanque";
    private const string CaminhoDoMaterial = "Assets/Shaders/EscudoDoTanque.mat";
    private const string NomeDoObjeto = "EscudoDoTanque";
    private const string NomeDaCasca = "Casca";
    private const float Folga = 1.1f;

    [MenuItem("Arena/Efeitos/Montar Escudo do Tanque na Cena")]
    public static void MontarNaCena()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escudo do Tanque",
                "Pare o Play primeiro. Alterações feitas em Play mode são perdidas ao parar.", "Ok");
            return;
        }

        GameObject tanque = GameObject.FindGameObjectWithTag("Player");
        if (tanque == null)
        {
            EditorUtility.DisplayDialog("Escudo do Tanque",
                "Nenhum objeto com a tag 'Player' nesta cena.\n\n" +
                "Confira no Inspector do tanque se a Tag está em 'Player'.", "Ok");
            return;
        }

        Shader shader = Shader.Find(NomeDoShader);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Escudo do Tanque",
                "Shader '" + NomeDoShader + "' não encontrado.\n\n" +
                "Confira se Assets/Shaders/EscudoDoTanque.shader existe e compila " +
                "(erros de compilação aparecem no Console).", "Ok");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(CaminhoDoMaterial);
        if (material == null)
        {
            material = new Material(shader) { name = "EscudoDoTanque" };
            AssetDatabase.CreateAsset(material, CaminhoDoMaterial);
            Debug.Log("[EscudoDoTanqueSetup] Material criado em " + CaminhoDoMaterial);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        // Reaproveita o que já existe em vez de duplicar.
        EscudoDoTanque componente = tanque.GetComponentInChildren<EscudoDoTanque>(true);
        if (componente != null)
        {
            bool regerarCasca = EditorUtility.DisplayDialog("Escudo do Tanque",
                "O escudo já existe em '" + componente.gameObject.name + "'.\n\n" +
                "Regerar a casca? (recalcula tamanho e centro; seus ajustes de posição manual " +
                "na Casca serão perdidos)", "Regerar", "Manter como está");

            componente.Configurar(material);

            if (regerarCasca)
            {
                GerarCasca(componente, tanque);
                MarcarCenaComoAlterada(tanque);
            }

            Selection.activeObject = componente;
            Debug.Log("[EscudoDoTanqueSetup] Escudo já existia; configuração atualizada.");
            return;
        }

        GameObject objeto = new GameObject(NomeDoObjeto);
        Undo.RegisterCreatedObjectUndo(objeto, "Montar Escudo do Tanque");
        Undo.SetTransformParent(objeto.transform, tanque.transform, "Montar Escudo do Tanque");
        objeto.transform.localPosition = Vector3.zero;
        objeto.transform.localRotation = Quaternion.identity;
        objeto.transform.localScale = Vector3.one;

        EscudoDoTanque criado = Undo.AddComponent<EscudoDoTanque>(objeto);
        criado.Configurar(material);

        GerarCasca(criado, tanque);

        MarcarCenaComoAlterada(tanque);
        Selection.activeObject = criado;

        Debug.Log("[EscudoDoTanqueSetup] Escudo montado em '" + tanque.name + "'. " +
                  "Agora ele faz parte da cena: Play não cria mais nada em runtime.");
    }

    [MenuItem("Arena/Efeitos/Montar Escudo do Tanque na Cena", true)]
    private static bool MontarNaCenaValidar()
    {
        return !EditorApplication.isPlaying;
    }

    /// <summary>
    /// Cria (ou recria) a esfera filha com o tamanho e o centro corretos.
    ///
    /// A matemática é a mesma do componente em runtime, de propósito: o tamanho sai em unidades
    /// de MUNDO e é dividido pela escala herdada, senão a escala do tanque seria aplicada duas
    /// vezes e a casca ficaria dentro do casco (foi o bug que deixou o efeito invisível).
    /// </summary>
    private static void GerarCasca(EscudoDoTanque componente, GameObject tanque)
    {
        Transform transformDoEscudo = componente.transform;

        Transform antiga = transformDoEscudo.Find(NomeDaCasca);
        while (antiga != null)
        {
            Undo.DestroyObjectImmediate(antiga.gameObject);
            antiga = transformDoEscudo.Find(NomeDaCasca);
        }

        float diametro;
        Vector3 centro;
        Medir(componente.transform, tanque.transform, out diametro, out centro);

        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = NomeDaCasca;
        Undo.RegisterCreatedObjectUndo(esfera, "Criar Casca do Escudo");

        // Só desenha: nenhum collider pode ficar no caminho dos tiros.
        Collider colisor = esfera.GetComponent<Collider>();
        if (colisor != null) Undo.DestroyObjectImmediate(colisor);

        esfera.transform.SetParent(transformDoEscudo, false);

        Vector3 centroComDeslocamento = centro + (componente.transform.rotation * componente.DeslocamentoDoCentro);
        esfera.transform.localPosition = transformDoEscudo.InverseTransformPoint(centroComDeslocamento);
        esfera.transform.localRotation = Quaternion.identity;
        esfera.transform.localScale = EscalaCompensada(transformDoEscudo, diametro);

        MeshRenderer renderer = esfera.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = componente.MaterialDoEscudo;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Deixa VISÍVEL na cena de propósito: é assim que se enxerga a casca para posicionar e
        // dimensionar com o tanque do lado. Quem apaga no início da partida é o componente,
        // em OnEnable (Apagar) — o comportamento em Play não muda.
    }

    private static void Medir(Transform doEscudo, Transform tanque, out float diametro, out Vector3 centro)
    {
        Renderer[] partes = tanque.GetComponentsInChildren<Renderer>(true);

        bool temAlguma = false;
        Bounds area = default;

        foreach (Renderer parte in partes)
        {
            if (parte == null) continue;
            if (parte.transform.IsChildOf(doEscudo)) continue; // ignora a própria casca

            if (!temAlguma) { area = parte.bounds; temAlguma = true; }
            else area.Encapsulate(parte.bounds);
        }

        if (!temAlguma)
        {
            diametro = 4f;
            centro = tanque.position;
            return;
        }

        float maiorLado = Mathf.Max(area.size.x, Mathf.Max(area.size.y, area.size.z));
        diametro = Mathf.Clamp(maiorLado * Folga, 1f, 200f);
        centro = area.center;
    }

    private static Vector3 EscalaCompensada(Transform transformDoEscudo, float diametroEmMundo)
    {
        Vector3 herdada = transformDoEscudo.lossyScale;

        float x = Mathf.Abs(herdada.x) > 0.0001f ? diametroEmMundo / Mathf.Abs(herdada.x) : diametroEmMundo;
        float y = Mathf.Abs(herdada.y) > 0.0001f ? diametroEmMundo / Mathf.Abs(herdada.y) : diametroEmMundo;
        float z = Mathf.Abs(herdada.z) > 0.0001f ? diametroEmMundo / Mathf.Abs(herdada.z) : diametroEmMundo;

        return new Vector3(x, y, z);
    }

    private static void MarcarCenaComoAlterada(GameObject tanque)
    {
        Scene cena = tanque.scene;
        if (cena.IsValid()) EditorSceneManager.MarkSceneDirty(cena);
    }
}
#endif
