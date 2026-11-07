# Ambiente de verificação (fora do repositório)

Os testes deste repositório foram executados em `/home/user/check`, **fora** da pasta do projeto
Unity (para o Unity não importar os stubs). Não faz parte do jogo.

## Conteúdo

| Caminho | O que é |
|---|---|
| `check/UnityStubs.cs` | stubs da API Unity/uGUI/TMP/Input System usada pelos scripts |
| `check/EditorStubs.cs` | stubs da API UnityEditor |
| `check/Runtime/Check.csproj` | compila os scripts de runtime contra os stubs |
| `check/Editor/CheckEditor.csproj` | compila runtime + Editor (`UNITY_EDITOR` definido) |
| `check/Tests/Harness.csproj` | executa a lógica pura (ranking, IA, ScriptableObjects, PlayerPrefs) |

## Comandos

```bash
cd /home/user/check/Runtime && dotnet build Check.csproj
cd /home/user/check/Editor  && dotnet build CheckEditor.csproj
cd /home/user/check/Tests   && dotnet run --project Harness.csproj
```

Resultado da última execução: 2 builds com 0 erros e 53 verificações com 0 falhas.

## Limites

Os stubs cobrem apenas as APIs usadas. Um build verde aqui **não** substitui o Unity: não
validam serialização de cena/prefab, bake de NavMesh, layout real das telas, renderização nem a
ordem de execução do motor.
