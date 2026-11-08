# Arena JW

Jogo 3D de tanque em arena, feito em Unity, para as matérias **Jogos Digitais para Console** e
**Inteligência Artificial em Jogos Digitais**.

O jogador pilota um tanque numa arena e enfrenta naves alienígenas que aparecem sem parar. Os
inimigos são controlados por uma máquina de estados com percepção por visão e audição de
proximidade — eles patrulham sozinhos, veem o jogador, perseguem e atiram. Levar dano também
faz o inimigo perseguir, mesmo sem ter visto o jogador.

A pontuação vai para um ranking persistente, com top 5 por dificuldade.

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Sumário

- [Sobre o jogo](#sobre-o-jogo)
- [Controles](#controles)
- [Sistemas](#sistemas)
  - [Movimento e câmera](#movimento-e-câmera)
  - [Tiro e projéteis](#tiro-e-projéteis)
  - [Vida e HUD](#vida-e-hud)
- [Inteligência Artificial](#inteligência-artificial)
- [Dificuldade](#dificuldade)
- [Ranking](#ranking)
- [Áudio](#áudio)
- [Saída da partida](#saída-da-partida)
- [Como executar](#como-executar)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Decisões de projeto](#decisões-de-projeto)
- [Limitações conhecidas](#limitações-conhecidas)

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Sobre o jogo

| | |
|---|---|
| **Gênero** | Arena shooter 3D, visão de terceira pessoa |
| **Plataforma** | PC (Windows) |
| **Engine** | Unity 6 (`6000.3.18f1`) |
| **Pipeline** | Universal Render Pipeline (URP) |
<<<<<<< Updated upstream
| **Entrada** | Teclado e mouse (Input System) |
=======
| **Entrada** | Teclado, mouse e controle (Xbox / PlayStation) |
>>>>>>> Stashed changes
| **Duração de uma partida** | Sem fim definido — termina quando o jogador perde |

O objetivo é sobreviver o máximo possível. Cada inimigo destruído vale um ponto. O jogo acaba
quando a vida do jogador chega a zero.

<<<<<<< Updated upstream
<br>

## Controles

| Ação | Tecla |
|---|---|
| Acelerar / recuar | `W` / `S` ou `↑` / `↓` |
| Girar o tanque | `A` / `D` ou `←` / `→` |
| Mirar a torre | Mouse |
| Orbitar a câmera | Mouse |
| Atirar | Clique esquerdo |
| Capturar o cursor | Clique esquerdo |
| Voltar ao menu (sem gravar) | Segurar `Esc` por 3 s |

A direção do movimento é independente da câmera: a tração usa o `transform.forward` do próprio
tanque, e a torre acompanha o mouse separadamente.

<br>

=======
---

## Controles

Funciona no teclado/mouse e no controle, sem precisar escolher.

| Ação | Teclado / Mouse | Xbox | PlayStation |
|---|---|---|---|
| Acelerar / recuar | `W` `S` ou `↑` `↓` | Stick esquerdo (↕) ou D-pad | Stick esquerdo (↕) ou D-pad |
| Girar o tanque | `A` `D` ou `←` `→` | Stick esquerdo (↔) ou D-pad | Stick esquerdo (↔) ou D-pad |
| Olhar / mirar | Mouse | **Stick direito** | **Stick direito** |
| Atirar | Clique esquerdo ou `Espaço` | RT, **A** ou RB | R2, **✕** ou R1 |
| Voltar ao menu (sem gravar) | Segurar `Esc` por 3 s | Segurar **Y** por 3 s | Segurar **△** por 3 s |
| Navegar nos menus | Mouse | D-pad ou stick | D-pad ou stick |

O mesmo código serve para os dois controles: o Input System trata os dois como `<Gamepad>`, e as
legendas na tela citam os dois nomes (`A / ✕`, `Y / △`).

A direção do movimento é independente da câmera: a tração usa o `transform.forward` do próprio
tanque, e a torre acompanha a mira separadamente.

### Controle

O stick direito gira câmera e torre, com **zona morta** para não tremer com o analógico parado.
A velocidade fica em `Sensibilidade Do Controle` (180°/s no padrão) e a zona morta em
`Zona Morta Do Analógico` (0,18), no componente `MouseOrbitCamera`.

Nos menus, o Unity só navega por D-pad/stick quando existe um objeto selecionado. O componente
`ControleNoMenu` seleciona o primeiro botão disponível assim que o menu abre — e também quando
o jogador aperta qualquer coisa no controle.

O botão de sair pode ser trocado no Inspector (`Botao Do Controle`), e o atalho pelo controle
pode ser desligado com `Usar Controle = false`.

---

>>>>>>> Stashed changes
## Sistemas

### Movimento e câmera

O tanque usa `Rigidbody` + **WheelColliders**. Em `Awake` o script cria 4 rodas a partir dos
bounds das esteiras e desativa os colisores delas, que passam a servir só de apoio — o torque
vai para as rodas. A direção é o `transform.forward` do próprio tanque, independente da câmera.

A câmera orbita o tanque com *yaw* e *pitch* limitado (de −15° a 75°) e segue o alvo com
suavização.

A torre (`TankTurretAim`) gira independente do chassi, e o cano inclina conforme a mira. Dois
retículos no HUD marcam onde a câmera aponta e onde o cano aponta — eles coincidem quando o
cano está alinhado com o alvo.

### Tiro e projéteis

Os projéteis saem de uma **pool** (`TankProjectilePool`), não de `Instantiate`/`Destroy`. É o
mesmo caminho para o jogador e para os inimigos: a diferença é só a origem, a direção e o
*dano* de quem atirou (`TankShotData`).

Cada projétil carrega sua facção (`Player` ou `Enemy`) e ignora as colisões do próprio dono, o
que evita o tanque atirar em si mesmo.

O jogador atira a 1 tiro/s com 50 de dano; o inimigo atira a cada 1,6 s com 25 de dano.

### Vida e HUD

Todo objeto com vida usa o mesmo componente `Vida`, que expõe `ReceberDano`, `Morto` e o evento
`AoMorrer`. A `BarraVida` desenha a vida como recorte (`fillAmount` em um `Image` do tipo
*Filled*).

O HUD mostra a barra de vida do jogador, a pontuação e a dificuldade em uso. Inimigos têm uma
barra de vida própria, sempre visível e virada para a câmera (`OlharParaCamera`).

<<<<<<< Updated upstream
=======
---

>>>>>>> Stashed changes
## Inteligência Artificial

O controle dos inimigos está em `InimigoMaquinaDeEstados` (o mesmo nome usado nas aulas). O
inimigo é um `NavMeshAgent`, e a locomoção usa o NavMesh da arena.

### Máquina de estados

Três estados, com pesos configuráveis:

| Estado | Comportamento |
|---|---|
| **Andando** | Vai reto para frente até trombar em algo. Ao bater, gira de 15° a 180° (ângulo e lado sorteados) e segue reto. |
| **Perseguindo** | Vai atrás do jogador, enquanto ele está fora do alcance de ataque. |
| **Atacando** | Parou no alcance e atira. |

A escolha é por **peso** (`PesoAndando` 40, `PesoPerseguindo` 60, `PesoAtacando` 100). Quando
mais de um estado é possível no mesmo frame, vence o de peso maior. Em caso de empate, a
prioridade é `Atacando > Perseguindo > Andando`.

### Percepção

O inimigo "vê" o jogador quando **três condições** são verdadeiras:

1. A distância é menor que `DistanciaDeVisao` (45 m).
2. O ângulo entre a frente do inimigo e o jogador cabe em `AnguloDeVisao` (160°, metade para
   cada lado).
3. Um `Physics.Raycast` da posição dos olhos até o jogador **não encontra parede** no caminho.
   O raio ignora o próprio inimigo e o jogador — eles não são obstáculos para a visão.

Além da visão, existe **percepção por proximidade**: se o jogador está a até 70% da distância
de visão (31,5 m), o inimigo percebe **mesmo de costas**. Sem isso, um inimigo que nasce virado
para o lado errado andaria reto para longe e nunca encontraria o jogador.

### Reação a dano

**Ao levar um tiro, o inimigo passa a perseguir o jogador independentemente de tê-lo visto.**
É uma regra fixa: o dano interrompe o que ele estava fazendo e força o estado `Perseguindo`.

A perseguição forçada só termina quando:

- o inimigo **alcança** o jogador (entra no alcance e vira `Atacando`), ou
- estoura o teto de segurança `tempoMaximoPerseguindo` (20 s) — que existe apenas para o
  inimigo não passar o resto da partida atrás de alguém que nunca vai alcançar.

### Navegação

O inimigo usa `NavMeshAgent` com `updateRotation = false`, ou seja, quem gira a nave é a máquina
de estados, não o agente. A "trombada" do estado `Andando` é detectada pelo próprio agente:
quando o caminho fica inválido ou parcial (`NavMeshPathStatus`), significa que há algo no
caminho — é o gatilho do giro.

Durante a perseguição o destino é recalculado a cada 0,2 s, para acompanhar o jogador em
movimento sem gastar processamento todo frame.

### Números do inimigo

Todos ficam em `ScriptableObject`s, ajustáveis sem tocar em código:

| Parâmetro | Valor padrão |
|---|---|
| Distância de visão | 45 m |
| Ângulo de visão | 160° |
| Alcance de ataque | 30 m |
| Velocidade andando | 6 |
| Velocidade perseguindo | 9 |
| Intervalo entre tiros | 1,6 s |
| Dano do projétil | 25 |
| Giro ao trombar | 15° a 180° |

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Dificuldade

Três dificuldades, escolhidas no menu antes de jogar:

| Dificuldade | Inimigos vivos | Spawn | Intensidade de IA |
|---|---|---|---|
| **Fácil** | 2 | a cada 3 s | 0,35 |
| **Médio** | 4 | a cada 2 s | 0,65 |
| **Difícil** | 6 | a cada 1 s | 1,00 |

> **Nota:** a intensidade de IA está exposta no `DifficultyConfig`, mas o jogo ainda **não a
> consome** — hoje o que muda de fato entre as dificuldades é o limite de inimigos e o
> intervalo de spawn. Os números do comportamento do inimigo são os mesmos nas três.

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Ranking

O ranking fica em `ranking.json`, salvo em `Application.persistentDataPath`. Guarda o **top 5
de cada dificuldade**, com desempate pelo registro mais recente.

A pontuação **só** é salva quando o jogador clica em um dos botões da tela de Game Over
(`SubmitAndStartNew` ou `SubmitAndReturnToMenu`). Sair da partida pelo atalho de `Esc` descarta
a pontuação de propósito.

Cada entrada guarda apelido (3 caracteres), pontos e dificuldade.

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Áudio

| Som | Quando toca |
|---|---|
| `tank-shot` | a cada disparo do jogador |
| `alien-shot` | a cada disparo do inimigo |
| `alien-ship` | em loop, em cada inimigo (3D — dá para localizar pelo som) |
| `music` | música de fundo, em loop |

Todos passam pelos volumes de `GameSettings` (`Master × Sfx` para efeitos, `Master × Música`
para a música) e acompanham os sliders mesmo com a partida em andamento.

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Saída da partida

Segurar `Esc` por **3 segundos** volta ao menu: aparece um aviso no centro da tela com a barra
de progresso enchendo. Soltar antes cancela. A partida sai **sem gravar a pontuação**.

Com a tela de Game Over aberta, o atalho fica inerte — ali quem manda são os botões.

<<<<<<< Updated upstream
<br>
=======
---

## Como executar

### Requisitos

- Unity **6** (`6000.3.18f1`) — outras versões 6.x devem funcionar, mas o projeto foi feito
  nesta
- Universal Render Pipeline (instalado como dependência do projeto)

### Passos

1. Abra o projeto no Unity Hub (`Add project from disk`).
2. Aguarde a importação e a compilação dos scripts.
3. Abra a cena `Assets/Scenes/menu_principal`.
4. Aperte **Play**.

Para gerar a build, adicione as duas cenas em **File → Build Settings**, nesta ordem:

```
Assets/Scenes/menu_principal.unity
Assets/Scenes/playground.unity
```

---
>>>>>>> Stashed changes

## Estrutura do projeto

```
Assets/
├── Scenes/
│   ├── menu_principal.unity     Menu, configurações, ranking
│   └── playground.unity         A arena
├── Scripts/
│   ├── Arena/                   Jogo: manager, spawner, HUD, ranking, áudio
│   │   ├── Bootstrap/           Instalação automática da partida
│   │   ├── Data/                ScriptableObjects (dificuldade, status do jogador)
│   │   ├── Menu/                Painéis do menu
│   │   └── Settings/            Volume e resolução
│   ├── Inimigo/                 IA: máquina de estados, tiro, configuração
│   └── PlayerMovement/          Tanque, torre, câmera, projéteis, mira
├── Prefabs/                     ArenaGame, Inimigo, ProjetilTanque
├── ScriptableObjects/           Dados ajustáveis (dificuldades, inimigos)
├── Resources/                   Assets carregados por nome em runtime
├── Sound/                       Efeitos e música
└── Aulas/                       Material das aulas (ver abaixo)
```

### Sobre `Assets/Aulas`

Esta pasta guarda todas as etapas de cada aula (`Versao1`, `Versao2`, `Passo1`…), mantidas como
registro do processo. Como todas ficam no mesmo projeto, as etapas intermediárias recebem um
sufixo no nome da classe (`MovimentoPlayerV1`, `ContadorPasso2`, `GeradorInimigosPasso1`…) — o
Unity não permite duas classes com o mesmo nome no mesmo assembly.

A versão final de cada aula mantém o nome original (`MovimentoPlayer`, `BarraVida`, `Contador`,
`GeradorInimigos`) e é a que vai para o jogo. O componente `Vida` e a `BarraVida` usados na
arena são justamente esses.

<<<<<<< Updated upstream
<br>
=======
---
>>>>>>> Stashed changes

## Decisões de projeto

**Máquina de estados com pesos, não `switch` por distância.** Cada estado declara se é possível
no frame atual e qual seu peso; o de maior peso vence. Isso tornou trivial ajustar o
comportamento por dificuldade e evitar o clássico "o inimigo pisca entre dois estados na
beirada do alcance" — que foi resolvido com uma noção única de percepção compartilhada pelos
três estados.

**Dano força a perseguição.** É uma regra de design: o jogador nunca fica impune por atirar de
longe. O inimigo vai atrás mesmo sem ver de onde veio o tiro.

**Pool de projéteis.** Um arena shooter gera muitos projéteis por segundo; `Instantiate` e
`Destroy` nesse volume causam picos de GC. A pool elimina isso.

**Criar as dependências em runtime.** O `ArenaGameInitializer` garante que o HUD, o áudio e o
atalho de saída existam, então a cena funciona mesmo se o prefab `ArenaGame` não estiver
montado. Isso reduziu muito o tempo perdido com "esqueci de arrastar tal referência".

**Proteções contra o `NavMeshAgent` de inimigo morto.** Quando o dano mata, o `Destroy` é
adiado: o componente ainda executa no mesmo frame, mas o agente já saiu do NavMesh. Mexer nele
nessa situação gera o erro *"Resume can only be called on an active agent"*. Por isso
`LevarDanoFazPerseguir` e `EntrarNoEstado` checam se o inimigo está vivo e no NavMesh antes de
tocar no agente.

<<<<<<< Updated upstream
<br>
=======
---

## Limitações conhecidas

- **`aiIntensity` não é consumida.** O campo existe nas dificuldades e aparece na descrição,
  mas hoje só o limite de inimigos e o intervalo de spawn mudam de fato entre elas.
- **Sem pausa.** Não há menu de pausa; a partida roda até o jogador morrer, sair pelo atalho de
  `Esc` ou voltar pelo Game Over.
- **Sem build para console.** O projeto é focado em PC. A matéria é *Jogos Digitais para
  Console*, mas a entrega rodou em Windows. O jogo **tem suporte completo a controle** (Xbox e
  PlayStation, ver [Controles](#controles)) — o que falta é gerar a build para o console.
- **Sem remapeamento pelo jogador.** O esquema funciona em Xbox e PlayStation, mas trocar um
  botão exige mexer no Inspector (ou no `TankInputActions`), não há tela de configuração.
- **Uma única cena de arena.** A arena (`playground`) é fixa.
- **Um tipo de inimigo.** Existe apenas o alienígena; não há variação de comportamento entre
  inimigos na mesma partida.

---
>>>>>>> Stashed changes

## Créditos

Trabalho desenvolvido para as matérias **Jogos Digitais para Console** e
**Inteligência Artificial em Jogos Digitais**.

Assets 3D e sons são de uso livre/educacional; a pasta `Assets/Aulas` contém o material das
aulas que serviu de base para o projeto.
