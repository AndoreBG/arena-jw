# arena-jw

## Controles

- **W/S** ou **↑/↓**: avançar e recuar na direção do próprio tanque.
- **A/D** ou **←/→**: girar o próprio tanque.
- **Mouse**: orbitar a câmera; a torre acompanha horizontalmente e o Trabuco acompanha a inclinação vertical.
- **Esc**: liberar o cursor.
- **Clique esquerdo**: capturar o cursor novamente.

A direção do movimento é independente da câmera: a tração usa `transform.forward` do GameObject do tanque.

## Scripts das aulas

As pastas `Assets/Aulas` guardam todas as etapas de cada aula (`Versao1`, `Versao2`, `Passo1`...).
Como todas ficam no mesmo projeto, as etapas intermediárias recebem um sufixo no nome da classe
(`MovimentoPlayerV1`, `ContadorPasso2`, `GeradorInimigosPasso1`...) — o Unity não permite duas
classes com o mesmo nome no mesmo assembly. A versão final de cada aula mantém o nome original
(`MovimentoPlayer`, `BarraVida`, `Contador`, `GeradorInimigos`) e é a que vai para o jogo.
