# Testes manuais

Log: `%LOCALAPPDATA%\RadialShift\logs\radialshift-AAAAMMDD.log` (bandeja → **Abrir log**).

## Fase 1 — gatilho

Pré-requisito: `dotnet run --project src/RadialShift.App`; ícone aparece na bandeja.

| # | Ação | Esperado no log |
|---|---|---|
| 1 | Explorer em foco, arquivo selecionado, toque rápido no Shift esquerdo | `Disparo` |
| 2 | Idem com Shift direito | `Disparo` |
| 3 | Dois toques seguidos em menos de 0,5 s | só 1 `Disparo` (cooldown) |
| 4 | Shift + clique para selecionar vários | nada |
| 5 | Shift + setas | nada |
| 6 | Segurar Shift por ~1 s e soltar | nada |
| 7 | Digitar maiúscula (ex.: barra de busca do Explorer) | nada |
| 8 | F2 (renomear) e tocar Shift sozinho dentro do campo | `Toque ignorado: foco em campo de texto` |
| 9 | Clicar na barra de endereço / caixa de busca e tocar Shift | `Toque ignorado: foco em campo de texto` |
| 10 | Ctrl + Shift + Esc | nada |
| 11 | Ctrl (ou Alt) segurado + toque no Shift | nada |
| 12 | Toque no Shift com Bloco de Notas/navegador em foco | nada (só em DEBUG) |
| 13 | Bandeja → Pausar gatilho, tocar Shift no Explorer | `Gatilho pausado` e nenhum `Disparo` |
| 14 | Abrir o app duas vezes | aviso "já está em execução" |
| 15 | Conferir o log inteiro | nenhuma tecla, nome ou caminho de arquivo registrado |

Fase 1 ainda não exige seleção (regra 3.1.7 entra na fase 2): toque com nada selecionado também gera `Disparo`.

## Fase 2 — seleção do Explorer

Rodar com `.\executar.ps1 -Diagnostico` para ver os caminhos em DEBUG (só nesta máquina).

| # | Ação (numa pasta do Explorer) | Esperado no log |
|---|---|---|
| 1 | 1 arquivo selecionado, toque no Shift | `Disparo: 1 item(ns)...; extensões: .pdf x1` (ou a extensão certa) |
| 2 | 5 arquivos de tipos variados | quantidade 5 e extensões corretas |
| 3 | Pasta selecionada | `(pasta) x1` |
| 4 | Nada selecionado (clicar no vazio) | `Toque ignorado: nenhum arquivo selecionado` |
| 5 | 3 abas abertas, seleção diferente em cada; tocar na aba do meio | quantidade/extensões da aba do meio |
| 6 | Trocar de aba e repetir | segue a aba ativa |
| 7 | "Este Computador" ou "Início" | `nenhum arquivo selecionado` |
| 8 | Lixeira com item selecionado | `nenhum arquivo selecionado` |
| 9 | Arquivo dentro de um .zip aberto no Explorer | `nenhum arquivo selecionado` |
| 10 | Sem `-Diagnostico` | log INFO sem nenhum nome ou caminho |

## Fase 3 — anel

Catálogo fixo: PDF, Fiscal e Caminho aparecem sempre (filtro por extensão é fase 6). Clicar numa ação só grava `Ação escolhida` no log (execução é fase 4).

| # | Ação | Esperado |
|---|---|---|
| 1 | Toque no Shift com arquivo selecionado | anel abre centrado no cursor, com animação curta |
| 2 | 3+ arquivos selecionados | furo central mostra "3 arquivos" |
| 3 | Passar o mouse em "PDF" | categoria destaca e abre arco com Converter, Juntar, Senha |
| 4 | Ir da categoria até uma ação | arco continua aberto; ação destaca |
| 5 | Clicar numa ação | anel fecha; log `Ação escolhida: pdf/juntar_pdf...` |
| 6 | Esc | fecha sem log de ação |
| 7 | Clique fora do anel | fecha |
| 8 | Novo toque no Shift com anel aberto | fecha |
| 9 | Cursor perto da borda / do canto da tela | anel desloca para caber inteiro |
| 10 | Monitor a 100%, 125%, 150% (Configurações → Tela → Escala) | mesmo tamanho visual relativo, nítido, no cursor |
| 11 | Dois monitores com escalas diferentes | abre no monitor do cursor, no lugar certo |
| 12 | Tema claro e escuro do Windows | cores seguem o tema (reabrir o anel após trocar) |

## Fase 4 — ações (copiar caminho, juntar PDFs)

Resultado aparece como balão da bandeja (provisório; toasts na fase 5) e no log. O timeout que mata o processo filho é coberto por teste automático (`ActionRunnerTests.Timeout_MataOProcessoFilho`).

| # | Ação | Esperado |
|---|---|---|
| 1 | 1 arquivo → Caminho → Copiar; colar no Bloco de Notas | caminho completo |
| 2 | 3 arquivos → Copiar | 3 caminhos, um por linha |
| 3 | 3 PDFs (`a2.pdf`, `a10.pdf`, `a1.pdf`) → PDF → Juntar | `juntado_AAAA-MM-DD_HHMM.pdf` na pasta, páginas na ordem a1, a2, a10 |
| 4 | Repetir o 3 no mesmo minuto | `juntado_..._1.pdf`; o anterior intacto |
| 5 | Juntar com um PDF protegido por senha no meio | junta os outros; balão avisa "1 item(ns) com problema: PDF protegido por senha" |
| 6 | Juntar com só 1 PDF | erro "Selecione pelo menos 2 PDFs para juntar"; nada criado |
| 7 | Converter / Senha / NF-e → Excel | erro "ainda não está disponível" (fase 5) |
| 8 | Pasta com acento e espaço, e pasta de rede (UNC) | funciona igual |
| 9 | Log sem `-Diagnostico` | ação, quantidade, extensões, duração, resultado; nenhum nome ou caminho |
| 10 | `%LOCALAPPDATA%\RadialShift\tmp` após as ações | vazia |

## Roteiro completo (fases seguintes)

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
