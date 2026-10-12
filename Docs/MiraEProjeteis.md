# Mira (retículos) e pool de projéteis

Mudanças em `Assets/Scripts/PlayerMovement/`. Resumo do que mudou, do que você precisa fazer no
Unity e de como um NPC pede projétil.

---

## 1. Retículos: o script não cria mais nada

`TankAimReticleHUD` perdeu toda a criação procedural. Ele **não** gera mais:

- o canvas `HUD_Mira` (nem `CanvasScaler`/`GraphicRaycaster`);
- os GameObjects `ReticuloCamera` / `ReticuloCanhao`;
- os sprites desenhados em `Texture2D` (`Sprite_ReticuloCamera` / `Sprite_ReticuloCanhao`);
- nem sobrescreve `sizeDelta`, cor, anchor ou pivot dos objetos.

Sobrou o que é responsabilidade do script: **posicionar**, **esconder quando a mira está atrás da
câmera** e **animar a recarga**.

O que você faz no Editor:

1. Selecione `HUD_Mira/ReticuloCamera` → `Image > Source Image` → seu sprite de mira central.
2. Faça o mesmo em `HUD_Mira/ReticuloCanhao`.
3. Confirme em cada um: `Anchor Min` = `Anchor Max` = `Pivot` = `0.5 / 0.5`
   (o script posiciona por `anchoredPosition` a partir do centro; se divergir, ele avisa no Console).
4. No `Tanque`, o componente `TankAimReticleHUD` já aponta para os dois RectTransforms (nada a fazer).

Referências que faltarem agora viram erro no Console em vez de objeto criado em código:

| Campo vazio | Comportamento |
|---|---|
| `Hud Canvas` | `LogError` + script se desabilita |
| só `Camera Reticle` | aviso; segue só com o retículo do canhão |
| só `Barrel Reticle` | aviso; segue só com o retículo central |
| `Camera` | `LogError` + se desabilita |

## 2. Offset dos retículos (pixels do canvas)

Dois campos novos no `TankAimReticleHUD`, um por retículo, somados à posição central:

```
Camera Reticle Offset   (x: 0, y: 0)     → ReticuloCamera
Barrel Reticle Offset   (x: 0, y: -18)   → ReticuloCanhao
```

- Unidade: **pixels do canvas** (pixels de referência do `CanvasScaler`, 1920×1080 no projeto). X
  cresce para a direita, Y para cima. O offset é somado *depois* da conversão para o espaço do
  canvas, então ele continua estável se a resolução mudar.
- É visual: a trajetória do tiro **não** muda (você escolheu offset 2D). Para mudar o ponto para
  onde o canhão aponta, continue usando `Aim Distance` e o `connectionOffset` do `TankTurretAim`.
- Em runtime, por script:

```csharp
hud.BarrelReticleOffset += new Vector2(0f, 4f);   // ex.: chute de recuo
hud.CameraReticleOffset = Vector2.zero;
hud.AddReticleOffset(new Vector2(2f, -1f));       // desloca os dois de uma vez
```

Caso você queira "mira deslocada = tiro deslocado", não é preciso mexer no HUD: o `TankPlayerInput`
usa `firePoint`/`barrelTransform` como origem e direção, e é lá que um offset de mira vira
compensação de ballistic drop (basta rotacionar/ajustar a direção do tiro em `TryRequestProjectile`).

## 3. Animação de recarga no retículo do canhão

O retículo que representa o canhão usa o **próprio `Image`** como medidor de cooldown — igual à
animação de recarga de habilidade: `Image.type = Filled` e `fillAmount` varrendo de 0 (disparou) a
1 (pronto). No fim da recarga o desenho volta a ficar inteiro.

Configurado no `TankAimReticleHUD`:

| Campo | Default | Para quê |
|---|---|---|
| `Animate Barrel Reload` | ligado | liga/desliga (ao desligar, o `Image` volta ao tipo que estava no Inspector) |
| `Shooter` | `TankPlayerInput` do Tanque | fonte do tempo de recarga (`Fire Rate`) |
| `Fill Method` | `Radial360` | varredura circular; há `Radial180/90`, `Horizontal`, `Vertical` |
| `Fill Origin` | `1` (Top) | de onde o "molho" começa |
| `Fill Clockwise` | ligado | sentido |
| `Reload Sweep` | `Recharges` | `Recharges` enche enquanto recarrega; `Depletes` esvazia |
| `Min Reload Duration` | `0.15` | piso da animação, para `fireRate` muito curto não virar um piscar |

Detalhes que o código resolve por você:

- A animação começa no **instante do disparo**, não no frame em que o HUD percebeu: se o canvas
  ficar um frame desligado, o preenchimento já volta no ponto certo (`cooldown` já decorrido), e se o
  cooldown já terminou nesse intervalo ele **não** inventa uma recarga atrasada.
- Disparo recusado (pool lotada) **não** anima — o HUD acompanha `ShotsFired`, e não o relógio.
- `Retículo do canhão sem Image` (ex.: `RawImage`) → aviso único e animação desligada; o
  posicionamento e o offset continuam funcionando.

API pública no HUD, caso você queira pilotar por fora (menu de opções, retículo de um NPC,
mira assistida):

```csharp
hud.PlayReload(0.75f);          // dispara a animação manualmente
bool recarregando = hud.IsReloading;
float p = hud.ReloadProgress;   // 0 = disparou agora, 1 = pronto
hud.SetReloadAnimated(false);
```

