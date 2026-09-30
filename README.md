# arena-jw

## Controles

- **W/S** ou **↑/↓**: avançar e recuar na direção do próprio tanque.
- **A/D** ou **←/→**: girar o próprio tanque.
- **Mouse**: orbitar a câmera; a torre acompanha horizontalmente e o Trabuco acompanha a inclinação vertical.
- **Esc**: liberar o cursor.
- **Clique esquerdo**: capturar o cursor novamente.

A direção do movimento é independente da câmera: a tração usa `transform.forward` do GameObject do tanque.
