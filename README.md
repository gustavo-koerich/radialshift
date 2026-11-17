# RadialShift

[![testes](https://github.com/gustavo-koerich/radialshift/actions/workflows/testes.yml/badge.svg)](https://github.com/gustavo-koerich/radialshift/actions/workflows/testes.yml)

Menu radial para o Explorer do Windows: selecione arquivos, dê um toque no **Shift** e um anel de ações abre em volta do cursor. Hoje funcionam **Juntar PDFs** e **Copiar caminho**; converter para PDF, proteger com senha e NF-e → Excel estão no roteiro. As ações rodam em segundo plano e o resultado é salvo na mesma pasta do arquivo original.

## Destaques técnicos

- **Hook de teclado global** (`WH_KEYBOARD_LL`) que só detecta "toque isolado no Shift": nunca armazena, registra ou transmite teclas, sempre repassa o evento (`CallNextHookEx`) e não faz I/O no callback
- **Leitura da seleção do Explorer** via COM (`Shell.Application`) com *fallback* em UI Automation
- **Anel em WPF** com geometria própria (setores, *hit-testing* em coordenadas polares) e filtro de ações por extensão e quantidade de arquivos
- **Executor isolado**: as ações rodam num processo filho (`RadialShift.Ferramentas.exe`) com contrato JSON de entrada/saída, *timeout* e fila — uma falha nunca derruba o app residente
- **Catálogo declarativo** em [`config/acoes.json`](config/acoes.json): novas ações sem recompilar a interface
- **Segurança**: senhas só via stdin, nada em `HKLM` nem exige admin (só `HKCU` e `%LOCALAPPDATA%`), logs sem conteúdo de arquivos
- **Testes unitários** (xUnit) para detecção do gatilho, filtro de ações, geometria do anel e ações de PDF

## Arquitetura

```
RadialShift.App (WPF, bandeja)
  KeyboardHook → TriggerDetector → ExplorerSelection → RadialWindow ← ActionCatalog
                                                            │
                                                     ActionRunner ──► RadialShift.Ferramentas.exe
                                                            │          (JSON in → executa → JSON out)
                                                     notificação na bandeja
```

Especificação completa em [docs/SPEC.md](docs/SPEC.md) e roteiro de testes manuais em [docs/TESTES_MANUAIS.md](docs/TESTES_MANUAIS.md).

## Stack

C# · .NET 8 · WPF · PDFsharp · Serilog · xUnit

## Rodando

Requer Windows 10/11 e .NET 8 SDK.

```powershell
dotnet test src/RadialShift.Tests
dotnet test src/RadialShift.Ferramentas.Tests
dotnet run --project src/RadialShift.App
```

Ou pelo atalho: `.\executar.ps1` (`-Testes` roda os testes, `-Diagnostico` liga o log detalhado).

## Estrutura

```
src/
  RadialShift.App/               # app residente: hook, seleção, anel, fila
  RadialShift.Ferramentas/       # executor das ações (console)
  RadialShift.Tests/             # testes do app
  RadialShift.Ferramentas.Tests/ # testes das ações
config/acoes.json                # catálogo de categorias e ações
docs/                            # especificação e testes manuais
```

## Licença

MIT
