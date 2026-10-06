using UnityEngine;
using UnityEngine.SceneManagement;

// Vai no Canvas da cena do Menu. Cada método é ligado no OnClick de um botão.
public class MenuPrincipal : MonoBehaviour
{
    public GameObject painelDoMenu;
    public GameObject painelDeConfiguracao;
    public string nomeDaCenaDoJogo = "Arena";

    void Start()
    {
        MostrarMenu();
    }

    public void Jogar()
    {
        SceneManager.LoadScene(nomeDaCenaDoJogo);
    }

    public void AbrirConfiguracaoDosControles()
    {
        painelDoMenu.SetActive(false);
        painelDeConfiguracao.SetActive(true);
    }

    public void MostrarMenu()
    {
        painelDeConfiguracao.SetActive(false);
        painelDoMenu.SetActive(true);
    }

    public void Sair()
    {
        Application.Quit();
    }
}
