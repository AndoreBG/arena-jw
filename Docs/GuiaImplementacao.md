# Guia de Implementação — arena-jw

Este guia é curto de propósito: a maior parte da configuração é feita por scripts de Editor.
Siga na ordem. Tempo estimado: 10 minutos.

---

## 0. Requisitos

- Unity **6000.3.18f1** (versão do projeto — não abra com outra sem atualizar o projeto).
- Pacotes já instalados: Input System 1.19.0, AI Navigation 2.0.13, URP 17.3, TextMesh Pro.
- O projeto usa **Input System** (`activeInputHandler = 1`), então a UI usa `InputSystemUIInputModule`.

---

## 1. Primeiro passo (obrigatório)

Abra o projeto. Espere a importação terminar. No menu do topo aparece **Arena**.

1. Menu **Arena → Create ScriptableObjects Assets**.
   Gera as 3 dificuldades (Fácil 2/3s/0.5 · Médio 4/2s/0.7 · Difícil 6/1s/1.0), o catálogo,
   `PlayerStats`, `SpawnerConfig` e 6 resoluções, além das cópias em `Assets/Resources`.

2. Menu **Arena → Setup Main Menu + Playground**.
   Este comando, em sequência:
   - gera os ScriptableObjects (se faltarem);
   - cria o prefab `Assets/Prefabs/ArenaGame.prefab` (HUD + Game Over + bootstrap);
   - **reescreve o prefab `Assets/Prefabs/Inimigo.prefab` como nave alienígena**;
   - coloca o `ArenaGame`, o EventSystem, a pool de projéteis e os pontos de spawn no Playground;
   - monta a cena `Assets/Scenes/menu_principal.unity`;
   - adiciona as duas cenas ao Build Settings.

3. Abra a cena **playground** e **faça o bake da NavMesh**: selecione o `NavMeshSurface` →
   botão **Bake**. (Não é possível fazer isso por código: depende da geometria da cena.)

4. Pressione **Play** começando pela cena do menu. O Console deve mostrar
   `[Arena] Partida configurada.` com jogador, dificuldade, spawner, HUD e Game Over.

> Se algo parecer errado, rode **Arena → Validate Setup** (ou **Validate All Scenes**): ele lista
> referências e cenas faltando.

---

## 2. O que o segundo comando altera no prefab `Inimigo`

O prefab antigo era um tanque com tag `Player`, `Rigidbody` e scripts de tanque. Ele virou uma nave:

| Antes | Depois | Por quê |
|---|---|---|
| Tag `Player` | Tag `Untagged` | Com a tag Player a nave era tratada como jogador no dano |
| `Rigidbody` | removido | NavMeshAgent + código movem a nave; um Rigidbody brigava com o agente |
| `ArenaEnemyAI` (tanque) | `AlienShipAI` | 3 estados com pesos estáticos (Patrol 40 / Pursue 60 / Attack 100) |
| `ArenaEnemyWeapon` (cano) | `AlienShipWeapon` | sem cano/muzzle: tira de `transform.position` na direção da frente |
| `FX_Tiro` e clarão | removidos | eram efeitos do cano do tanque |
| `NavMeshAgent.updateRotation = true` | `false`, `updateUpAxis = false` | quem gira a nave no eixo Y é a arma (para encarar o jogador) |
| `NavMeshAgent` no chão | `baseOffset = 3` via `EnemyHover` | a nave voa a 3 m com oscilação senoidal de ±0,5 m |
| — | `EnemyHealthBar` | barra de vida + ícone de estado acima da nave (UI em World Space) |
| — | `ArenaEnemyRegistration` | liga a morte à pontuação e libera a vaga do spawner |

### Sobre a rotação e a flutuação

- **X/Z**: `NavMeshAgent` (`updatePosition = true`). Ele continua dono do caminho.
- **Y**: `EnemyHover` escreve `agent.baseOffset = 3 + 0,5·sen(2π·0,35·t)`.
  Elevar pelo `baseOffset` mantém o agente **no** NavMesh — sem isso, o path following para de
  funcionar (`isOnNavMesh` fica falso e o agente deixa de seguir a rota).
