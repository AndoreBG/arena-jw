# Arena JW

Jogo 3D de sobrevivência em arena, desenvolvido em **Unity 6** para as disciplinas **Jogos Digitais para Console** e **Inteligência Artificial em Jogos Digitais**.

Pilote um tanque, enfrente naves alienígenas e sobreviva pelo maior tempo possível. Cada inimigo destruído vale um ponto. Ao final da partida, registre seu apelido e dispute um lugar no ranking da dificuldade escolhida.

**[Baixar o jogo](https://github.com/AndoreBG/arena-jw/releases)** · **[Código-fonte](https://github.com/AndoreBG/arena-jw)**

## Sumário

- [Sobre o jogo](#sobre-o-jogo)
- [Como jogar](#como-jogar)
- [Controles](#controles)
- [Menu e configurações](#menu-e-configurações)
- [Sistemas de jogo](#sistemas-de-jogo)
- [Efeitos visuais](#efeitos-visuais)
- [Inteligência artificial](#inteligência-artificial)
- [Dificuldades](#dificuldades)
- [Ranking e persistência](#ranking-e-persistência)
- [Áudio](#áudio)
- [Executar no Unity](#executar-no-unity)
- [Gerar uma build](#gerar-uma-build)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Contexto acadêmico](#contexto-acadêmico)
- [Limitações e pontos de atenção](#limitações-e-pontos-de-atenção)
- [Documentação complementar](#documentação-complementar)
- [Autoria e créditos](#autoria-e-créditos)

## Sobre o jogo

| Característica | Descrição |
|---|---|
| Gênero | Arena shooter 3D em terceira pessoa |
| Plataforma de distribuição | PC — Windows |
| Engine | Unity 6 — `6000.3.18f1` |
| Renderização | Universal Render Pipeline — `17.3.0` |
| Entrada | Teclado, mouse e gamepad pelo Input System — `1.19.0` |
| Navegação dos inimigos | AI Navigation — `2.0.13`, com NavMeshAgent |
| Objetivo | Sobreviver e acumular pontos |
| Fim da partida | Vida do jogador chega a zero |

O projeto implementa suporte a gamepads reconhecidos pelo Input System, com referências de botões Xbox e PlayStation. Isso não significa que exista uma build para consoles.

## Como jogar

1. Acesse a página de [Releases](https://github.com/AndoreBG/arena-jw/releases).
2. Baixe e extraia o pacote Windows disponibilizado na release.
3. Execute `Arena-JW.exe`, mantendo os demais arquivos e pastas da build juntos.
4. No menu principal, selecione **Jogar** e escolha a dificuldade.
5. Movimente o tanque, mire e destrua as naves para pontuar.
6. No Game Over, informe um apelido de três caracteres e escolha **Tentar Novamente** ou **Menu Principal** para enviar a pontuação.

> Sair durante a partida pelo atalho de retorno ao menu descarta a pontuação daquela tentativa.

## Controles

### Durante a partida

| Ação | Teclado / mouse | Xbox | PlayStation |
|---|---|---|---|
| Acelerar / recuar | `W` / `S` ou `↑` / `↓` | Analógico esquerdo vertical ou D-pad | Analógico esquerdo vertical ou D-pad |
| Girar o tanque | `A` / `D` ou `←` / `→` | Analógico esquerdo horizontal ou D-pad | Analógico esquerdo horizontal ou D-pad |
| Mirar e orbitar a câmera | Mouse | Analógico direito | Analógico direito |
| Atirar | Clique esquerdo ou `Espaço` | `RT`, `A` ou `RB` | `R2`, `✕` ou `R1` |
| Voltar ao menu sem salvar | Segurar `Esc` por 3 segundos | Segurar `Y` por 3 segundos | Segurar `△` por 3 segundos |

O movimento usa a orientação do próprio tanque, independentemente da câmera. A torre possui controle de mira separado do chassi. O analógico direito utiliza zona morta para reduzir movimentos involuntários.

Ao segurar o atalho de saída, um aviso apresenta o progresso. Soltar antes de completar o tempo cancela o retorno. Na tela de Game Over, esse atalho fica desabilitado.

### Menus e teclado virtual

- Navegue com D-pad ou analógico esquerdo.
- Acione o elemento selecionado com o botão de confirmação (`A` / `✕`).
- Ao usar o controle no campo de apelido, o Game Over oferece um teclado virtual com `A–Z` e `0–9`.
- Use `X` / `□` para apagar o último caractere.
- Use `B` / `○` para fechar o teclado e retornar ao campo de apelido.
- Selecione o botão **OK** do teclado para seguir para a confirmação da partida.
- Ao completar três caracteres, o foco passa para **OK**.
- O teclado físico continua disponível para digitação direta.

> O botão físico `B` / `○` fecha o teclado; ele não executa a mesma ação do botão visual **OK**, apesar do rótulo atual `OK (B)`.

## Menu e configurações

O menu principal permite iniciar uma partida, abrir as configurações ou sair do jogo. Antes do combate, o jogador escolhe entre Fácil, Médio e Difícil.

As configurações incluem:

- Volume geral, de efeitos e de música.
- Resolução.
- Modo de exibição: janela, janela sem bordas ou tela cheia exclusiva.
- Botão **Aplicar** para salvar e aplicar as opções.
- Confirmação ao tentar voltar com alterações não aplicadas.

As preferências são persistidas em `PlayerPrefs`. As mudanças de resolução são aplicadas na execução fora do Editor.

## Sistemas de jogo

### Movimento, câmera e mira

O tanque utiliza `Rigidbody` e `WheelColliders`. A câmera orbital acompanha o jogador com suavização, enquanto a torre e o cano se orientam de acordo com a mira.

Dois indicadores ajudam a visualizar o alinhamento: a direção apontada pela câmera e a direção do cano.

### Projéteis

Jogador e inimigos compartilham o sistema de projéteis, utilizando `TankProjectilePool` para reutilizar objetos em vez de criar e destruir um novo objeto a cada disparo.

`TankShotData` identifica o dono, a facção e o dano do tiro. As colisões do próprio dono são ignoradas para evitar autoacertos na saída do projétil.

### Vida e HUD

O componente `Vida` centraliza o recebimento de dano e os eventos de alteração de vida e morte. O HUD apresenta vida do jogador, pontuação e dificuldade. Os inimigos também possuem barras de vida voltadas para a câmera.

Quando o jogador morre, a partida é congelada e a interface de Game Over é exibida. O teclado virtual utiliza tempo não escalado para continuar funcionando com `Time.timeScale = 0`.

## Efeitos visuais

### Shader do escudo

O arquivo `Assets/Shaders/EscudoDoTanque.shader` implementa um efeito em HLSL com:

- Fresnel para realçar o contorno conforme o ângulo da câmera.
- Padrão procedural de células animadas.
- Pulsação de brilho ao longo do tempo.
- Transparência e blending aditivo.

`EscudoDoTanque.cs` controla a exibição do efeito e seus parâmetros conforme o dano recebido e a vida restante. O padrão é estilizado; não representa uma malha hexagonal geometricamente exata.

### Partículas

`ExplosaoDeParticulas.cs` configura o Particle System por código para impactos e explosões de morte dos inimigos.

O efeito utiliza emissão em rajada, distribuição esférica, velocidade, gravidade, gradiente de cor, transparência e redução de tamanho ao longo da vida. O objeto é destruído automaticamente ao terminar.

O material de partículas fica em `Assets/Resources/ExplosaoDeParticulas.mat`.

## Inteligência artificial

Cada nave é um agente reativo: percebe o jogador, seleciona um comportamento e executa movimento ou ataque. Não há aprendizado de máquina.

### Máquina de estados

A lógica está em `InimigoMaquinaDeEstados.cs` e utiliza três estados:

| Estado | Comportamento |
|---|---|
| **Andando** | Desloca-se em frente e escolhe outra direção quando encontra condições de bloqueio ou término do caminho. |
| **Perseguindo** | Atualiza o destino do NavMeshAgent para se aproximar do jogador. |
| **Atacando** | Interrompe o deslocamento, orienta-se para o jogador e permite disparos. |

A seleção considera pesos configuráveis. Os valores do asset `InimigoFacil` são `40` para Andando, `60` para Perseguindo e `100` para Atacando. No empate, a seleção prioriza Atacando, depois Perseguindo e depois Andando.

Os pesos representam prioridade, não probabilidade. Existem transições diretas que não passam pela disputa de pesos, como a reação a dano e a entrada em ataque ao alcançar o jogador durante uma perseguição.

### Percepção

O sensor visual combina distância, ângulo e raycast. No asset atual do inimigo:

| Parâmetro | Valor |
|---|---|
| Distância de visão | 45 m |
| Ângulo de visão | 160° |
| Alcance inicial de ataque | 30 m |
| Velocidade andando | 6 |
| Velocidade perseguindo | 9 |
| Intervalo entre tiros | 1,6 s |
| Dano por projétil | 25 |

Há também percepção por proximidade em até 70% da distância de visão: com 45 m de visão, o raio de proximidade é 31,5 m.

**Proximidade não é audição:** o sistema verifica distância, não eventos sonoros. Essa verificação também não consulta paredes, permitindo perceber o jogador atrás de obstáculos dentro do raio.

### Reação a dano e ataque

Receber dano pode forçar a perseguição mesmo sem detecção visual. A perseguição possui um limite de segurança de 20 segundos e pode terminar ao alcançar o jogador.

`TiroDoInimigo.cs` executa os disparos quando a máquina permite atacar. A mira utiliza o corpo do jogador como alvo, e os projéteis saem da mesma pool usada pelo tanque.

### Navegação

O cenário contém dados de NavMesh. Os inimigos utilizam NavMeshAgent, e o spawner procura posições válidas com `NavMesh.SamplePosition()`.

Durante a perseguição, o destino é atualizado em intervalos de 0,2 segundo. Os parâmetros do comportamento ficam em `ConfiguracaoDoInimigo`, um ScriptableObject ajustável pelo Inspector.

## Dificuldades

| Dificuldade | Limite de inimigos vivos | Intervalo entre tentativas de geração |
|---|---|---|
| Fácil | 2 | 3 s |
| Médio | 4 | 2 s |
| Difícil | 6 | 1 s |

Os valores ficam nos assets de `Assets/ScriptableObjects/Difficulties/`.

No estado atual, a diferença efetiva entre as dificuldades está no limite de inimigos e na frequência de geração. O campo `aiIntensity` existe na configuração, mas não é consumido pela lógica atual da IA. O projeto possui um único asset de configuração de inimigo.

## Ranking e persistência

O ranking é local, salvo em:

```text
Application.persistentDataPath/ranking.json
```

Regras:

- Top 5 por dificuldade.
- Apelido de exatamente três caracteres: letras `A–Z` ou números `0–9`.
- Letras digitadas são normalizadas para maiúsculas.
- Uma entrada por apelido em cada dificuldade.
- Um novo resultado substitui o mesmo apelido apenas se superar a pontuação anterior.
- Em empate de pontos entre registros, o mais recente tem prioridade.

O envio acontece pelos botões **Tentar Novamente** e **Menu Principal** do Game Over. Um resultado fora do top 5 não altera o ranking. Abandonar a partida pelo atalho não envia pontuação.

## Áudio

O jogo possui sons de tiro do tanque, tiros dos inimigos, loop espacial das naves e música de fundo.

`AudioDaArena.cs` gerencia as fontes e acompanha os valores das configurações. O sistema também possui integração com AudioMixer para os parâmetros de volume geral, música e efeitos.

## Executar no Unity

### Requisitos de desenvolvimento

- Unity Hub.
- Unity Editor **6000.3.18f1**.
- Para build Windows, o módulo de suporte correspondente instalado no Editor.

As dependências do projeto estão declaradas em `Packages/manifest.json` e são resolvidas pelo Package Manager.

### Passos

```bash
git clone https://github.com/AndoreBG/arena-jw.git
```

1. No Unity Hub, adicione a pasta clonada como projeto existente.
2. Abra com a versão indicada do Editor.
3. Aguarde a importação dos assets e a compilação dos scripts.
4. Abra `Assets/Scenes/menu_principal.unity`.
5. Entre em **Play**.

Utilizar a versão indicada reduz o risco de incompatibilidades de pacotes e serialização.

## Gerar uma build

1. Abra **File → Build Profiles** no Unity 6.
2. Selecione ou crie um perfil para Windows.
3. Confira se as cenas estão habilitadas nesta ordem:

   ```text
   0 — Assets/Scenes/menu_principal.unity
   1 — Assets/Scenes/playground.unity
   ```

4. Gere a build em uma pasta separada dos assets do projeto.
5. Teste o executável fora do Editor, incluindo áudio, shader, partículas, persistência e navegação por controle.
6. Para distribuir, compacte a pasta completa da build — não apenas o executável.

## Estrutura do projeto

```text
Assets/
├── Aulas/                  Material e versões dos exercícios das aulas
├── Editor/                 Ferramentas de configuração no Editor
├── Input/                  Ações e bindings do Input System
├── Prefabs/                ArenaGame, Inimigo e ProjetilTanque
├── Resources/              Assets carregados em tempo de execução
├── Scenes/
│   ├── menu_principal.unity
│   ├── playground.unity
│   └── playground/         Dados de NavMesh
├── ScriptableObjects/      Dificuldades, inimigo e outros parâmetros
├── Scripts/
│   ├── Arena/
│   │   ├── Bootstrap/      Inicialização e dependências da partida
│   │   ├── Data/           Definições de dados
│   │   ├── Debug/          Diagnóstico
│   │   ├── Efeitos/        Escudo e partículas
│   │   ├── Menu/           Navegação, painéis e teclado virtual
│   │   └── Settings/       Configurações persistentes
│   ├── Inimigo/            Estados, configuração e arma da IA
│   └── PlayerMovement/     Movimento, câmera, torre, mira e projéteis
├── Settings/               Configurações de renderização
├── Shaders/                Shader e material do escudo
└── Sound/                  Música e efeitos sonoros
Docs/                       Documentação técnica complementar
Packages/                   Dependências do projeto
ProjectSettings/            Configurações da Unity
```

A pasta `Assets/Aulas` preserva etapas do desenvolvimento. Versões intermediárias possuem nomes de classe distintos para evitar conflitos de compilação. Componentes como `Vida` e `BarraVida` são utilizados pelo jogo.

## Contexto acadêmico

### Jogos Digitais para Console

| Tema | Aplicação no projeto |
|---|---|
| Shader | Escudo de energia em HLSL com Fresnel e animação procedural. |
| Configurações | Áudio, resolução, modo de tela e persistência. |
| Partículas | Impactos e explosões de inimigos. |
| Joystick | Combate, navegação da interface e teclado virtual. |
| Menu principal | Entrada no jogo, escolha de dificuldade, configurações e saída. |
| Colaboração | Deve ser demonstrada por registros reais de participação, testes ou feedback; o código isoladamente não comprova esse tópico. |

### Inteligência Artificial em Jogos Digitais

| Tema | Aplicação no projeto |
|---|---|
| Arena | Ambiente de sobrevivência com geração de inimigos e pontuação. |
| Agente inteligente | Naves que percebem, decidem e agem de forma autônoma. |
| IA simples | Máquina de estados com prioridades por pesos e transições especiais. |
| Cenário para IA | NavMesh, obstáculos e validação de posições de geração. |
| Sensor de detecção | Distância, cone visual, raycast e proximidade. |
| Sensor para ação | Percepção e distância orientando perseguição e ataque. |

## Limitações e pontos de atenção

- **Plataforma:** a distribuição é para Windows; não há build de console documentada.
- **Sem pausa manual:** há congelamento no Game Over, mas não um menu de pausa durante a partida.
- **Sem remapeamento na interface:** alterações de bindings exigem edição do projeto.
- **Conteúdo:** uma arena e um tipo principal de inimigo.
- **Dificuldade:** `aiIntensity` não altera o comportamento atual.
- **Percepção:** a proximidade ignora obstáculos. O raycast visual aceita o próprio inimigo como primeiro impacto válido, sem continuar a consulta para verificar obstáculos adiante.
- **Ataque:** não há uma nova checagem independente de linha de visão a cada disparo; o inimigo pode tentar atirar contra paredes. O bloqueio do projétil depende de suas colisões.
- **Resoluções:** a ordenação dos assets deve ser consistente entre a lista do menu e a aplicação da opção salva por índice.
- **Teclado virtual:** o rótulo `OK (B)` não corresponde à ação física do botão `B` / `○`, que fecha o teclado retornando ao campo.
- **Validação:** suporte implementado não substitui testes na build e em controles físicos de diferentes modelos.

## Documentação complementar

- [Implementação da arena](Docs/ArenaImplementation.md)
- [Configuração de áudio e AudioMixer](Docs/AudioMixer.md)
- [Mira e projéteis](Docs/MiraEProjeteis.md)
- [Conversão de cena para YAML](Docs/ConversaoCenaYAML.md)

A documentação complementar registra decisões e etapas de desenvolvimento; em caso de divergência, consulte a implementação atual.

## Autoria e créditos

Projeto de **André B Gorzoni**, desenvolvido no contexto das disciplinas **Jogos Digitais para Console** e **Inteligência Artificial em Jogos Digitais**.

O projeto inclui material das aulas e assets de terceiros. As condições de uso e redistribuição de cada recurso devem ser verificadas em suas respectivas fontes; o uso acadêmico não substitui a licença de um asset.
