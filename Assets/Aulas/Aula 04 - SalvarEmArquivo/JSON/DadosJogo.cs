using System;

// AULA 4
// Classe de dados: NÃO herda de MonoBehaviour, então não é componente
// e não entra em objeto nenhum da cena. É criada pelo código: new DadosJogo().
//
// [Serializable] autoriza a Unity a transformar esta classe em texto.
// Sem ele, o JSON sai vazio: { }
//
// O JsonUtility só enxerga campos public (ou private com [SerializeField]).
[Serializable]
public class DadosJogo
{
    public int pontos;
    public int fase = 1;
    public int vidas = 3;
    public int recorde;
}
