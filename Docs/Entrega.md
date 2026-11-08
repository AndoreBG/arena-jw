# Entrega — Implementação do planejamento arena-jw

Repositório implementado em **`/home/user/repo`** (clone do seu GitHub), sobre os seus commits
`6d12eed` e `2d0aa32`. As correções ficam em commits próprios, por cima deles.

Patches em **`/home/user/patches/`**: `fases/` (um patch por commit de correção, base `2d0aa32`),
`arena-jw-full.patch` (todos juntos), `arena-jw.bundle` (histórico completo) e o pacote `.zip`.

---

## 1. O que foi implementado

### Inimigos → naves alienígenas flutuantes
- `AlienShipAI`: 3 estados ativos com **pesos estáticos 40 / 60 / 100**
  (Patrol / Pursue / Attack), histerese no alcance, percepção por visão **ou** proximidade.
- `AlienShipWeapon`: gira a nave no **eixo Y** para encarar o jogador e atira da
  `transform.position` na direção `transform.forward` — **sem cano, sem muzzle**, usando a
  `TankProjectilePool` compartilhada.
- `EnemyHover`: voo a **3 m** com oscilação **±0,5 m** (`agent.baseOffset`, o agente continua no NavMesh).
- `EnemyHealthBar`: barra de vida + ícone de estado acima da nave (World Space, billboard).
- Prefab `Inimigo` reescrito como nave (tag `Player` removida, `NavMeshAgent.updateRotation = false`,
  `updateUpAxis = false`, `Rigidbody` kinematic, efeitos de cano removidos).

### Menu principal completo
- Cena `menu_principal`: **ranking à esquerda** (abas Fácil/Médio/Difícil, top 5 com Nome |
  Pontuação), **título + Jogar/Configurações/Sair ao centro**, **créditos à direita**.
- Painel de dificuldade (3 botões + descrição ao passar o mouse + Cancelar).
- Painel de configurações (volumes, resolução, modo de exibição, Aplicar/Voltar) com
  **modal de confirmação** quando há mudanças não aplicadas.

### HUD, Game Over e ranking
- HUD de partida: barra de vida, barra de aceleração, pontuação e dificuldade; retículos do
  `TankAimReticleHUD` preservados.
- Game Over: título, pontuação, input de apelido (3 caracteres A–Z/0–9), Tentar Novamente e
  Menu Principal — **ambos salvam** a pontuação na dificuldade corrente.
- `RankingService` por dificuldade, **top 5 por aba**, `timestamp` para desempate
  (mais recente vence), uma entrada por apelido por dificuldade.

### ScriptableObjects
`DifficultyConfig` (Fácil 2/3 s/0,5 · Médio 4/2 s/0,7 · Difícil 6/1 s/1,0), `SpawnerConfig`,
`PlayerStats`, `ResolutionOption` + `DifficultyCatalog` (extra, para o menu resolver a dificuldade).

### Áudio
`AudioSettings` **já implementado como se o mixer existisse**: procura `Resources/ArenaMixer`,
converte linear→dB e aplica em `MasterVolume`/`MusicVolume`/`SfxVolume`; sem mixer, cai em
`AudioListener.volume`. Passo a passo de criação: `Docs/AudioMixer.md` + menu
`Arena → Check Audio Mixer` para validar.

---

## 2. Como aplicar

```bash
cd /caminho/do/seu/arena-jw
git switch -c feature/planejamento-jogo

# Opção A (recomendada): 8 commits por fase — testado em clone limpo do seu HEAD
git am --whitespace=nowarn /home/user/patches/fases/*.patch

# Opção B: um único diff
git apply --3way /home/user/patches/arena-jw-full.patch

# Opção C: só os arquivos finais
unzip -o /home/user/patches/arena-jw-implementacao.zip -d .

# Opção D: histórico completo como remote
git fetch /home/user/patches/arena-jw.bundle 'refs/heads/*:refs/heads/arena/*'
```

Conteúdo de `/home/user/patches/`:

| Arquivo | O que é |
|---|---|
| `fases/0001..0008-*.patch` | os 8 commits, na ordem (aplicação testada) |
| `arena-jw-full.patch` | diff acumulado de `d933c62..HEAD` |
| `arena-jw-implementacao.zip` | árvore final do projeto (idêntica a `/home/user/repo`) |
| `arena-jw.bundle` | repositório git completo (histórico + commits novos) |

Depois, no Unity: **Arena → Setup Main Menu + Playground** e **Bake** da NavMesh
(ver `Docs/GuiaImplementacao.md`).

---

## 3. Verificação

