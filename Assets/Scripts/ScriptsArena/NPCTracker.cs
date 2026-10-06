using System;
using UnityEngine;

// Componente auxiliar adicionado automaticamente pelo NPCSpawner.
// Notifica o Spawner quando o NPC e destruido para atualizar o contador.
public class NPCTracker : MonoBehaviour
{
    public event Action OnNPCDestroyed;

    private void OnDestroy()
    {
        OnNPCDestroyed?.Invoke();
    }
}
