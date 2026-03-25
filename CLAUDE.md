# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Run & Test

```bash
# Build
dotnet build excelst.slnx

# Run (development)
dotnet run --project src/excelst -- compile fichier.exl

# Compile a specific file
dotnet run --project src/excelst -- compile demo/demo.exl

# Run all tests
dotnet test excelst.slnx

# Run a single test (by name filter)
dotnet test excelst.slnx --filter "FullyQualifiedName~SheetsAdd_avec_after"

# Publish a self-contained single-file binary for the current platform
dotnet publish src/excelst/excelst.csproj -c Release

# Cross-compile for another platform
dotnet publish src/excelst/excelst.csproj -c Release -r linux-x64
dotnet publish src/excelst/excelst.csproj -c Release -r osx-arm64
```

> Note: .NET 10 generates `excelst.slnx` (new solution format), not `excelst.sln`.

## CI / GitHub

- **CI** (`.github/workflows/ci.yaml`) — lance `dotnet test` sur ubuntu à chaque push sur `main` et chaque PR. Le check s'appelle `Tests`.
- **Release** (`.github/workflows/release.yaml`) — déclenché par un tag `vX.Y.Z` (ou manuellement via `workflow_dispatch` avec un tag en input pour tester) :
  1. Compile et archive pour linux-x64, win-x64, osx-arm64
  2. Produit `.deb` et `.rpm` via `nfpm` (job Linux)
  3. Crée la GitHub Release avec tous les assets
  4. Met à jour `Formula/excelst.rb` dans `ymauray/homebrew-tap` (secret `HOMEBREW_TAP_TOKEN`)
  5. Met à jour `excelst.json` dans `ymauray/scoop-bucket` (secret `SCOOP_BUCKET_TOKEN`)
- **Dependabot** (`.github/dependabot.yml`) — surveille les GitHub Actions et les paquets NuGet, mises à jour mensuelles groupées.
- **Branch protection** sur `main` : le merge est bloqué si le check `Tests` échoue (configuré dans Settings → Branches).

## Distribution

| Gestionnaire | Commande d'installation |
|---|---|
| Homebrew (macOS/Linux) | `brew install ymauray/tap/excelst` |
| Scoop (Windows) | `scoop bucket add ymauray https://github.com/ymauray/scoop-bucket` puis `scoop install excelst` |
| Debian/Ubuntu | `sudo dpkg -i excelst-linux-x64.deb` |
| Fedora/RHEL | `sudo rpm -i excelst-linux-x64.rpm` |
| Toutes plateformes | Télécharger l'archive depuis les [GitHub Releases](https://github.com/ymauray/excelst/releases) |

Repos associés : `ymauray/homebrew-tap`, `ymauray/scoop-bucket`.

## Git

Commits follow the **Conventional Commits** specification:
```
feat: add for-loop support
fix: handle empty sheet block correctly
test: add parser tests for array literals
docs: update PROJECT.md with new syntax
chore: bump ClosedXML to 0.106
```

Common types: `feat`, `fix`, `test`, `docs`, `chore`, `refactor`.

## Architecture

This is a CLI tool (`excelst`) that compiles `.exl` source files into `.xlsx` Excel files — inspired by Typst's approach to PDF generation.

**Entry point**: `Program.cs` — wires a `RootCommand` and registers subcommands.

**Command pattern**: Each CLI subcommand lives in `src/excelst/Commands/` as a static class with a `Build()` method returning a `System.CommandLine.Command`. The command declares its arguments/options, wires a `SetAction` handler, and is added to the root command in `Program.cs`.

**Excel generation**: Uses `ClosedXML` (`XLWorkbook`) to produce `.xlsx` files. Output path is always derived from the input path via `Path.ChangeExtension(input, ".xlsx")`.

**Compiler pipeline** (`src/excelst/Compiler/`):
- `Lexer.cs` — text → `List<Token>`
- `Parser.cs` — tokens → AST (`Programme`), recursive descent with operator precedence chain: `ParseComparison` → `ParseAddition` → `ParseMultiplication` → `ParseUnary` → `ParsePrimary`
- `ExcelGenerator.cs` — AST → `XLWorkbook`, with a `Scope` class for variable resolution
- `Token.cs` — `TokenKind` enum + `Token` record
- `Ast.cs` — two hierarchies: `Expression` (AST nodes output by parser) and `Value` (runtime values output by generator); unified `Statement` hierarchy

**Key design points**:
- `let` = immutable, `var` = mutable — enforced by `Scope.Define/Set`
- `while` has a 100,000-iteration guard against infinite loops
- `cell(addr, val)` — `addr` is an `Expression` (allows `"A" + i`), must evaluate to `StringValue`
- Each compiler stage has its own exception type: `LexerException`, `ParseException`, `GeneratorException`

**Key dependency API notes** — `System.CommandLine` 2.0.5 uses a revised API (not the older beta):
- `Command.Add(argument/option/subcommand)` instead of `AddArgument`/`AddCommand`
- `command.SetAction((ParseResult result) => { ... })` instead of `SetHandler`
- `result.GetValue(argument)` to retrieve parsed values
- `rootCommand.Parse(args).InvokeAsync()` as the entry point invocation