- **Rotação Y**: `AlienShipWeapon` gira a nave para encarar o jogador; o tiro só sai quando o
  ângulo está dentro de `aimTolerance` (12°). `updateUpAxis = false` evita que o agente tente
  levantar a nave em rampas.

### Estados e ícones

`EnemyHealthBar` aceita três sprites opcionais (`patrolIcon`, `pursueIcon`, `attackIcon`).
Os campos estão vazios de propósito — o planejamento dizia "ícones fornecidos pelo usuário".
Sem sprite, o ícone é um quadrado colorido: amarelo (Patrol), laranja (Pursue), vermelho (Attack).
Para adicionar os seus: selecione o prefab → `EnemyHealthBar` → arraste os sprites.

---

## 3. Fluxo de jogo

```
MenuPrincipal ── Jogar ──► Painel Dificuldade ── Fácil/Médio/Difícil ──► playground
      │                                        └── Cancelar ──► Menu
      ├── Configurações ──► Painel Configurações ── Voltar com mudanças ──► Modal
      │                                    └── Aplicar ──► PlayerPrefs + aplica
      └── Sair ──► Application.Quit() (no Editor, sai do Play)

playground ── vida do jogador chega a 0 ──► Game Over (Time.timeScale = 0)
      ├── Tentar Novamente ──► mesma dificuldade (salva no ranking)
      └── Menu Principal ──► menu_principal (salva no ranking)
```

- **Dificuldade** é gravada em `PlayerPrefs["Arena_Difficulty"]` (0=Fácil, 1=Médio, 2=Difícil).
- **Ranking**: `ranking.json` em `persistentDataPath`, top 5 por dificuldade, empate resolvido
  pelo registro mais recente, um recorde por apelido/dificuldade. Apelido: exatamente 3
  caracteres `A–Z`/`0–9` (o campo já converte para maiúsculas e limita a 3).
- **Configurações**: `Settings_MasterVolume`, `Settings_SfxVolume`, `Settings_MusicVolume`,
  `Settings_ResolutionIndex`, `Settings_DisplayMode`. Aplicadas ao abrir o menu e ao dar Play.

---

## 4. Áudio

Veja **`Docs/AudioMixer.md`**. Resumo: o código **já** procura um mixer em
`Assets/Resources/ArenaMixer` com os parâmetros expostos `MasterVolume`, `MusicVolume` e
`SfxVolume`. Sem o mixer, o volume geral cai em `AudioListener.volume` e os volumes de efeito e
música ficam prontos em `AudioSettings.EffectiveSfx` / `AudioSettings.EffectiveMusic` para quem
tocar som. Rode **Arena → Check Audio Mixer** para conferir.

---

## 5. Scripts

### Novos (runtime)

| Pasta | Script | Responsabilidade |
|---|---|---|
| `Data` | `DifficultyConfig` | números por dificuldade (limite, spawn, IA) |
| `Data` | `SpawnerConfig` | ajustes de spawn que não mudam por dificuldade |
| `Data` | `PlayerStats` | vida, velocidade, dano e cadência do jogador |
| `Data` | `ResolutionOption` | uma resolução do dropdown |
| `Data` | `DifficultyCatalog` | lista das 3 dificuldades (resolve por enum) |
| `Settings` | `GameSettings` + `AudioSettings` | PlayerPrefs em um único lugar + aplicação de volumes/resolução |
| — | `AlienShipAI` | IA da nave: Patrol / Pursue / Attack com pesos estáticos |
| — | `AlienShipWeapon` | gira a nave no Y e dispara da `transform.position` |
| — | `EnemyHover` | flutuação ±0,5 m na altura de voo (via `baseOffset`) |
| — | `EnemyHealthBar` | barra de vida + ícone de estado acima do inimigo |
| — | `HUDController` | barras de vida/aceleração, pontuação e dificuldade na partida |
| `Menu` | `MenuManager` | navegação entre painéis e carga de cenas |
| `Menu` | `DifficultyPanel` | 3 botões + descrição ao passar o mouse + cancelar |
| `Menu` | `SettingsPanel` | sliders, dropdowns, Aplicar/Voltar |
| `Menu` | `SettingsConfirmModal` | modal "perder mudanças?" |
| `Menu` | `RankingDisplay` | abas Fácil/Médio/Difícil, top 5 por aba |
| `Bootstrap` | `ArenaGameInitializer` | monta e liga o jogo no Playground |

