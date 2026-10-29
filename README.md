# Triple Wheel 3D (TW3D)

Překážková jízda s 3D grafikou a 2D fyzikou: auto s otočnými nápravami musí projet
trať s využitím rotace a zamykání náprav. Projekt vznikl v roce 2016 jako příloha
bakalářské práce (ukázková hra + editor tratí) a v roce 2026 byl převeden na
současné nástroje.

Hra je postavená na frameworku [MonoGame](https://monogame.net/) a fyzikálním
enginu Farseer Physics. Fyzika běží ve 2D, 3D modely se vykreslují podle polohy
fyzikálních těles.

## Struktura repozitáře

| Cesta | Obsah |
|---|---|
| `src/TW3D.Game/` | Hra (`TW3D.exe`) |
| `src/TW3D.LevelEditor/` | Editor tratí (`LevelEditor.exe`), jen pro Windows |
| `src/FarseerPhysics/` | Fyzikální engine Farseer Physics 3.5 (převzatý kód třetí strany) |
| `content/` | Sdílený herní obsah: zkompilované modely, textury a fonty (`.xnb`) a tratě (`Levels/*.lev`). Při sestavení se kopíruje do výstupu jako `Content/`. |
| `docs/bakalarka/` | Dokumentace z bakalářské práce (popisuje stav z roku 2016) |
| `docs/katalog/` | Katalog bloků tratí a dynamických objektů. Otevři `katalog.html` v prohlížeči. |

## Požadavky

- [.NET SDK 10](https://dotnet.microsoft.com/download) nebo novější
- Volitelně IDE: Visual Studio 2022 (17.13+) / 2026, JetBrains Rider nebo VS Code s C# Dev Kit

Balíčky (MonoGame 3.8 DesktopGL) se stáhnou automaticky z NuGetu. Jejich verze jsou
centrálně v `Directory.Packages.props`.

## Sestavení a spuštění

```sh
dotnet build TW3D.slnx

# hra
dotnet run --project src/TW3D.Game
dotnet run --project src/TW3D.Game -- --windowed --level lock

# editor tratí
dotnet run --project src/TW3D.LevelEditor
```

Parametry hry pro vývoj:

| Parametr | Význam |
|---|---|
| `--windowed` | Okno 1280×720 místo celé obrazovky |
| `--level <název>` | Přeskočí menu a rovnou načte trať (`lock`, `startblock`, `tunel`). Esc vrátí do menu. |

## Ovládání hry

| Klávesa | Funkce |
|---|---|
| → / ← | Plyn / couvání |
| ↑ / ↓ | Rotace náprav po / proti směru hodinových ručiček |
| Mezerník | Brzda všech kol |
| Levý Ctrl | Zamčení rotace náprav |
| Esc | Zpět do menu |
| F1–F8 | Ladicí zobrazení fyzikální simulace (Farseer DebugView) |

Hra ve výchozím stavu čte klávesnici. Pro ovládání virtuálním joystickem na dotykovém
displeji je třeba v `src/TW3D.Game/ScreenSystem/InputHelper.cs` změnit
`VirtualState = HandleKeyboard();` na `VirtualState = HandleVirtualStick();`.

Ovládání editoru popisuje `docs/bakalarka/dokumentace-editoru-trati.pdf`. Novou trať
ulož do `content/Levels/` a přidej ji do seznamu ve `Game1.Initialize`
(`selLevScreen.AddLevelItem(...)`). Náhledový obrázek v menu se načítá
z `content/Levels/levimgs/<název>.xnb`.

## Herní obsah

Složka `content/` obsahuje pouze **zkompilované** soubory `.xnb`, sestavené
původně v XNA 4.0. Zdrojové 3D modely a textury nejsou v repozitáři, takže
obsah nyní nelze měnit, jen používat. Zdrojové soubory jsou jen u fontů
(`*.spritefont`).

Formát tratí `.lev` je textový, záznamy jsou oddělené znakem `|`:

```
<blok> x,y,posun|...       bloky prostřední (fyzikální) vrstvy
*<blok> x,y,posun|...      bloky zadní dekorační vrstvy
#<blok> x,y,posun|...      bloky přední dekorační vrstvy
<Typ> <model> x,y|...      dynamické objekty (Box, Bar, Tb, Co, …)
```

Fyzikální tvar každého bloku se ve hře vytváří vektorizací bitmapy
`content/Levels/bit/<blok>.xnb`.

## Historie

- Tag `bachelor-thesis-2016` je původní stav přílohy bakalářské práce
  (UWP, MonoGame 3.5, Visual Studio 2015), jen bez výstupů sestavení a binárek.
- Kompletní původní složka včetně zkompilovaných verzí je zálohovaná mimo
  repozitář v `D:\TW3D_bakalarka_2016_puvodni.zip`.
- V roce 2026 byl projekt převeden na .NET 10 + MonoGame 3.8 DesktopGL. Hra se
  od té doby spouští jako běžná desktopová aplikace místo UWP aplikace.
  Vyzkoušená je na Windows. DesktopGL podporuje i Linux a macOS, tam ale
  zatím testovaná nebyla.

## Licence

- **Vlastní kód, herní obsah (`content/`) a dokumentace:** licence [MIT](LICENSE),
  © 2016–2026 Antonín Pražský.
- **Farseer Physics Engine** (`src/FarseerPhysics/`) a soubory odvozené z jeho
  ukázek (`src/TW3D.Game/ScreenSystem/`, `src/TW3D.Game/DrawingSystem/`,
  `src/TW3D.Game/ConvertUnits.cs`): licence Microsoft Public License (Ms-PL),
  viz [`src/FarseerPhysics/LICENSE.txt`](src/FarseerPhysics/LICENSE.txt).
  Farseer vychází z Box2D (© Erin Catto).

## Plán dalšího vývoje

Věci nalezené při převodu, zhruba seřazené podle důležitosti:

1. **Nahradit Farseer knihovnou [Aether.Physics2D](https://github.com/nkast/Aether.Physics2D)**,
   udržovaným nástupcem Farseeru (NuGet `Aether.Physics2D.MG`). Pak bude možné
   smazat `src/FarseerPhysics/`. API se liší hlavně ve vytváření těles
   (`World.CreateBody` místo `BodyFactory`).
2. **Oddělit vlastní kód od ukázkového frameworku Farseeru.** `ScreenSystem/`
   a `DrawingSystem/` vychází z ukázek Farseer Samples a obsahují i nepoužívané
   části (např. `MessageBoxScreen`, text „Welcome, betatesters!“ v `MainGame`).
3. **Zdrojové modely a textury.** Bez nich nejde obsah měnit. Pokud se najdou
   (např. `.blend`/`.fbx`), přidat je do `content/` a zavést MonoGame Content
   Pipeline (`.mgcb`).
4. **Jeden společný seznam bloků a objektů.** Načítání 69 modelů je ručně
   vypsané dvakrát (`MainGame.LoadContent` a `Editor.LoadContent`) a parser
   `.lev` je také dvakrát (hra a editor). Kandidát na sdílenou knihovnu
   a datový soubor (např. JSON).
5. **Robustnější formát tratí.** Parser čte soubor znak po znaku. Editor ukládá
   souřadnice přes `float.ToString()` podle nastavení jazyka systému, takže by
   neceločíselná souřadnice na českém Windows vytvořila desetinnou čárku
   kolidující s oddělovačem. Používat `CultureInfo.InvariantCulture`, případně
   přejít na JSON.
6. **Seznam tratí v menu** je napevno ve `Game1.Initialize`, včetně testovacích
   duplikátů. Lépe ho načítat ze složky `Levels`.
7. **Nedokončené menu:** Garage, About a Options. Volba klávesnice / dotykového
   ovládání jen úpravou kódu.
8. **Editor:** výchozí složka `C:\levels`, závislost na Windows (dialogy
   WinForms, `keybd_event` pro Caps Lock) a ovládání přes dvě okna. Možná
   náhrada: UI přímo v okně editoru (např. ImGui.NET).
9. **Automatické testy a CI**, např. testy parseru tratí a sestavení přes
   GitHub Actions.
