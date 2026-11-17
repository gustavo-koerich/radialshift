# RadialShift — Especificação técnica

> Nome provisório. Troque à vontade; o nome aparece só no namespace, no nome do executável e na pasta de logs.

## 1. Objetivo

App residente para Windows 10/11. O usuário seleciona um ou mais arquivos no Explorer com clique esquerdo, dá um toque na tecla Shift, e um menu radial (anel) abre em volta do cursor com ações para os arquivos selecionados: converter para PDF, juntar PDFs, gerar Excel a partir de NF-e etc. As ações rodam em segundo plano, e o resultado é salvo na mesma pasta do arquivo original.

## 2. Arquitetura

```
┌──────────────────────────────────────────────┐
│ RadialShift.App  (C# .NET 8, WPF, bandeja)    │
│                                               │
│  KeyboardHook ─► TriggerDetector              │
│                       │                       │
│                       ▼                       │
│  ExplorerSelection (COM + UI Automation)      │
│                       │                       │
│                       ▼                       │
│  RadialWindow (anel)  ◄── ActionCatalog       │
│                       │      (acoes.json)     │
│                       ▼                       │
│  ActionRunner ──► processo filho ─────────────┼──► RadialShift.Ferramentas.exe (C#)
│       │                                       │       lê JSON, executa, devolve JSON
│       ▼                                       │
│  Notificações (toast) + Log                   │
└──────────────────────────────────────────────┘
```

Decisão: tudo em C# .NET 8, sem Python (revisado em 2026-10-24; antes as ações eram em Python). O núcleo fica em WPF (hook nativo estável, janela transparente com animação fluida, inicialização instantânea). As ações ficam num segundo executável C# de console, `RadialShift.Ferramentas.exe`, chamado como processo filho: isola falhas, permite matar uma ação travada (e o Office que ela abriu) sem derrubar o app, e mantém o contrato JSON da seção 7. Bibliotecas: PDFsharp (PDF, MIT), ClosedXML (Excel, MIT), `System.Xml.Linq` (NF-e) e COM do Office. Os dois exes são publicados *self-contained*: a máquina de destino não precisa ter .NET nem Python instalados.

## 3. Gatilho (toque no Shift)

### 3.1 Regras de disparo — modo `tap` (padrão)

Dispara somente se TODAS forem verdadeiras:

1. `KeyDown` de `VK_LSHIFT` ou `VK_RSHIFT` seguido de `KeyUp` da mesma tecla em até **300 ms** (configurável).
2. Nenhuma outra tecla foi pressionada entre o down e o up.
3. Nenhum botão do mouse foi pressionado entre o down e o up (evita disparar em Shift + clique de seleção múltipla).
4. O `KeyDown` não é auto-repeat (Shift segurado).
5. A janela em primeiro plano é o Explorer (`CabinetWClass`). Área de trabalho (`Progman`/`WorkerW`) fica para a fase 7.
6. O elemento com foco **não** é campo de texto (UI Automation `ControlType.Edit`): cobre renomear (F2), barra de endereço e caixa de busca.
7. Há pelo menos 1 item selecionado.
8. Passou o cooldown de **500 ms** desde o último disparo.

### 3.2 Modos alternativos (config `trigger.mode`)

- `doubletap`: dois toques isolados em até 400 ms.
- `hold`: Shift segurado sozinho por 400 ms; abre ao atingir o tempo.

### 3.3 Regras do hook — obrigatórias

- `WH_KEYBOARD_LL` + `WH_MOUSE_LL` via `SetWindowsHookEx`, instalados na thread de UI (WPF já tem message loop).
- O callback precisa retornar em microssegundos. O Windows remove o hook silenciosamente se passar de `LowLevelHooksTimeout` (~300 ms a 1 s). Nada de COM, I/O ou log dentro do callback: só atualizar o estado do detector e, se disparar, `Dispatcher.BeginInvoke`.
- O hook **nunca** armazena nem registra qual tecla foi pressionada. O detector só guarda: "Shift está down desde T" e "houve outra tecla/clique: sim/não".
- Sempre chamar `CallNextHookEx`; o app nunca engole a tecla Shift.
- Watchdog: a cada 60 s, verificar se o hook continua ativo (contador de eventos ou reinstalação preventiva) e reinstalar se necessário, registrando em log.

## 4. Leitura da seleção do Explorer