| Checagem | Resultado |
|---|---|
| Compilação dos 22 scripts de runtime contra stubs Unity 6/uGUI/TMP/Input System | **0 erros** |
| Compilação dos 7 scripts de Editor (`UNITY_EDITOR`) | **0 erros** |
| Execução da lógica (ranking, IA, ScriptableObjects, PlayerPrefs, HUD, idempotência, menu) | **113 verificações, 0 falhas** |

Não validado (exige o Unity Editor): layout visual das telas, bake da NavMesh, comportamento
em Play Mode, criação de assets/layer pelos menus.

### Correção: botões do menu sem resposta (`cc04d2a`)

Duas causas somadas:

| Causa | Efeito | Correção |
|---|---|---|
| O painel `SettingsConfirmModal` nasceu **ativo** na cena. É Stretch, tem `raycastTarget` e é o último irmão do Canvas | capturava o clique antes de qualquer botão — **nenhum** botão respondia | nasce desativado no setup e na cena; `SettingsConfirmModal` ganhou o flag `showing` para o `Awake` não fechar o modal no instante em que o `Show()` o abre |
| `playButton.onClick.AddListener(...)` era chamado no **Editor script** | listener registrado em tempo de execução do Editor **não é serializado**: todos os `m_OnClick` ficavam vazios | as referências vão para o `MenuManager`, que liga os três botões no `Awake`, como os outros painéis já faziam |

**Se você já editou a cena à mão** (cores, posições), rode `Arena > Repair Main Menu Scene`
em vez de `Setup Main Menu Scene`: ele desativa o modal e preenche as referências dos botões
sem recriar nada.

### Correções depois dos commits `6d12eed` / `2d0aa32`

| Erro no Unity | Causa | Correção |
|---|---|---|
| `MissingComponentException: There is no 'Canvas' attached to 'HUD_Partida'` ao rodar `Arena > Setup Playground Scene` | o campo serializado `root` apontava para um objeto sem `Canvas`; o código antigo só criava Canvas quando o objeto era **nulo** | `HUDController`, `ArenaGameOverUI` e `SettingsConfirmModal` validam o componente antes de usar e reconstroem num Canvas próprio |
| UI não aparecia / painéis fora do Canvas | `ArenaMenuSceneSetup` pendurava os painéis como **irmãos** do Canvas | painéis e modal passam a ser filhos de `canvas.transform`; artefatos soltos na raiz são apagados |
| Setup duplicava UI ao rodar 2× | `BuildHierarchy` reconstruía a cada chamada | `if (built) return;` no início + limpeza dos filhos antes de montar |
| Nave girava sozinha / altura errada | `Inimigo.prefab` sem `m_UpdateRotation` / `m_UpdateUpAxis` — o Unity aplica o default `true` | prefab grava `m_UpdateRotation: 0` e `m_UpdateUpAxis: 0`; o Editor script reforça ao rodar |
| Referência inválida depois de `Destroy` | `GetComponent<T>() ?? AddComponent<T>()` — `??` **não** respeita o "fake null" do Unity | troca por `x = GetComponent<T>(); if (x == null) x = AddComponent<T>();` em todo o projeto |

**Ordem para atualizar o seu projeto:**

1. `Arena > Create ScriptableObjects Assets` (se os assets ainda não existirem).
2. `Arena > Setup Main Menu + Playground` (regenera a cena de menu e o prefab `ArenaGame`).
3. Abrir `playground` e **bake** da NavMesh (`NavMesh Surface > Bake`).
4. Play em `menu_principal`.

---

## 4. Áudio

- **Criar o mixer**: `Docs/AudioMixer.md`, seções 1–3 (grupos Master/Music/SFX e parâmetros
  expostos com os nomes exatos).
- **Ligar nos sons**: seção 4 (`Output` do AudioSource).
- **Conferir**: menu `Arena → Check Audio Mixer`.
- **Sem mixer**: nada quebra; `AudioSettings.EffectiveSfx` / `EffectiveMusic` ficam prontos.

---

## 5. Sobre converter a cena para YAML

**Não é necessário para esta implementação** — toda a fiação do Playground é feita por scripts
de Editor que usam a API do Unity (`PrefabUtility`, `EditorSceneManager`, `SerializedObject`),
o que funciona com cena binária ou texto.

Se quiser converter (útil para diff/revisão em PR): `Docs/ConversaoCenaYAML.md`, seção 1 —
`Edit → Project Settings → Editor → Asset Serialization → Force Text`, reabrir o projeto e
salvar cada cena. Aviso: não reescreva a cena por fora do Unity; o `NavMeshData` e os `fileID`
são preservados apenas pelo resave do Editor.
