# AudioMixer — como criar e ligar

O código **já está pronto para o mixer**. `AudioSettings` procura um mixer em
`Assets/Resources/ArenaMixer` e aplica os volumes nos parâmetros expostos.
Sem o mixer, nada quebra: o volume geral vai para `AudioListener.volume` e os volumes de efeito e
música continuam disponíveis em `AudioSettings.EffectiveSfx` e `AudioSettings.EffectiveMusic`.

---

## 1. Criar o mixer

1. No Project, crie a pasta `Assets/Resources` (se ainda não existir — o script de assets já cria).
2. Clique com o botão direito em `Assets/Resources` → **Create → Audio → Audio Mixer**.
3. Nomeie exatamente **`ArenaMixer`** (o caminho final deve ser `Assets/Resources/ArenaMixer.mixer`).
4. Abra a janela **Window → Audio → Audio Mixer** e selecione `ArenaMixer`.

## 2. Criar os grupos

Na coluna **Groups**:

1. Renomeie `Master` para `Master` (já vem assim).
2. Clique com o botão direito em `Master` → **Add child group** e crie:
   - `Music`
   - `SFX`
3. O roteamento fica: `Music` → `Master`, `SFX` → `Master`.

## 3. Expor os parâmetros (nomes exatos)

Para cada grupo, selecione o grupo e, no painel **Inspector** (com **Attenuation** visível),
arraste o **Volume** do grupo — ou clique com o botão direito no valor de Volume → **Expose
'Volume' to script**:

| Grupo | Parâmetro exposto (nome exato) |
|---|---|
| `Master` | `MasterVolume` |
| `Music` | `MusicVolume` |
| `SFX` | `SfxVolume` |

O nome é editado na aba **Exposed Parameters** do Audio Mixer (parte superior direita):
clique no nome gerado (`MyExposedParam`) e digite o nome da tabela. **O nome precisa ser
idêntico**, sem espaços.

> Importante: os valores no mixer são **decibéis**, não 0..1. O código já converte:
> `dB = 20 · log10(valor)`, com piso de −80 dB. Não mude a conversão.

## 4. Ligar o mixer nos AudioSources

Todo `AudioSource` que tocar som no jogo deve apontar para um grupo:

| Tipo de som | `Output` do AudioSource |
|---|---|
| Música de fundo | `Music` |
| Tiros, explosões, UI | `SFX` |
| Voz / anúncios | `Master` (ou um grupo próprio) |

Exemplo no Inspector: AudioSource → **Output** → arraste o grupo `SFX` do `ArenaMixer`.

Se você usa `AudioSource` criados por código, defina
`source.outputAudioMixerGroup = mixer.FindMatchingGroups("SFX")[0];`.

## 5. Conferir

No Unity: menu **Arena → Check Audio Mixer**.

- Mixer encontrado + 3 parâmetros: `[Arena] AudioMixer OK.`
- Sem mixer: aviso explicando que os volumes ainda funcionam via `AudioListener.volume`.
- Mixer encontrado sem algum parâmetro: aviso listando os nomes que faltam.

Também é possível conferir em código:

```csharp
AudioMixer mixer = AudioSettings.Mixer;               // null se não existir
mixer.GetFloat("MasterVolume", out float db);          // true quando o parâmetro existe
AudioSettings.SetMixerVolume(mixer, "SfxVolume", 0.5f); // aplica 0.5 linear = -6 dB
```

## 6. Como o painel de configurações usa isso

- Os sliders gravam valores **lineares** (0..1) em `PlayerPrefs`:
  `Settings_MasterVolume`, `Settings_SfxVolume`, `Settings_MusicVolume`.
- **Aplicar** chama `GameSettings.Save(...)`, que chama `AudioSettings.ApplySaved()`.
- `ApplySaved()` aplica nos três parâmetros do mixer e espelha o master no `AudioListener.volume`.
- O jogo também aplica no início (`MenuManager.Awake` e `ArenaGameInitializer.Install`).

## 7. Se você não quiser usar o mixer

Não faça nada. Os volumes continuam persistentes e o master é aplicado no listener. Quando
adicionar sons, multiplique o volume pelo valor efetivo:

```csharp
source.volume = AudioSettings.EffectiveMusic; // música
source.PlayOneShot(clip, AudioSettings.EffectiveSfx); // efeito
```
