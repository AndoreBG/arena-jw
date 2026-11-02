# Implementação de arena, combate e ranking (partes 1–3)

## O que foi implementado em código

- `Assets/Scripts/Arena/ArenaManager.cs`: estado de partida, dificuldade do Inspector (2/4/6 inimigos), contador TMP, encerramento idempotente, input/IA interrompidos no Game Over, recarga da cena e retorno ao nome de cena configurável.
- `ArenaSpawner.cs`: tentativas infinitas em pontos embaralhados, checagem de distância horizontal, amostragem NavMesh limitada, caixa de ocupação, limite de vivos e remoção sem pontuar se destruído externamente. `ArenaEnemyRegistration` conecta morte, score e liberação de vaga uma vez.
- `ArenaEnemySensor.cs` / `ArenaEnemyAI.cs`: percepção de distância/ângulo/linha de visão com memória, escolha ponderada e desempate Attack > Pursue > Search, histerese de alcance, NavMeshAgent como autoridade do casco, patrulha/perseguição/procura/ataque. `ArenaEnemyWeapon` mira a partir das referências da torre/cano e pede disparos da pool compartilhada.
- `TankProjectile.cs`, `TankProjectilePool.cs` e `TankPlayerInput.cs`: metadados explícitos por disparo (atirador, facção e dano), dano de jogador a inimigo e inimigo a jogador, descarte sem dano em cenário/inimigo aliado, proteção do atirador e reset de estado por reutilização. Mantida a API antiga da pool; o player mantém VFX, cooldown e política de pool cheia.
- `Vida.cs`: propriedade configurável para não destruir o jogador ao morrer; o gerenciador da arena desativa essa opção.
- `RankingService.cs` / `ArenaGameOverUI.cs`: nickname normalizado de 3 caracteres ASCII, top 5 sem duplicatas, atualização só por pontuação maior, empate estável, JSON no `persistentDataPath`, gravação temporária e preservação de arquivo inválido/falha. API pública de consulta para o futuro menu.
- `ProjectSettings/EditorBuildSettings.asset`: entrada inexistente `SampleScene` substituída por `Assets/Scenes/playground.unity`, GUID obtido do `.meta` da cena.

## Integração obrigatória no Unity Editor

A cena `playground.unity` está em formato binário nesta revisão. Não foi alterada às cegas. O clone/ambiente não inclui o Editor Unity; portanto os scripts ainda precisam ser montados e verificados visualmente:

1. Abra `Assets/Scenes/playground.unity` na versão `6000.3.18f1` e confirme o jogador, tag `Player`, `Vida`, `TankPlayerInput`, HUD, pool e os colisores.
2. Crie um GameObject `Arena` com `ArenaManager`; atribua `Vida` do jogador, `TMP_Text` de eliminações, o `ArenaSpawner` e `ArenaGameOverUI`. Escolha `Facil`, `Medio` ou `Dificil` no Inspector. O nome inicial de menu fica configurável; menu ainda não existe no build.
3. Crie um prefab inimigo usando `Assets/Tanque.fbx`: casco visual, `NavMeshAgent`, `Vida` (destruição desabilitada pela inscrição de arena), colliders, `ArenaEnemySensor`, `ArenaEnemyAI` e `ArenaEnemyWeapon`. Não adicione `TankPlayerInput`, `TankMovement`, câmera ou HUD ao inimigo. Configure os transforms reais de olhos, torre, cano e boca no modelo.
4. Faça a torre/cano visuais filhos do casco. Garanta que somente `NavMeshAgent` dirija o casco. Use Rigidbody/colliders compatíveis com navegação e teste-os no Play Mode.
5. Configure sensor: `blockingMask` deve incluir paredes/obstáculos que bloqueiam visão; configure o jogador como alvo pela spawner. Configure a arma para a pool compartilhada, dano, alcance e cadência.
6. Crie pontos de spawn como transforms. No `ArenaSpawner`, atribua prefab e pontos; configure `occupancyMask` para obstáculos/personagens, excluindo piso e triggers; ajuste meia-extensão/offset à dimensão real do prefab. O spawn só aceita NavMesh a até `navMeshTolerance` do ponto.
7. Crie painel Game Over com `TMP_Text` de pontuação/mensagem, `TMP_InputField`, dois botões e `ArenaGameOverUI`. Vincule os botões a `SubmitAndStartNew` e `SubmitAndReturnToMenu`. O painel inicia oculto; a orientação de nickname é apresentada ao abrir.
8. Gere/revise o NavMesh no AI Navigation com dimensões do `NavMeshAgent` adequadas ao tanque. Teste os quatro fluxos de colisão, limite de vivos para cada dificuldade, reposição e restart.

## Verificação realizada neste ambiente

- Conferidos o HEAD (`bc65c17`), versão Unity `6000.3.18f1`, Input System `1.19.0`, AI Navigation `2.0.13`, scripts citados, API existente da pool, GUID da cena e estado inicial limpo.
- A cena e o asset NavMesh foram identificados como arquivos binários/asset serializado. Não foram regravados.
- O `EditorBuildSettings` foi atualizado para a cena existente.
- Não há Unity Editor, `dotnet`, `csc` ou `mcs` neste ambiente. Assim, não foram executados compilação Unity, Play Mode, teste de física/NavMesh, build PC, teste visual ou montagem de cena/prefab. Esses pontos não são declarados validados.
- Ranking e escolha ponderada foram implementados de forma isolada para facilitar testes. Ainda não há assembly de testes Edit Mode adicionado para não introduzir asmdef que quebre referências aos scripts em `Assembly-CSharp`.

O menu e a exibição visual do ranking continuam pendentes, conforme escopo; o serviço já fornece `RankingService.TryGetEntries` para integração posterior.
