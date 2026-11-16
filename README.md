# Snapper

Schneller Screenshot-Ausschnitt mit Editor für Windows. Ein Nachbau der Grundidee von Skitch.

Programm starten → der Bildschirm friert ein → Bereich aufziehen → im Editor beschriften → kopieren oder speichern.

## Funktionen

- **Sofort startklar:** Beim Start wird der Bildschirm eingefroren, du ziehst ein Rechteck auf (mit Größenanzeige). `Esc` bricht ab.
- **Mehrere Bildschirme** und Windows-Skalierung (z. B. 125 %) werden unterstützt. Der Ausschnitt ist pixelgenau.
- **Pfeil** im Skitch-Stil (hinten spitz, vorne dick), wächst live beim Ziehen. Weitere Stile: Gerade, Dick, Doppelt, Linie. `Umschalt` rastet die Winkel ein.
- **Text**, **Rechteck** und **Verpixeln** (nicht umkehrbar, auch für Namen und Passwörter).
- 6 Farben, 3 Dicken, Rückgängig und Wiederholen.
- **Kopieren** in die Zwischenablage, **Speichern** als PNG nach `Bilder\Screenshots` (Name mit Datum und Uhrzeit).
- Zoom mit `Strg` + Mausrad, Verschieben mit der mittleren Maustaste.

## Tastenkürzel

| Taste | Aktion |
| --- | --- |
| `A` `T` `R` `P` | Pfeil, Text, Rechteck, Verpixeln |
| `Strg+Z` / `Strg+Y` | Rückgängig / Wiederholen |
| `Strg+C` | Kopieren |
| `Strg+S` | Speichern |
| `Strg+N` | Neuer Ausschnitt |
| `Strg+0` | Ansicht einpassen |
| `Esc` | Editor schließen |

## Bauen

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
# Entwickeln und starten
dotnet run --project src\Snapper

# Fertige Snapper.exe nach publish\ bauen
powershell -File publish.ps1
```

Die `Snapper.exe` kannst du an die Taskleiste heften. Ein Klick startet sofort die Auswahl.
Zum Ausführen braucht man die [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download).

## Selbsttest (für Entwickler)

```powershell
src\Snapper\bin\Debug\net10.0-windows\Snapper.exe --selftest C:\Temp\snapper-test
```

Zeichnet per Code in einen Beispiel-Ausschnitt und legt `result.png` (fertiges Bild), `window.png` (Editor) und `info.txt` ab. Es werden keine Mausklicks simuliert.

## Noch nicht drin

Pfeile und Objekte nachträglich ändern und verschieben, Stift, Radierer, Zuschneiden, globales Tastenkürzel, Fenster- und Vollbild-Auswahl, Weichzeichnen.

Fehler werden nach `%LOCALAPPDATA%\Snapper\log.txt` geschrieben.
