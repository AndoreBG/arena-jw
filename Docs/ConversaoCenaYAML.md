# Converter a cena do Playground para YAML (texto)

Pergunta original: *"Como posso transformar a scene em YAML para que você consiga editar?"*

## Resposta curta

**Não é necessário para esta implementação.** Mas a conversão ajuda em trabalhos futuros,
então segue o passo a passo — e um aviso importante no final.

---

## 1. Como converter (passo a passo no Unity)

1. **Faça backup / commit** antes (a conversão regrava o arquivo inteiro).
2. Feche o Unity (ou pelo menos a cena).
3. Abra **Edit → Project Settings → Editor**.
4. Em **Asset Serialization**, mude **Mode** de `Force Binary` para **`Force Text`**.
5. Feche e reabra o projeto.
6. Abra cada cena, dê `Ctrl+S` (o Unity regrava a cena no modo texto) e faça commit.
7. Confirme que o `EditorSettings.asset` ficou com `m_SerializationMode: 2`.

O `playground.unity` neste repositório é binário (`%YAML` não aparece no começo do arquivo;
os primeiros bytes são o cabeçalho binário do Unity). Depois do passo 6, ele passa a começar com
`%YAML 1.1` e vira um arquivo de texto — diff e merge funcionam normalmente.

Observação: `m_SerializationMode: 2` já está configurado no `ProjectSettings/EditorSettings.asset`
deste repositório, mas a cena foi salva antes disso e continuou binária. Só o resave no Editor
converte o arquivo.

---

## 2. Por que eu **não** converti o arquivo aqui

Editar uma cena binária por fora do Unity exige reimplementar o formato serializado do motor.
Converter para YAML "na mão" seria reescrever 500 KB de cena — com alto risco de perder:

- o `NavMeshData` embutido (o bake da NavMesh vive dentro da cena);
- as referências de `fileID` entre objetos, prefabs e materiais;
- os componentes com `m_Script` apontando por GUID.

Um erro em qualquer `fileID` = cena corrompida, e o Unity não abre. Por isso a automação foi
feita pelo caminho seguro: **scripts de Editor** que usam a API do Unity (`PrefabUtility`,
`EditorSceneManager`, `SerializedObject`). Eles editam a cena binária sem tocar no formato.

---

## 3. Quando vale a pena converter

| Vale a pena | Não vale |
|---|---|
| Vários devs mexendo na mesma cena | Projeto solo |
| Revisar mudanças de cena em pull request | Cena editada por script de Editor |
| Integração/CI que inspeciona a cena | Só rodar e jogar |

Como toda a fiação do Playground agora é feita por `Arena → Setup Main Menu + Playground`,
o YAML do Playground não é necessário para a implementação do planejamento. Se você converter
depois, o script continua funcionando igual: ele opera pela API, não lê o texto do arquivo.

---

## 4. Depois de converter

1. Rode `Arena → Setup Main Menu + Playground` uma vez.
2. `git diff` vai mostrar a cena como texto. Confira que os `NavMeshData` aparecem como bloco de
   bytes (é normal: o NavMesh serializado continua sendo um binário dentro do YAML).
3. Nunca edite `fileID` à mão. Se precisar mexer em campos, prefira os scripts de Editor.
