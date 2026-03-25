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
- [x] Blocs `sheet("nom") { }` — crée une feuille avec son contenu
- [x] Paramètre `after: "autre"` — contrôle l'ordre des feuilles
- [x] Instruction `cell("A1", valeur)` — écrit une valeur dans une cellule
- [x] Types de valeurs : chaîne (`"texte"`), entier (`42`), décimal (`3.14`)
- [x] Variables — `let x = valeur` (top-level, portée globale, immutables, résolution eager)
- [x] Tableaux — littéraux `(1, "a", 3.14)`, tableau vide `()`, un élément `(x,)`, accès `arr.at(0)`
- [x] Variables mutables — `var x = expr` (mutable, réassignable via `x = expr`)
- [x] Boucles `while condition { body }` — avec garde anti-boucle infinie (100 000 itérations)
- [x] Expressions arithmétiques — `+`, `-`, `*`, `/` avec précédence correcte
- [x] Expressions de comparaison — `<`, `>`, `<=`, `>=`, `==`, `!=`
- [x] Négation unaire — `-expr`
- [x] Concaténation de chaînes — `"texte" + variable`
- [x] Tests unitaires — 77 tests (lexer, parser, générateur, variables, tableaux, var/while/expressions) dans `tests/excelst.Tests`
- [x] Binaire autonome — `dotnet publish` produit un exe single-file auto-détectant la plateforme

### Syntaxe `.exl` (actuelle)

```
// Variables (portée globale, immutables, doivent précéder leur utilisation)
let x = 42
let titre = "Bonjour"
let arr   = (1, "a", 3.14)   // tableau, résolution eager
let seul  = (42,)             // tableau à un élément (virgule obligatoire)
let vide  = ()

// Déclaration des feuilles (ordre du classeur)
sheets.add("NomFeuille")
sheets.add("AutreFeuille", after: "NomFeuille")

// Définition du contenu
sheet("NomFeuille", {
    cell("A1", titre)          // variable
    cell("B1", arr.at(0))      // accès tableau, zéro-based
    cell("C1", 3.1415927)
})

sheet("AutreFeuille", {
})
```

- `sheets.add()` déclare une feuille et son ordre — doit précéder tout `sheet()` qui la référence
- `sheet("nom", { ... })` définit le contenu d'une feuille déjà déclarée — erreur sinon
- `let` est immutable — re-déclarer une variable est une erreur
- `(x)` est une expression parenthésée (équivaut à `x`), pas un tableau
- Commentaires sur une ligne : `// ...`
- Chaînes entre guillemets doubles
- Types de valeurs : `"texte"`, entier (`42`), décimal (`3.14`)

### Syntaxe `.exl` (actuelle)

```
// Variables immutables (let) et mutables (var)
let titre = "Poste"
var n = 0

// Réassignation d'une variable mutable
n = n + 1

// Boucle while
while n < 10 {
    n = n + 1
}

// Expressions arithmétiques et comparaisons
let total = 3 + 4 * 2        // 11 (précédence correcte)
let addr  = "A" + n          // concaténation

// Tableaux (inchangés)
let arr = (1, "a", 3.14)
let seul = (42,)
let vide = ()

// Feuilles et cellules (inchangés)
sheets.add("Feuille")
sheet("Feuille", {
    cell("A1", titre)
    while n < 5 {
        cell("A" + n, n)
        n = n + 1
    }
})
```

### À faire

- [ ] Boucles (`for`)
- [ ] Styles (gras, couleurs, bordures…)
- [ ] Formules

## Décisions techniques

- **Langage** : C# / .NET 10
- **CLI** : `System.CommandLine` 2.0.5
- **Génération Excel** : `ClosedXML` 0.105.0 (MIT, cible `netstandard2.0`, compatible .NET 10)
- **Format de sortie** : `.xlsx` (OOXML)
