using System.Globalization;
using System.Windows.Markup;

namespace Snapper;

/// <summary>
/// Texte der Oberfläche in Deutsch und Englisch. Die Sprache folgt der Windows-Anzeigesprache:
/// Deutsch bekommt Deutsch, alle anderen Englisch. Mit "--lang de|en" lässt sie sich überschreiben.
/// Neue Sprache = neue Spalte in <see cref="Table"/> plus ein Eintrag in <see cref="Pick"/>.
/// </summary>
internal static class Loc
{
    public static string Lang { get; private set; } = Pick(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static void Override(string? lang)
    {
        if (lang != null) Lang = Pick(lang);
    }

    private static string Pick(string iso) => iso.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";

    /// <summary>Text zum Schlüssel in der aktuellen Sprache. Fehlt er, wird der Schlüssel selbst gezeigt.</summary>
    public static string T(string key) =>
        Table.TryGetValue(key, out var v) ? (Lang == "de" ? v.De : v.En) : key;

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, (string De, string En)> Table = new()
    {
        // Fenstertitel
        ["overlay.title"] = ("Snapper Auswahl", "Snapper Selection"),
        ["drag.title"] = ("Snapper Ziehen", "Snapper Drag"),

        // Auswahl-Ansicht
        ["overlay.hint"] = ("Bereich aufziehen", "Drag to select an area"),
        ["overlay.esc"] = ("   Esc = Abbrechen", "   Esc = Cancel"),

        // Kopfzeile
        ["undo"] = ("Rückgängig (Strg+Z)", "Undo (Ctrl+Z)"),
        ["redo"] = ("Wiederholen (Strg+Y)", "Redo (Ctrl+Y)"),
        ["new.tip"] = ("Neuen Ausschnitt aufnehmen (Strg+N)", "Capture a new area (Ctrl+N)"),
        ["new"] = ("Neuer Ausschnitt", "New capture"),
        ["update.tip"] = ("Öffnet die Download-Seite", "Opens the download page"),
        ["update.default"] = ("Update verfügbar", "Update available"),
        ["update.version"] = ("Update {0} verfügbar", "Update {0} available"),
        ["minimize"] = ("Minimieren", "Minimize"),
        ["maximize"] = ("Maximieren", "Maximize"),
        ["close"] = ("Schließen (Esc)", "Close (Esc)"),

        // Werkzeuge
        ["tool.arrow.tip"] = ("Pfeil (A)", "Arrow (A)"),
        ["tool.text.tip"] = ("Text (T)", "Text (T)"),
        ["tool.rect.tip"] = ("Rechteck (R)", "Rectangle (R)"),
        ["tool.pixelate.tip"] = ("Verpixeln (P)", "Pixelate (P)"),
        ["tool.arrow"] = ("Pfeil", "Arrow"),
        ["tool.text"] = ("Text", "Text"),
        ["tool.rect"] = ("Rechteck", "Rectangle"),
        ["tool.pixelate"] = ("Verpixeln", "Pixelate"),
        ["hint.arrow"] = ("Maustaste halten und ziehen. Der Pfeil wächst mit. Umschalt = gerade Winkel.",
                          "Hold the mouse button and drag. The arrow grows as you drag. Shift = straight angles."),
        ["hint.text"] = ("Ins Bild klicken und tippen. Enter setzt den Text, Umschalt+Enter macht eine neue Zeile.",
                         "Click into the image and type. Enter places the text, Shift+Enter starts a new line."),
        ["hint.rect"] = ("Ziehen, um einen Rahmen zu zeichnen.", "Drag to draw a frame."),
        ["hint.pixelate"] = ("Ziehen. Der Bereich wird verpixelt, z. B. für Namen oder Passwörter.",
                             "Drag. The area gets pixelated, e.g. for names or passwords."),

        // Zoom
        ["zoom.out"] = ("Verkleinern", "Zoom out"),
        ["zoom.in"] = ("Vergrößern", "Zoom in"),
        ["fit"] = ("Einpassen", "Fit"),

        // Einstellungen
        ["label.style"] = ("STIL", "STYLE"),
        ["label.color"] = ("FARBE", "COLOR"),
        ["label.thickness"] = ("DICKE", "THICKNESS"),
        ["label.grain"] = ("KÖRNUNG", "BLOCK SIZE"),
        ["size.thin"] = ("Dünn", "Thin"),
        ["size.medium"] = ("Mittel", "Medium"),
        ["size.thick"] = ("Dick", "Thick"),
        ["color.red"] = ("Rot", "Red"),
        ["color.yellow"] = ("Gelb", "Yellow"),
        ["color.green"] = ("Grün", "Green"),
        ["color.blue"] = ("Blau", "Blue"),
        ["color.black"] = ("Schwarz", "Black"),
        ["color.white"] = ("Weiß", "White"),
        ["arrow.classic"] = ("Klassisch", "Classic"),
        ["arrow.straight"] = ("Gerade", "Straight"),
        ["arrow.thick"] = ("Dick", "Thick"),
        ["arrow.double"] = ("Doppelt", "Double"),
        ["arrow.line"] = ("Linie", "Line"),

        // Knöpfe
        ["copy"] = ("Kopieren", "Copy"),
        ["copy.keys"] = ("  Strg+C", "  Ctrl+C"),
        ["copy.tip"] = ("In die Zwischenablage (Strg+C)", "To the clipboard (Ctrl+C)"),
        ["save"] = ("Speichern", "Save"),
        ["save.keys"] = ("  Strg+S", "  Ctrl+S"),
        ["save.tip"] = ("Als PNG speichern (Strg+S)", "Save as PNG (Ctrl+S)"),
        ["drag"] = ("Ziehen", "Drag"),
        ["drag.tip"] = ("Festhalten und in eine App, Mail oder einen Chat ziehen",
                        "Hold and drag into an app, an email or a chat"),

        // Meldungen
        ["toast.copied"] = ("In die Zwischenablage kopiert", "Copied to the clipboard"),
        ["toast.copy.fail"] = ("Kopieren hat nicht geklappt", "Copying failed"),
        ["toast.saved"] = ("Gespeichert in Bilder\\Screenshots  ·  Klick öffnet den Ordner",
                           "Saved to Pictures\\Screenshots  ·  Click to open the folder"),
        ["toast.save.fail"] = ("Speichern hat nicht geklappt", "Saving failed"),
        ["toast.drag.fail"] = ("Ziehen hat nicht geklappt", "Dragging failed"),

        // Nur für die Beispielbilder des Selbsttests
        ["demo.click"] = ("Hier klicken!", "Click here!"),
        ["demo.win1"] = ("Verkaufszahlen", "Sales figures"),
        ["demo.win2"] = ("Projektübersicht", "Project overview"),
    };
}

/// <summary>XAML-Zugriff: <c>Text="{local:T overlay.hint}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public class TExtension : MarkupExtension
{
    public TExtension(string key) => Key = key;
    public string Key { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
