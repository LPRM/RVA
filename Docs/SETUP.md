# Guia de configuração do projeto

Este guia cobre a Fase 0 (preparação) e a montagem da cena da Fase 1. Está fora de `Assets/`,
por isso o Unity ignora-o.

Versões: **Unity 6000.6.3f1**, **Vuforia Engine 11.4.4**, URP, Input System ("Input System Package only").

---

## 1. Instalar o Vuforia (cada pessoa, uma vez)

O pacote do Vuforia é um ficheiro `com.ptc.vuforia.engine-11.4.4.tgz` com 138 MB. Passa o limite de
100 MB por ficheiro do GitHub, por isso **não vai para o repositório** (está no `.gitignore`).
Cada pessoa instala-o no seu computador:

1. Descarrega **Vuforia Engine 11.4.4 for Unity** (`add-vuforia-package-11-4-4.unitypackage`) em
   https://developer.vuforia.com → Downloads → SDK (precisa de conta gratuita).
2. Com o projeto aberto: **Assets › Import Package › Custom Package…**, escolhe o ficheiro e carrega em **Import**.
3. Aparece a janela **"Add Vuforia Engine Package"**: carrega em **Update**. O `.tgz` passa para
   `Packages/` e o `Packages/manifest.json` fica com `"com.ptc.vuforia.engine": "file:com.ptc.vuforia.engine-11.4.4.tgz"`.
4. **Se a janela não aparecer** (acontece quando o `manifest.json` já aponta para o ficheiro, ou seja,
   depois de alguém ter feito commit dessa linha): fecha o Unity, move
   `Assets/Editor/Migration/com.ptc.vuforia.engine-11.4.4.tgz` para `Packages/`, apaga o `.tgz.meta`
   que ficou em `Assets/Editor/Migration/` e volta a abrir o projeto. Também podes simplesmente copiar o
   `.tgz` de um colega para a tua pasta `Packages/`.
5. Confirma em **Window › Package Manager** que aparece "Vuforia Engine AR" 11.4.4 sem erros.

Os scripts do jogo compilam mesmo sem o Vuforia (o `MazeTracker` desliga-se e joga-se com as setas),
por isso uma instalação em falta não bloqueia o resto do trabalho.

---

## 2. Estrutura de pastas

```
Assets/
  Scripts/                  código do jogo (assembly RVA.Game, namespace RVA)
    RVA.Game.asmdef
    Tracking/MazeTracker.cs     estado de tracking do marcador + pose suavizada (Vuforia)
    Maze/MazeLayout.cs          lê as grelhas de texto
    Maze/MazeBuilder.cs         constrói o labirinto: cópia física escondida + cópia visível
    Maze/MazeMirror.cs          copia bola e portões da cópia física para a visível
    Maze/MazeGravity.cs         inclinação → direção da gravidade; Calibrar; teclado
    Maze/MazeBall.cs            bola: gravidade própria, congelar, buracos, eventos
    Maze/MazeGate.cs            portão que desce para o chão quando abre
    Maze/MazeTrigger.cs         marca buracos, saídas e checkpoints
    DebugTools/MazeDebugView.cs vista da cópia física no canto do ecrã (tecla V)
    Gameplay/                   (Fase 2) KeyGate, CheckpointManager, GameManager
    Weather/                    (Fase 3) WeatherService, WeatherEffects
    UI/                         (Fase 3) UIManager
  Levels/                   grelhas dos níveis (Level1.txt, ...)
  Markers/                  imagens dos marcadores: labirinto, chave vermelha, chave azul
  Scenes/Maze.unity         cena principal
  CREDITS.md                recursos de terceiros
Docs/SETUP.md               este guia
```

Os scripts em `Assets/Scripts/` compilam num assembly próprio (`RVA.Game.asmdef`). É isso que permite
compilar sem o Vuforia. Se um script novo precisar de outro package, acrescenta-o em
**References** no Inspector do `RVA.Game.asmdef` (Input System, TextMeshPro e UI já lá estão).

### Formato dos níveis

```
###########
#S....#...#
#.###.#.#.#
#...O...C.#
#.R...#..E#
###########
```

| Carácter | Significado |
|---|---|
| `#` | parede |
| `.` | chão |
| `S` | início da bola (só um) |
| `E` | saída |
| `O` | buraco (a bola cai e volta ao ponto de respawn) |
| `C` | checkpoint |
| `R` / `B` | portão vermelho / azul (abre com a chave da mesma cor, Fase 2) |

Linhas começadas por `;` são comentários. A primeira linha da grelha é o lado de cima da imagem do marcador.

---

## 3. Montar a cena da Fase 1

Não editamos `.unity` nem `.prefab` por código, por isso isto faz-se no Editor (5 minutos):