- `Shell.Application` → `Windows()` → para cada janela, comparar `HWND` com o root do foreground (`GetAncestor(GA_ROOT)`).
- `Document.SelectedItems()` → `Path` de cada item.
- **Abas do Windows 11:** várias abas compartilham o mesmo HWND, e `ShellWindows` devolve uma entrada por aba. Para identificar a aba ativa, comparar com o `ShellTabWindowClass` visível (filho do HWND) ou via `IShellBrowser`. É ponto de risco: testar cedo (fase 2).
- Itens que não são arquivos de sistema (Lixeira, "Este Computador", bibliotecas virtuais, arquivos dentro de ZIP) devem ser descartados. Se sobrar zero, não abrir o anel.
- A chamada COM roda fora do callback do hook e com timeout de 1 s. Se estourar, não abrir o anel e registrar em log.

## 5. Janela do anel (RadialWindow)

### 5.1 Janela

- `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`.
- App **PerMonitorV2 DPI aware** (manifest). Posicionar em pixels físicos convertidos para DIP do monitor onde está o cursor.
- Centro do anel = posição do cursor no momento do disparo. Se o anel ultrapassar a área de trabalho do monitor, deslocar o centro para caber.
- Ativar a janela ao abrir (`Activate()`) para receber Esc e mouse; fechar em `Deactivated`.

### 5.2 Geometria (em DIP)

| Elemento | Valor |
|---|---|
| Furo central (raio) | 56 |
| Anel de categorias | 56 a 110 |
| Arco de ações | 114 a 166 |
| Máximo de categorias | 8 (setores iguais, primeiro centrado no topo) |
| Ações por categoria | até 5, arco de 36° cada, centrado no ângulo da categoria |
| Animação | 150 ms, escala 0,6 → 1 + opacidade |

O furo central mostra a contagem de itens selecionados quando houver mais de 1 (ex.: "3 arquivos").

### 5.3 Interação

