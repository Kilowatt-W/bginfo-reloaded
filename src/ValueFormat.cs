using System.Globalization;

namespace BgInfoReloaded;

/// <summary>
/// Formatiert Datums- und Zahlenwerte der Platzhalter nach den Einstellungen unter "format" in der
/// Konfiguration. Die Leser der Platzhalter sind statische Funktionen ohne Zugriff auf die
/// Konfiguration; der <see cref="ValueResolver"/> setzt deshalb vor jedem Aufloesen die fuer ihn
/// geltenden Einstellungen - je Thread, weil der Editor Vorschau und aktuelle Werte gleichzeitig
/// auf verschiedenen Threads mit verschiedenen Kopien der Konfiguration berechnet.
/// Ein ungueltiges Format oder eine unbekannte Kultur faellt auf den Standard zurueck, statt den
/// Lauf scheitern zu lassen.
/// </summary>
public static class ValueFormat
{
    private static readonly FormatConfig Defaults = new();

    [ThreadStatic]
    private static FormatConfig? _current;

    [ThreadStatic]
    private static bool _dateOnly;

    /// <summary>Die Einstellungen, die auf diesem Thread gerade gelten.</summary>
    public static FormatConfig Current
    {
        get => _current ?? Defaults;
        set => _current = value;
    }

    /// <summary>Angabe in "format.culture" fuer "so wie das jeweilige Windows eingestellt ist".</summary>
    public const string SystemCultureName = "system";

    /// <summary>Die Kultur, mit der Windows das Programm gestartet hat - gemerkt, bevor <see cref="PinInvariantCulture"/> sie ersetzt.</summary>
    public static CultureInfo SystemCulture { get; private set; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Ganz am Anfang von Main aufrufen: merkt sich die Kultur von Windows und stellt das Programm
    /// dann auf die neutrale Kultur. So bleibt alles, was nicht ausdruecklich ueber "format"
    /// formatiert wird, unabhaengig vom Rechner gleich (Punkt als Dezimaltrenner, englische Namen) -
    /// wie bisher, als das Programm ganz ohne Kulturdaten gebaut war.
    /// </summary>
    public static void PinInvariantCulture()
    {
        SystemCulture = CultureInfo.CurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Kultur fuer Trennzeichen sowie Monats- und Tagesnamen: leer = neutral (wie bisher),
    /// "system" = wie Windows, sonst die genannte ("de-DE"); unbekannt = neutral.
    /// </summary>
    public static CultureInfo Culture => ResolveCulture(Current.Culture) ?? CultureInfo.InvariantCulture;

    /// <summary>Null bei leerer oder unbekannter Angabe.</summary>
    public static CultureInfo? ResolveCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        if (string.Equals(name.Trim(), SystemCultureName, StringComparison.OrdinalIgnoreCase))
        {
            return SystemCulture;
        }
        try
        {
            return CultureInfo.GetCultureInfo(name.Trim());
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Reines Datum ("format.date").</summary>
    public static string FormatDate(DateTime value) => Apply(value, Current.Date, Defaults.Date);

    /// <summary>Datum mit Uhrzeit ("format.dateTime").</summary>
    public static string FormatDateTime(DateTime value) => Apply(value, Current.DateTime, Defaults.DateTime);

    /// <summary>Datumswert aus WMI: mit Uhrzeit, innerhalb von <see cref="AsDateOnly"/> ohne.</summary>
    public static string FormatWmiDate(DateTime value) => _dateOnly ? FormatDate(value) : FormatDateTime(value);

    /// <summary>Mit einem ausdruecklich angegebenen Format ({now:Format}); ungueltig = Standard mit Uhrzeit.</summary>
    public static string FormatWith(DateTime value, string format) => Apply(value, format, Defaults.DateTime);

    /// <summary>Fuehrt eine Abfrage so aus, dass Datumswerte aus WMI ohne Uhrzeit formatiert werden (z.B. BIOS-Datum).</summary>
    public static string AsDateOnly(Func<string> read)
    {
        var before = _dateOnly;
        _dateOnly = true;
        try
        {
            return read();
        }
        finally
        {
            _dateOnly = before;
        }
    }

    /// <summary>Groessen und Geschwindigkeiten: hoechstens "format.decimals" Nachkommastellen, Trennzeichen nach Kultur.</summary>
    public static string FormatNumber(double value)
    {
        var decimals = Math.Clamp(Current.Decimals, 0, 6);
        return value.ToString(decimals == 0 ? "0" : "0." + new string('#', decimals), Culture);
    }

    private static string Apply(DateTime value, string? format, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            try
            {
                return value.ToString(format, Culture);
            }
            catch (FormatException)
            {
                // z.B. ein einzelner Buchstabe, der kein Standardformat ist - dann der Standard
            }
        }
        return value.ToString(fallback, Culture);
    }
}