1. **File › New Scene**, modelo *Basic (URP)* ou *Standard (URP)*. Guarda como `Assets/Scenes/Maze.unity`.
2. Apaga a **Main Camera**.
3. **GameObject › Vuforia Engine › AR Camera**.
4. Licença: seleciona a ARCamera → no Inspector, **Open Vuforia Engine configuration** → cola a chave em
   **App License Key** (gera-se em developer.vuforia.com → Plan & Licenses → License Manager).
   Neste momento esse campo está vazio no `VuforiaConfiguration.asset`.
5. Põe a imagem do marcador do labirinto em `Assets/Markers/` (PNG ou JPG com muito detalhe e contraste;
   evitar padrões repetidos e zonas lisas).
6. **GameObject › Vuforia Engine › Image Target**, muda o nome para **MazeTarget** e no Inspector:
   - **Type: From Image**, **Image:** a imagem do passo 5;
   - **Width:** a largura *impressa* em metros (A5 ao alto = `0.148`, A5 deitado = `0.210`).
7. No MazeTarget: **Add Component › Maze Tracker**. (O `DefaultObserverEventHandler` pode ficar.)
8. Cria um GameObject vazio **Game** na raiz da cena e adiciona **Maze Builder**, **Maze Gravity**,
   **Maze Mirror** e **Maze Debug View**.
   No Maze Builder, arrasta `Assets/Levels/Level1.txt` para **Level File**. As restantes referências
   são encontradas automaticamente.
9. **File › Build Profiles › Scene List**: acrescenta `Maze` e tira a `SampleScene`.
10. Guarda a cena e carrega em **Play**.

Hierarquia final:

```
Directional Light
ARCamera
MazeTarget          (ImageTarget, From Image) + MazeTracker
  └ MazeVisual      ← criado em runtime pelo MazeBuilder
Game                MazeBuilder, MazeGravity, MazeMirror, MazeDebugView
MazePhysics (hidden)  ← criado em runtime, 1000 m abaixo da cena, na layer 29 (a ARCamera não a desenha)
```

### Controlos de teste (Play Mode)

| Tecla | Ação |
|---|---|
| Setas | inclinar o labirinto (modo teclado) |
| K | alternar entre teclado e marcador |
| C | calibrar: a pose atual do marcador passa a ser "nivelada" |
| R | pôr a bola no ponto de respawn |
| , e . | rodar o labirinto 15° (o que o slider de rotação vai fazer na Fase 3) |
| V | mostrar/esconder a vista da cópia física (canto inferior direito) |

No Editor o jogo arranca em **modo teclado** e a vista física está ligada, por isso dá para testar
a física e os níveis sem marcador nem webcam. O painel no canto superior esquerdo mostra o estado.

### "Pronto quando" (Fase 1)

- [ ] Sem marcador: as setas fazem a bola rolar na vista física, as paredes seguram-na e o buraco faz
      a bola cair e voltar ao início.
- [ ] `K` para modo marcador e o marcador em frente à webcam: o labirinto aparece em cima do marcador
      e calibra sozinho ao fim de meio segundo (ou com `C`, com o marcador na posição "nivelada").
- [ ] Inclinar o papel faz a bola rolar para o lado certo.
- [ ] Tirar o marcador da vista congela a bola; quando volta, a bola continua.

Valores a afinar no Inspector (em Play Mode, para ver logo o efeito; anota os bons e repõe-nos depois):
`MazeGravity` → Gravity Strength, Max Tilt, Dead Zone, Tilt Sensitivity, Gravity Sharpness;
`MazeTracker` → Rotation Sharpness; `MazeBuilder` → Visual Size, Ball Linear Damping.

Se a bola rolar para o lado errado com o marcador, ou o labirinto aparecer rodado ou fora do sítio,
anotem o que acontece (e o que diz o painel) para corrigirmos.

---

## 4. Conteúdo de exemplo que pode ser apagado

Apagar **dentro do Unity** (janela Project), para irem também os `.meta`.

| Caminho | O que é |
|---|---|
| `Assets/TutorialInfo/` | readme do modelo URP |
| `Assets/Readme.asset` | o mesmo readme |
| `Assets/Editor/Vuforia/` | imagens e PDFs de exemplo da Vuforia (Mars) |
| `Assets/StreamingAssets/Vuforia/VuforiaMars_Images.*` | base de dados de exemplo; com targets "From Image" não precisamos de base de dados |
| `Assets/Scenes/SampleScene.unity` | cena de exemplo (Cube em cima do marcador Mars). Útil para testar o Vuforia até a `Maze.unity` funcionar; depois pode ir |

**Não apagar:**

| Caminho | Porquê |
|---|---|
| `Assets/Editor/Migration/AddVuforiaEnginePackage.cs` | instala o Vuforia a partir do `.unitypackage` |
| `Assets/Resources/VuforiaConfiguration.asset` | licença e configuração do Vuforia |
| `Assets/Settings/` | configuração do URP |
| `Assets/InputSystem_Actions.inputactions` | ações do Input System do projeto |