- Hover em categoria: destaca e abre o arco de ações dela.
- Clique em ação: fecha o anel e executa.
- Esc, clique fora ou novo toque no Shift: fecha sem executar.
- Teclado (fase 7): setas giram entre setores e Enter executa.
- Tema claro/escuro seguindo o Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`).

### 5.4 Filtro de categorias e ações

- Cada ação declara as extensões aceitas e os limites `min_itens` e `max_itens`.
- Com seleção mista (ex.: .docx + .xlsx), uma ação aparece só se aceitar **todas** as extensões selecionadas.
- Categoria sem nenhuma ação aplicável não aparece.
- Ações com `min_itens: 2` (ex.: Juntar PDFs) só aparecem com 2 ou mais itens.

## 6. Catálogo de ações (`config/acoes.json`)

```json
{
  "trigger": { "mode": "tap", "tap_max_ms": 300, "cooldown_ms": 500 },
  "categorias": [
    {
      "id": "pdf",
      "rotulo": "PDF",
      "icone": "file-pdf",
      "acoes": [
        {
          "id": "converter_pdf",
          "rotulo": "Converter",
          "extensoes": [".docx", ".doc", ".xlsx", ".xls", ".pptx", ".jpg", ".jpeg", ".png"],
          "min_itens": 1,
          "max_itens": 50,
          "executor": "ferramentas",
          "timeout_s": 120
        },
        {
          "id": "juntar_pdf",
          "rotulo": "Juntar",
          "extensoes": [".pdf"],
          "min_itens": 2,
          "max_itens": 100,
          "executor": "ferramentas",
          "timeout_s": 120
        },
        {
          "id": "proteger_pdf",
          "rotulo": "Senha",
          "extensoes": [".pdf"],
          "min_itens": 1,
          "max_itens": 50,
          "executor": "ferramentas",
          "pede_senha": true,
          "timeout_s": 60
        }
      ]
    },
    {
      "id": "fiscal",
      "rotulo": "Fiscal",
      "icone": "receipt",
      "acoes": [
        {
          "id": "nfe_excel",
          "rotulo": "NF-e → Excel",
          "extensoes": [".xml"],
          "min_itens": 1,
          "max_itens": 5000,
          "executor": "ferramentas",
          "timeout_s": 300
        }
      ]
    },
    {
      "id": "caminho",
      "rotulo": "Caminho",
      "icone": "copy",
      "acoes": [
        {
          "id": "copiar_caminho",
          "rotulo": "Copiar",
          "extensoes": ["*"],
          "min_itens": 1,
          "max_itens": 1000,
          "executor": "interno"
        }
      ]
    }
  ]
}
```

Campos adicionais previstos:

- `externo: true` → a ação envia dados para fora da máquina (API, serviço online). **Bloqueada por padrão**; exige `permitir_externo: true` no config e confirmação a cada execução (ver seção 9).
- `executor: "interno"` → implementada no C# (ex.: copiar caminho), sem processo filho.

## 7. Contrato do executor (`RadialShift.Ferramentas.exe`)

### 7.1 Chamada

```
RadialShift.Ferramentas.exe --entrada "C:\Users\...\AppData\Local\RadialShift\tmp\<guid>.json"
```

A lista de arquivos vai num JSON temporário, nunca na linha de comando: evita o limite de ~32 mil caracteres e não expõe caminhos na lista de processos. O app apaga o arquivo temporário ao fim da execução.

Entrada:

```json
{
  "acao": "converter_pdf",
  "arquivos": ["C:\\Pasta\\relatorio.docx"],
  "parametros": {}
}
```

**Segredos (ex.: senha do PDF) vão por stdin**, nunca no JSON temporário nem em argumento. O C# escreve a senha no stdin do processo e fecha o stream.

### 7.2 Saída

Uma única linha JSON no stdout:

```json
{
  "ok": true,
  "saidas": ["C:\\Pasta\\relatorio.pdf"],
  "mensagem": "1 arquivo convertido",
  "erros": []
}
```

Códigos de saída:

| Código | Significado |
|---|---|
| 0 | Sucesso (total ou parcial; detalhes em `erros`) |
| 1 | Erro de negócio tratado (arquivo inválido, NF-e fora do layout etc.) |
| 2 | Erro inesperado |
| 3 | Timeout interno ou cancelamento |

### 7.3 Regras gerais das ações

- Saída na mesma pasta do original. Nunca sobrescrever: se existir, usar sufixo `_1`, `_2`…
- Gravar em arquivo temporário na mesma pasta e renomear ao final (escrita atômica), para não deixar arquivo pela metade.
- Caminhos com acento, espaços, UNC (`\\servidor\...`) e longos (> 260 caracteres) precisam funcionar.
- Arquivos do OneDrive/SharePoint em modo "sob demanda" serão baixados ao serem lidos. Aceitável, mas registrar em log o tempo de leitura e tratar falha de download como erro de negócio.

### 7.4 Ações do MVP

**converter_pdf**
- Word, Excel e PowerPoint: automação COM do Office (late binding, sem PIA), `Visible=False`, `DisplayAlerts=False`, sempre fechando documento e aplicação em `finally`. Se o Office não estiver instalado, tentar LibreOffice (`soffice --headless --convert-to pdf`) se encontrado; senão, erro de negócio com mensagem clara.
- Timeout por arquivo. Se estourar, matar o processo `WINWORD.EXE`/`EXCEL.EXE`/`POWERPNT.EXE` **iniciado por esta execução** (guardar o PID); nunca matar instâncias que o usuário já tinha abertas.
- Excel: exportar todas as abas visíveis, respeitando a área de impressão.
- Imagens: PDFsharp, uma imagem por página. Com várias imagens selecionadas, gerar um PDF por imagem (juntar é outra ação).

**juntar_pdf**
- PDFsharp. Ordem = ordem alfabética natural dos nomes (`arquivo2` antes de `arquivo10`).
- Saída: `juntado_AAAA-MM-DD_HHMM.pdf` na pasta do primeiro arquivo.
- PDF protegido por senha na seleção: erro de negócio para aquele arquivo; não pedir senha no MVP.

**proteger_pdf**
- PDFsharp com AES-256 (confirmar na fase 5 o suporte da versão fixada; se só houver AES-128, perguntar antes de seguir). Senha recebida por stdin; o C# pede a senha num diálogo com confirmação (digitar duas vezes) e mínimo de 8 caracteres.
- Saída: `<nome>_protegido.pdf`. Não apagar o original.
- A senha nunca vai para log, nem mascarada.

**nfe_excel**
- `System.Xml.Linq`, namespace `http://www.portalfiscal.inf.br/nfe`, layout 4.00. Aceitar `nfeProc` (com protocolo) e `NFe` (sem protocolo); marcar na planilha se tem protocolo de autorização.
- Ignorar XMLs que não são NF-e (eventos, CT-e, NFS-e) e listá-los numa aba "Ignorados" com o motivo.
- Saída ClosedXML, com abas:
  - `Notas`: chave, número, série, modelo, data de emissão, emitente (CNPJ, razão social, UF), destinatário (CNPJ/CPF, nome, UF), natureza da operação, valor dos produtos, frete, desconto, ICMS, IPI, valor total, possui protocolo, arquivo de origem.
  - `Itens`: chave, nº item, código, descrição, NCM, CFOP, unidade, quantidade, valor unitário, valor total, ICMS, IPI.
  - `Ignorados`: arquivo e motivo.
