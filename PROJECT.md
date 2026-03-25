# PROJECT.md

## But du projet

`excelst` est un outil en ligne de commande (CLI) écrit en .NET 10 qui permet de **décrire et générer des fichiers Excel** à partir d'un fichier source texte (`.exl`), de la même façon que [Typst](https://typst.app/) génère des fichiers PDF à partir d'un fichier source `.typ`.

L'idée centrale : le fichier `.exl` est la source de vérité, versionnable et lisible, et `excelst compile` produit le `.xlsx` correspondant.

## État de l'implémentation

### Fait

- [x] Solution `.slnx` et projet CLI `src/excelst`
- [x] Commande `excelst compile <fichier.exl>` — génère un fichier `.xlsx`
- [x] Lexer (`src/excelst/Compiler/Lexer.cs`) — texte → `List<Token>`
- [x] Parser récursif descendant (`src/excelst/Compiler/Parser.cs`) — tokens → AST
- [x] Générateur Excel (`src/excelst/Compiler/ExcelGenerator.cs`) — AST → `.xlsx` via ClosedXML
- [x] Blocs `sheet("nom", { })` — crée une feuille avec son contenu
- [x] Paramètre `after: "autre"` — contrôle l'ordre des feuilles
- [x] Instruction `cell(adresse, valeur)` — écrit une valeur dans une cellule
- [x] Types de valeurs : chaîne (`"texte"`), entier (`42`), décimal (`3.14`)
- [x] Variables immutables — `let x = expr` (portée lexicale, résolution eager)
- [x] Variables mutables — `var x = expr` (réassignable via `x = expr`)
- [x] Tableaux — littéraux `(1, "a", 3.14)`, tableau vide `()`, un élément `(x,)`, accès `arr.at(0)`
- [x] Boucles `while condition { body }` — garde anti-boucle infinie (100 000 itérations)
- [x] Expressions arithmétiques — `+`, `-`, `*`, `/` avec précédence correcte
- [x] Expressions de comparaison — `<`, `>`, `<=`, `>=`, `==`, `!=`
- [x] Négation unaire — `-expr`
- [x] Concaténation de chaînes — `"texte" + variable`
- [x] Tests unitaires — 77 tests dans `tests/excelst.Tests`
- [x] Binaire autonome — `dotnet publish` produit un exe single-file auto-détectant la plateforme

### Syntaxe `.exl`

```
// Commentaires sur une ligne
// Chaînes entre guillemets doubles

// ── Variables ─────────────────────────────────────────────────────────────────
let titre = "Poste"          // immutable — erreur si re-déclarée
var n = 0                    // mutable
n = n + 1                    // réassignation (var uniquement)

// ── Tableaux ──────────────────────────────────────────────────────────────────
let arr  = (1, "a", 3.14)   // tableau de types mixtes
let seul = (42,)             // un élément (virgule obligatoire)
let vide = ()                // tableau vide
// (x) est une expression parenthésée (équivaut à x), pas un tableau

// ── Expressions ───────────────────────────────────────────────────────────────
let total = 3 + 4 * 2        // précédence classique : 11
let addr  = "A" + n          // concaténation string + entier → "A1"
let val   = arr.at(0)        // accès tableau, zéro-based

// ── Feuilles ──────────────────────────────────────────────────────────────────
sheets.add("Données")
sheets.add("Résumé", after: "Données")   // after: contrôle l'ordre

// sheets.add() doit précéder tout sheet() qui référence la même feuille

sheet("Données", {
    cell("A1", titre)            // valeur littérale ou variable
    cell("B1", arr.at(0))        // expression quelconque
    cell("C1", 3 + 4 * 2)

    var i = 1
    while i <= 10 {
        cell("A" + (i + 1), i)   // adresse dynamique
        cell("B" + (i + 1), i * i)
        i = i + 1
    }
})

sheet("Résumé", {})
```

### À faire

- [ ] Boucles `for`
- [ ] Styles (gras, couleurs, bordures…)
- [ ] Formules Excel

## Décisions techniques

- **Langage** : C# / .NET 10
- **CLI** : `System.CommandLine` 2.0.5
- **Génération Excel** : `ClosedXML` 0.105.0 (MIT, cible `netstandard2.0`, compatible .NET 10)
- **Format de sortie** : `.xlsx` (OOXML)
- **Pipeline compilateur** : Lexer → Parser (AST) → ExcelGenerator — chaque étape a ses propres exceptions (`LexerException`, `ParseException`, `GeneratorException`)
- **Séparation AST/runtime** : `Expression` = nœuds AST (sortie du parser) ; `Value` = valeurs runtime (sortie du générateur)
- **Portée** : classe `Scope` chaînée — `let` immutable, `var` mutable, résolution remontante vers le parent