### Modificados

| Script | Mudança |
|---|---|
| `ArenaManager` | usa `DifficultyConfig`/catálogo em vez de enum fixo; aplica `PlayerStats`; liga HUD e Game Over; dificuldade vinda do menu |
| `ArenaSpawner` | usa `SpawnerConfig`/`DifficultyConfig`; fallback de pontos aleatórios no NavMesh; prioridade: dificuldade do manager |
| `ArenaGameOverUI` | recebe a dificuldade e salva no ranking; painel montado em código |
| `RankingService` | ranking por dificuldade + `timestamp`, empate pelo mais recente, top 5 por aba |
| `ArenaEnemySensor` | multiplicador de visão (dificuldade) e máscara configurável |
| `Vida` (referência) | `Configure(vidaMaxima, restore)` para os `PlayerStats` |
| `TankMovement` / `TankPlayerInput` | `Configure(...)` para os `PlayerStats` (sem mexer no comportamento atual) |
| `MouseOrbitCamera` | não recaptura o cursor com o Game Over aberto |

`ArenaEnemyAI` e `ArenaEnemyWeapon` continuam no projeto (o `ArenaManager` ainda os desativa no
Game Over), mas o prefab da nave passou a usar `AlienShipAI`/`AlienShipWeapon`.

### Scripts de Editor (menu `Arena`)

| Menu | O que faz |
|---|---|
| `Create ScriptableObjects Assets` | gera os assets + cópias em `Resources` |
| `Setup Main Menu + Playground` | fluxo completo (recomendado) |
| `Setup Main Menu Scene` | só a cena de menu |
| `Setup Playground Scene` | só o Playground (ArenaGame, pool, spawn points) |
| `Setup Enemy Prefab (Alien Ship)` | só o prefab da nave |
| `Create ArenaGame Prefab` | recria o prefab de UI/bootstrap |
| `Validate Setup` / `Validate All Scenes` | relatório de pendências |
| `Check Audio Mixer` | confere o mixer e os parâmetros expostos |

---

## 6. Verificação feita neste ambiente

Não há Unity Editor aqui, então a validação foi por compilação e execução:

- **Compilação** dos 20 scripts de runtime + 7 de Editor contra stubs da API do Unity 6 / uGUI /
  TextMesh Pro / Input System / UnityEditor: **0 erros** nos dois conjuntos.
- **Execução** da lógica pura em um harness: **53 verificações, 0 falhas** — normalização e
  validação de apelido, ranking por dificuldade, top 5, empate pelo mais recente, recorde
  por apelido, preservação de arquivo inválido, decisão da IA pelos pesos (com empate indo para
  o ataque), aplicação dos números de cada dificuldade no `DifficultyConfig`/`SpawnerConfig`,
  PlayerPrefs e os volumes efetivos.
- **Não verificado** (precisa do Editor): aparência final das telas, bake da NavMesh,
  comportamento da nave em jogo, criação de pasta/layer pelo Editor script e o fluxo completo
  clicando nos botões.

---

## 7. Problemas comuns

| Sintoma | Causa provável | Solução |
|---|---|---|
| As naves não aparecem | NavMesh não foi **baked** ou os pontos de spawn não estão sobre ela | Abra o Playground e clique em Bake |
| Console: `Prefab precisa de NavMeshAgent válido` | Mesh sem NavMesh sob o ponto | Refaça o bake; o spawner pula pontos inválidos |
| Naves paradas no chão | `EnemyHover` sem `NavMeshAgent` ou `baseOffset` sobrescrito | Rode `Setup Enemy Prefab (Alien Ship)` |
| Barra de vida some | Canvas filho da nave com escala errada | Ajuste `height`/`size` no `EnemyHealthBar` |
| Botões do menu não respondem | Faltou o `EventSystem` com `InputSystemUIInputModule` | Rode `Setup Main Menu Scene` de novo |
| "A cena playground não está no Build Settings" | Build Settings vazio | Rode `Setup Main Menu + Playground` |
| Pontuação não salva | Apelido inválido (o campo exige 3 caracteres) | Digite 3 letras/números |
