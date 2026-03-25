# 📊 excelst

> Générez des fichiers Excel à partir d'un fichier source texte — comme [Typst](https://typst.app/) le fait pour les PDFs.

[![CI](https://github.com/ymauray/excelst/actions/workflows/ci.yaml/badge.svg)](https://github.com/ymauray/excelst/actions/workflows/ci.yaml)
[![Release](https://img.shields.io/github/v/release/ymauray/excelst)](https://github.com/ymauray/excelst/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-linux%20%7C%20macOS%20%7C%20windows-blue)](#-installation)

Le fichier `.exl` est la **source de vérité**, versionnable et lisible par n'importe quel éditeur de texte. `excelst compile` produit le `.xlsx` correspondant.

---

## ✨ Fonctionnalités

- 📄 **Feuilles multiples** avec contrôle de l'ordre (`after:`)
- 🔢 **Types** : chaînes, entiers, décimaux
- 🔤 **Variables** immutables (`let`) et mutables (`var`)
- 🔁 **Boucles** `while` avec adresses de cellules dynamiques
- ➕ **Expressions** arithmétiques (`+` `-` `*` `/`) et comparaisons (`<` `>` `==` …)
- 🧵 **Concaténation** de chaînes (`"A" + i`)
- 📦 **Tableaux** avec accès indexé (`arr.at(0)`)
- 🚀 **Binaire autonome** — aucune dépendance, aucun runtime à installer

---

## 📦 Installation

### 🍺 Homebrew (macOS et Linux)

```bash
brew install ymauray/tap/excelst
```

### 🪣 Scoop (Windows)

```powershell
scoop bucket add ymauray https://github.com/ymauray/scoop-bucket
scoop install excelst
```

### 🐧 Linux — paquet natif

Télécharge le `.deb` ou `.rpm` depuis les [releases](https://github.com/ymauray/excelst/releases/latest) :

```bash
# Debian / Ubuntu
sudo dpkg -i excelst-linux-x64.deb

# Fedora / RHEL
sudo rpm -i excelst-linux-x64.rpm
```

### 📁 Archive (toutes plateformes)

Télécharge l'archive depuis les [GitHub Releases](https://github.com/ymauray/excelst/releases/latest), extrais et place le binaire dans ton `PATH`.

---

## 🚀 Utilisation

```bash
excelst compile mon-fichier.exl
# → génère mon-fichier.xlsx
```

---

## 📝 Syntaxe `.exl`

```
// ── Variables ─────────────────────────────────────────────────────────────────
let titre = "Rapport"        // immutable
var i     = 1                // mutable

// ── Tableaux ──────────────────────────────────────────────────────────────────
let mois = ("Jan", "Fév", "Mar")
cell("A1", mois.at(0))       // → "Jan"

// ── Feuilles ──────────────────────────────────────────────────────────────────
sheets.add("Données")
sheets.add("Résumé", after: "Données")

sheet("Données", {
    cell("A1", titre)

    // Boucle avec adresse dynamique
    while i <= 12 {
        cell("A" + (i + 1), i * 100)
        i = i + 1
    }
})

sheet("Résumé", {})
```

> [!TIP]
> `(x)` est une **expression parenthésée** (équivaut à `x`), pas un tableau.
> Pour un tableau à un élément, la virgule est obligatoire : `(x,)`.

> [!NOTE]
> `sheets.add()` doit **précéder** tout `sheet()` qui référence la même feuille.
> `let` est immutable — utilise `var` pour une variable réassignable.

---

## 🗺️ Feuille de route

- [ ] Boucles `for`
- [ ] Styles (gras, couleurs, bordures…)
- [ ] Formules Excel

---

## 🛠️ Développement

```bash
# Build
dotnet build excelst.slnx

# Tests
dotnet test excelst.slnx

# Compiler un fichier de démo
dotnet run --project src/excelst -- compile demo/demo.exl
```

---

## 📄 Licence

MIT — voir [LICENSE](LICENSE).