## 4. Pool de projéteis (player + NPCs, capacidade fixa)

Novo `TankProjectilePool`. A política é **teto rígido**: a pool cria no máximo `Capacity`
instâncias e, quando lotada, o pedido é **descartado** (um aviso a cada 20 recusas). Ela não cresce
em runtime.

- `Capacity` = 32 (default), `Prewarm Count` = 8 → 8 já criados no `Awake`, o resto criado sob
  demanda até o teto.
- Um projétil "morre" por impacto (`OnCollisionEnter`) ou por fim de vida — nos dois casos ele é
  **desativado e devolvido à pool**, nunca `Destroy`.
- O recolhimento acontece no `LateUpdate` da pool: desativar um `Collider` dentro de um callback de
  física é o jeito clássico de ganhar warning do PhysX.
- Os pares de `Physics.IgnoreCollision` com os colisores de quem disparou são **revertidos no
  retorno**. Sem isso, o projétil reusado atravessaria o tanque do atirador anterior (o ignore é
  permanente por par de colisores).
- `TankProjectile` fora de pool (prefab solto na cena, `Instantiate` direto) continua se
  autodestruindo — o script não exige a pool para existir.

### No projeto, hoje

`TankPlayerInput` não faz mais `Instantiate`: no `Awake` ele chama
`TankProjectilePool.Ensure(projectilePrefab, poolCapacity)`, que usa a pool atribuída, a pool da
cena, ou **cria um GameObject `PoolDeProjetis`** em runtime. Ou seja: *zero configuração obrigatória
para o player*. Quando a pool está lotada, o `TryFire()` retorna `false` e **não consome o
cooldown**.

### Como um NPC pede projétil

```csharp
// 1) mínimo — usa velocidade/vida do prefab
TankProjectile shot = TankProjectilePool.Spawn(muzzle.position, muzzle.forward);

// 2) com balística e vida próprias
TankProjectile shot = TankProjectilePool.Spawn(muzzle.position, aimDirection, speed: 28f, lifetime: 3f);

// 3) atalho que já coleta os colisores do dono (para o tiro não nascer dentro do próprio NPC)
TankProjectile shot = TankProjectilePool.SpawnAt(npcGameObject, muzzle, aimDirection, 28f, 3f);
```

Retorno `null` = pedido recusado (sem pool / sem prefab / lotada). Para reagir ao motivo:

```csharp
TankProjectile shot = pool.RequestProjectile(pos, dir, speed, life, myColliders,
                                             out TankProjectilePool.RequestResult result);
if (result == TankProjectilePool.RequestResult.PoolFull) { /* entra no estado de recarga, anda, etc. */ }
```

Num NPC "tanque" que também tem torre/cano, o pedido normalmente vem assim (mesma origem que o
player usa — a direção do `Trabuco`, não a do `transform.forward`):

```csharp
Vector3 aim = (target.position + Vector3.up * 1.2f) - muzzle.position;   // mira no peito com arco, se quiser
TankProjectilePool.SpawnAt(gameObject, muzzle, aim.normalized, 30f, 4f);
```

Diagnóstico (para um GUI de debug, por exemplo):

```csharp
pool.GetStats(out int emVoo, out int criados, out int capacidade, out int totalDisparados, out int recusados);
```

## 5. Arquivos

| Arquivo | O que houve |
|---|---|
| `TankAimReticleHUD.cs` | reescrito: sem criação de canvas/retículos/sprites; offsets em pixels; animação de recarga por `Filled`; aviso de anchor; resolução tardia do `TankPlayerInput` |
| `TankProjectilePool.cs` | **novo** — pool de teto rígido, API de instância + estática (`Spawn`/`SpawnAt`/`Ensure`), prewarm, `ReturnAll`, estatísticas |
| `TankPlayerInput.cs` | `TryFire` passa pela pool; cooldown não consumido em recusa; API pública `IsReady`/`CooldownProgress`/`CooldownRemaining`/`CooldownElapsed`/`ShotsFired`; campos `projectilePool`/`poolCapacity` |
| `TankProjectile.cs` | devolve-se à pool (com liberação dos ignores) em vez de `Destroy`; `RemainingLifetime`; tolerante a vida fora de pool |
| `Scenes/playground.unity` | bloco do HUD atualizado: `shooter` ligado ao `TankPlayerInput`, offsets e recarga serializados, `*Color`/`*Size` removidos |

`TankTurretAim`, `TankMovement` e `MouseOrbitCamera` não foram tocados.

## 6. Como foi verificado

Sem Unity neste ambiente, então a validação foi por compilação + execução:

- **typecheck** dos 6 scripts da pasta contra stubs da API do Unity 6/UGUI/Input System: 0 erros.
- **execução** dos scripts reais num mini-runtime (harness em `unity-check/` fora do repositório):
  78 checagens, 0 falhas — prewarm e reaproveitamento, posição/velocidade do spawn, cooldown não
  consumido em recusa, retorno por impacto e por fim de vida, reversão do `IgnoreCollision`,
  pedido de NPC pela API estática (inclusive recusa com a pool lotada), offset aplicado no ponto
  projetado, recarga acompanhando o cooldown real e o HUD reativado no meio de um cooldown.
- não verificado (requer o Editor): aparência do `fillAmount` com os seus sprites, comportamento
  físico do projétil e o resultado visual na tela.