- Valores numéricos como número (não texto), datas como data. Chave como texto (44 dígitos estouram a precisão do Excel se virarem número).
- Validar o dígito verificador da chave (módulo 11) e sinalizar inválidas numa coluna.
- Saída: com 1 XML, `<nome>.xlsx`; com vários, `nfe_consolidado_AAAA-MM-DD_HHMM.xlsx`.
- **LGPD:** destinatário pessoa física (CPF) é dado pessoal. Coluna de CPF fica, mas a planilha gerada herda o cuidado do XML original; registrar isso no README de uso.

**copiar_caminho** (interno, C#)
- Copia os caminhos completos, um por linha, para a área de transferência.

## 8. Execução, fila e notificações

- `ActionRunner` executa no máximo 2 ações em paralelo; as demais entram em fila.
- Processo filho com `CreateNoWindow=true`, `UseShellExecute=false`, stdout e stderr redirecionados.
- Timeout definido pela ação. Ao estourar, matar a árvore de processos (`Process.Kill(entireProcessTree: true)`).
- Toast nativo (`Microsoft.Toolkit.Uwp.Notifications` / `ToastNotificationManagerCompat`, que funciona em app não empacotado):
  - Início, só se a ação passar de 2 s: "Convertendo 3 arquivos…"
  - Fim com sucesso: "Pronto: relatorio.pdf", com botão "Abrir pasta" que seleciona o arquivo (`explorer.exe /select,`).
  - Erro: mensagem curta + "Ver log".
- Ícone na bandeja com menu: Pausar gatilho, Abrir log, Abrir configuração, Recarregar configuração, Sair.

## 9. Segurança e LGPD

- **Sem admin.** Tudo em HKCU e `%LOCALAPPDATA%\RadialShift`. Nada em HKLM nem em Program Files.
- **Início com o Windows:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, configurável na bandeja e desligado por padrão.
- **Hook:** nunca registrar teclas. Code review obrigatório em qualquer alteração em `KeyboardHook`/`TriggerDetector`.
- **Sem rede no MVP.** Nenhuma ação faz requisição externa. Ações `externo: true` ficam bloqueadas por padrão e, quando liberadas, pedem confirmação mostrando o que vai sair da máquina.
- **Logs** (Serilog, arquivo rotativo diário, 14 dias, `%LOCALAPPDATA%\RadialShift\logs`):
  - Nível INFO: ação, quantidade de arquivos, extensões, duração, resultado, tipo de erro.
  - **Nomes e caminhos de arquivos só em DEBUG**, desligado por padrão: nomes de arquivo podem conter nome de paciente ou de cliente.
  - Nunca: conteúdo de arquivo, senha, dados de NF-e.
- **Distribuição para clientes:** assinar `RadialShift.exe` e `RadialShift.Ferramentas.exe` com certificado de code signing. Publicar com `dotnet publish -r win-x64 --self-contained` numa pasta (sem single-file comprimido, que gera mais falso positivo); a máquina de destino não precisa de .NET nem Python.
- Dependências .NET com versões fixadas no `.csproj`.

## 10. Estrutura do repositório

```
radialshift/
├── CLAUDE.md
├── README.md
├── docs/
│   ├── SPEC.md
│   └── TESTES_MANUAIS.md
├── config/
│   └── acoes.json
├── src/
│   ├── RadialShift.App/                (WPF, .NET 8)
│   │   ├── App.xaml / App.xaml.cs      (single instance via Mutex, bandeja)
│   │   ├── app.manifest                (PerMonitorV2)
│   │   ├── Input/
│   │   │   ├── KeyboardHook.cs
│   │   │   ├── MouseHook.cs
│   │   │   └── TriggerDetector.cs
│   │   ├── Shell/
│   │   │   ├── ExplorerSelection.cs
│   │   │   └── FocusInspector.cs       (UI Automation: foco em Edit?)
│   │   ├── Radial/
│   │   │   ├── RadialWindow.xaml(.cs)
│   │   │   └── RadialGeometry.cs       (cálculo de setores; testável sem UI)
│   │   ├── Actions/
│   │   │   ├── ActionCatalog.cs        (lê e valida acoes.json)
│   │   │   ├── ActionFilter.cs         (extensões, min/max; testável)
│   │   │   ├── ActionRunner.cs
│   │   │   └── Internal/CopyPathAction.cs
│   │   ├── Notifications/Toasts.cs
│   │   └── Infra/ (Logging.cs, Paths.cs, Settings.cs)
│   ├── RadialShift.Ferramentas/        (console, .NET 8)
│   │   ├── Program.cs                  (CLI: lê JSON, despacha, imprime JSON)
│   │   ├── Contract.cs                 (records de entrada/saída, códigos de saída)
│   │   ├── OutputNames.cs              (sufixo _1, escrita atômica)
│   │   └── Actions/
│   │       ├── ConvertToPdf.cs
│   │       ├── MergePdf.cs
│   │       ├── ProtectPdf.cs
│   │       └── NfeToExcel.cs
│   ├── RadialShift.Tests/              (xUnit: TriggerDetector, ActionFilter, RadialGeometry)
│   └── RadialShift.Ferramentas.Tests/  (xUnit, com XMLs de NF-e fictícios)
└── publicar.ps1                        (dotnet publish self-contained dos dois exes)
```

O `TriggerDetector` deve receber eventos abstratos (`KeyDown(isShift, isRepeat, timestamp)`, `MouseDown`, `KeyUp`) para ser testável sem hook real.

## 11. Fases e critérios de aceite

| Fase | Entrega | Pronto quando |
|---|---|---|
| 1 | App na bandeja + hook + `TriggerDetector` | Toque no Shift com Explorer em foco grava "disparo" no log; Shift + clique, Shift + setas, digitação de maiúsculas e F2 **não** disparam. Testes unitários do detector passando. |
| 2 | `ExplorerSelection` | Log lista a quantidade e as extensões da seleção correta, inclusive com 2+ abas abertas no Windows 11. |
| 3 | `RadialWindow` com catálogo fixo | Anel abre no cursor, respeita borda do monitor e DPI 100%/125%/150%, fecha com Esc e clique fora. |
| 4 | `ActionRunner` + `RadialShift.Ferramentas.exe` + `copiar_caminho` + `juntar_pdf` | Contrato JSON funcionando ponta a ponta; timeout mata o processo filho. |
| 5 | `converter_pdf`, `proteger_pdf`, `nfe_excel` + toasts | Testes xUnit das ferramentas passando; arquivos gerados abrem sem erro; nada sobrescrito. |
| 6 | `acoes.json` dinâmico + filtro por extensão + início com Windows + empacotamento | Alterar o JSON e recarregar pela bandeja muda o anel sem recompilar. |
| 7 | Extras | Área de trabalho, modos `doubletap`/`hold`, navegação por teclado. |

## 12. Testes manuais (manter em `docs/TESTES_MANUAIS.md`)

- Shift sozinho com 1 arquivo, com 5 arquivos, com nenhum selecionado.
- Shift + clique, Shift + setas, Ctrl + Shift + Esc: não podem abrir o anel.
- F2 (renomear) + digitar maiúscula; caixa de busca do Explorer; barra de endereço.
- Explorer com 3 abas, seleção na aba do meio.
- 2 monitores com escalas diferentes; anel perto da borda e do canto.
- Arquivo em pasta do OneDrive "sob demanda"; arquivo em compartilhamento de rede UNC.
- Nome com acento e espaço; caminho maior que 260 caracteres.
- Word já aberto pelo usuário durante `converter_pdf`: a instância dele não pode fechar.
- Lote de 1.000 XMLs de NF-e: tempo e uso de memória.
- Antivírus (Defender) com o build empacotado: não pode ser bloqueado.

## 13. Decisões em aberto (premissas assumidas)

1. **Nome do produto:** "RadialShift" provisório.
2. **Distribuição:** MVP só na sua máquina. Distribuir para clientes exige certificado de code signing (custo anual) — decidir antes da fase 6.
3. **Conversão Office:** premissa de que há Microsoft Office instalado; LibreOffice como fallback.
4. **Ordem do Juntar PDFs:** alfabética natural. Alternativa: ordem de seleção (o Explorer não garante essa ordem via COM).
5. **Categorias iniciais:** PDF, Fiscal e Caminho. LGPD (anonimizar), Imagem e Dados entram depois do MVP.
