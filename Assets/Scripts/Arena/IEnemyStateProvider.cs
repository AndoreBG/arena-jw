/// <summary>
/// Contrato mínimo para a UI de estado do inimigo (barra de vida + ícone).
/// Existe para a UI não depender de uma IA específica.
/// </summary>
public interface IEnemyStateProvider
{
    string StateName { get; }
    int StateWeight { get; }
}
