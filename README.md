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
- **Ziehen:** Der rote Knopf „Ziehen“ unten im Editor gibt das fertige Bild als PNG-Datei weiter. Festhalten und in Mail, Chat, Explorer oder Claude Code fallen lassen. Die Datei liegt in `%TEMP%Snapper` und wird nach 7 Tagen automatisch gelöscht.
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

Die `Snapper.exe` (ca. 140 MB) bringt .NET selbst mit. Auf dem Rechner muss nichts installiert sein.
Du kannst sie an die Taskleiste heften. Ein Klick startet sofort die Auswahl.

Hinweis: Die `.exe` ist nicht signiert. Beim ersten Start nach einem Download zeigt Windows evtl. „Der Computer wurde durch Windows geschützt“. Dann auf „Weitere Informationen“ und „Trotzdem ausführen“ klicken.

## Neue Version veröffentlichen

1. In `src\Snapper\Snapper.csproj` die `<Version>` hochzählen (z. B. `1.1.0`).
2. `powershell -File publish.ps1` ausführen. Das baut `publish\Snapper.exe`.
3. Auf GitHub unter **Releases → Draft a new release** einen Tag anlegen, der zur Version passt (z. B. `v1.1.0`), und `publish\Snapper.exe` anhängen. Auf „Publish release“ klicken.

Snapper fragt beim Öffnen des Editors (höchstens alle 12 Stunden) bei GitHub nach dem neuesten Release. Ist die Version dort höher, erscheint oben der Knopf „Update x.y.z verfügbar“. Ein Klick öffnet die Release-Seite zum Herunterladen. Dabei werden keine Daten über den Nutzer gesendet. Das Update ersetzt man von Hand: Snapper schließen und die alte `Snapper.exe` überschreiben.

## Selbsttest (für Entwickler)

```powershell
src\Snapper\bin\Debug\net10.0-windows\Snapper.exe --selftest C:\Temp\snapper-test
```

Zeichnet per Code in einen Beispiel-Ausschnitt und legt `result.png` (fertiges Bild), `window.png` (Editor) und `info.txt` ab. Es werden keine Mausklicks simuliert.

## Noch nicht drin

Pfeile und Objekte nachträglich ändern und verschieben, Stift, Radierer, Zuschneiden, globales Tastenkürzel, Fenster- und Vollbild-Auswahl, Weichzeichnen.

Die Update-Prüfung merkt sich ihr Ergebnis in `%LOCALAPPDATA%\Snapper\update.json`.
Fehler werden nach `%LOCALAPPDATA%\Snapper\log.txt` geschrieben.
