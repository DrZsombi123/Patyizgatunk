# Patyizgatunk („Kimaradás”)

Felülnézetes, 2D pixel-art humoros játék Godot 4.7 + C# alapon. Kunu Márió és Lakatos Brendon bejárja a várost: vásárol, autózik, TikTok élőzik, kaszinózik és menekül a rendőrök elől. A cél: **500 aura**.

![Játék](docs/img/jatek.png)

## Letöltés és futtatás

1. **Releases** → `Patyizgatunk-v1.0-windows-x64.zip` letöltése.
2. Kicsomagolás, majd `Patyizgatunk.exe` indítása. A `.pck` fájl és a `data_…` mappa maradjon az exe mellett.

Forrásból: Godot 4.7.2 .NET + .NET 8 SDK, a `project.godot` megnyitása, majd F5.

## Irányítás

| Billentyű | Funkció | Billentyű | Funkció |
|---|---|---|---|
| WASD | Mozgás / vezetés | F1 | Telefon |
| Space | Ugrás | N | Nappal / éjszaka |
| E | Interakció | 1–6, görgő | Tárgy kiválasztása |
| F | Autó be / ki | Q, bal klikk | Tárgy használata |
| Esc | Menü | | |

## Csapat

| Szerepkör | Név |
|---|---|
| Scrum Master | Molnár János |
| Lead Developer | Girán Zsombor |
| UI/UX & QA | Sándor Gergő |

- Projekttábla: [Trello](https://trello.com/b/Jb7AZ2yB/patyizgatunk)
- Teljes dokumentáció: [DOKUMENTACIO.md](DOKUMENTACIO.md) (Word változat: [docs/Dokumentacio.docx](docs/Dokumentacio.docx))

## Fejlesztés

- **Branching:** a `main` a kiadható változat. Minden funkció külön ágon készül, és Pull Requesttel kerül be.
- **Automata füstteszt** (képernyőképeket is ment a `docs/img` mappába):

```
Godot_v4.7.2-stable_mono_win64_console.exe --path . -s res://Tests/SmokeTest.gd
```
