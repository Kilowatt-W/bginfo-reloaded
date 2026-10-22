using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BgInfoReloaded;

/// <summary>
/// Ersetzt {platzhalter} in den Label-/Value-Texten der Konfiguration. Neben den eingebauten Namen
/// ({hostname}, {cpu}, ...) gibt es generische Quellen, ueber die sich das Panel ohne Neubau erweitern
/// laesst: {env:NAME}, {reg:HKLM\Pfad\Wertname}, {wmi:Klasse.Eigenschaft [where ...]} bzw.
/// {wmi:SELECT ...}, {file:Pfad}, {filever:Pfad}, {filetime:Pfad}, {now:Format}. Wie im Rest der App gilt: eine fehlgeschlagene Abfrage ergibt "n/a", keinen Absturz.
/// Unbekannte Platzhalter bleiben woertlich stehen, damit Tippfehler auf dem Wallpaper auffallen.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ValueResolver
{
    public const string NotAvailable = "n/a";

    private static readonly Regex TokenPattern = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Eigene, benannte Platzhalter aus "fields" der Konfiguration.
    private readonly Dictionary<string, string> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _resolving = new(StringComparer.OrdinalIgnoreCase);

    private readonly string? _configVersion;

    // {name,20} kuerzt auf 20 Zeichen, {name,20...} mit Auslassungszeichen, {name,-8} behaelt die letzten 8.
    private static readonly Regex CutPattern = new(@",\s*(-?\d+)\s*(\.\.\.|…)?$", RegexOptions.Compiled);

    /// <summary>Mit den eigenen Feldern und der Version der Konfiguration, fuer die aufgeloest wird.</summary>
    public ValueResolver(AppConfig config) : this(config.Fields, config.Version, config.Format)
    {
        _profileName = config.ActiveProfile;
    }

    /// <summary>Das Profil, aus dem die Konfiguration stammt - null = Standardprofil.</summary>
    private readonly string? _profileName;

    public const string StandardProfileName = "Standardprofil";

    // Datums- und Zahlenformat dieser Konfiguration; gilt auf dem Thread, auf dem aufgeloest wird.
    private readonly FormatConfig _format;

    public ValueResolver(IEnumerable<KeyValuePair<string, string>>? fields = null, string? configVersion = null, FormatConfig? format = null)
    {
        _format = format ?? new FormatConfig();
        ValueFormat.Current = _format;
        _configVersion = configVersion;
        foreach (var (name, definition) in fields ?? Enumerable.Empty<KeyValuePair<string, string>>())
        {
            _fields[name.Trim().Trim('{', '}')] = definition;
        }
    }

    public sealed record BuiltIn(string Name, string Description, Func<string> Read);

    /// <summary>Platzhalter, die nur in "drives"-Zeilen gelten (ersetzt von SystemInfoCollector.FormatDrive).</summary>
    public static IReadOnlyList<(string Tag, string Description)> DriveTags { get; } = new[]
    {
        ("{drive}", "Laufwerksbuchstabe"),
        ("{volume}", "Datenträgername"),
        ("{used}", "Belegt in GB"),
        ("{total}", "Größe in GB"),
        ("{free}", "Frei in GB"),
        ("{percent}", "Belegt in Prozent")
    };

    /// <summary>
    /// Die generischen Quellen als Muster - fuer --list-tags und die Platzhalter-Liste der
    /// Beispielkonfiguration. Kommt in <see cref="ResolveUncached"/> eine Quelle dazu, hier ergaenzen.
    /// </summary>
    public static IReadOnlyList<(string Tag, string Description)> GenericTags { get; } = new[]
    {
        ("{env:NAME}", "Umgebungsvariable"),
        ("{reg:HIVE\\Pfad\\Wertname}", "Registry-Wert"),
        ("{wmi:Klasse.Eigenschaft}", "WMI-Eigenschaft"),
        ("{wmi:Klasse.Eigenschaft where Bedingung}", "WMI mit Filter"),
        ("{wmi:SELECT Eigenschaft FROM Klasse ...}", "Ganze WQL-Abfrage (wie in BGInfo)"),
        ("{file:Pfad}", "Erste nicht-leere Zeile einer Datei"),
        ("{filever:Pfad}", "Dateiversion"),
        ("{filetime:Pfad}", "Änderungsdatum einer Datei"),
        ("{name,20}  {name,20...}  {name,-8}", "Kürzen: Anfang, mit …, bzw. Ende behalten"),
        ("{now:Format}", "Datum/Uhrzeit mit eigenem Format"),
        ("{uptime:Format}", "Laufzeit mit eigenem Format")
    };

    /// <summary>
    /// Einzige Quelle fuer die eingebauten Platzhalter - sowohl die Aufloesung als auch --list-tags
    /// lesen aus dieser Liste. Neue Werte hier ergaenzen.
    /// </summary>
    public static IReadOnlyList<BuiltIn> BuiltIns { get; } = new BuiltIn[]
    {
        new("hostname", "Rechnername", () => Environment.MachineName),
        new("fqdn", "Voll qualifizierter Rechnername", GetFqdn),
        new("user", "Angemeldeter Benutzer mit Domäne", () => $"{Environment.UserDomainName}\\{Environment.UserName}"),
        new("username", "Benutzername", () => Environment.UserName),
        new("domain", "Domäne des Benutzers", () => Environment.UserDomainName),
        new("machinedomain", "Domäne bzw. Arbeitsgruppe des Rechners", () => ReadWmi("Win32_ComputerSystem", "Domain", null)),
        new("logonserver", "Anmeldeserver", () => Environment.GetEnvironmentVariable("LOGONSERVER")?.TrimStart('\\') is { Length: > 0 } server ? server : NotAvailable),
        new("systemtype", "Rolle des Rechners (Workstation, Server, Domänencontroller)", GetSystemType),
        new("ipv4", "Erste IPv4-Adresse eines aktiven Adapters", () => GetPrimaryAddress(AddressFamily.InterNetwork)),
        new("ipv6", "Erste IPv6-Adresse eines aktiven Adapters", () => GetPrimaryAddress(AddressFamily.InterNetworkV6)),
        new("subnet", "Subnetzmaske zur IPv4-Adresse", GetSubnetMask),
        new("mac", "MAC-Adresse des aktiven Adapters", GetMacAddress),
        new("gateway", "Standardgateway", GetGateway),
        new("dns", "DNS-Server", GetDnsServers),
        new("dhcp", "DHCP-Server", GetDhcpServer),
        new("nic", "Name des aktiven Netzwerkadapters", () => PrimaryAdapter()?.Description ?? NotAvailable),
        new("netspeed", "Geschwindigkeit des aktiven Netzwerkadapters", GetNetworkSpeed),
        new("nettype", "Art des aktiven Netzwerkadapters", GetNetworkType),
        new("os", "Windows-Edition", () => ReadWmi("Win32_OperatingSystem", "Caption", null)),
        new("osversion", "Windows-Versionsnummer", () => ReadWmi("Win32_OperatingSystem", "Version", null)),
        new("displayversion", "Windows-Release (z.B. 24H2)", () => ReadRegistry(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\DisplayVersion")),
        new("build", "Windows-Buildnummer", () => ReadWmi("Win32_OperatingSystem", "BuildNumber", null)),
        new("arch", "Architektur des Betriebssystems", () => ReadWmi("Win32_OperatingSystem", "OSArchitecture", null)),
        new("boot", "Letzter Systemstart", () => ReadWmi("Win32_OperatingSystem", "LastBootUpTime", null)),
        new("uptime", "Laufzeit seit dem letzten Start", GetUptime),
        new("timezone", "Zeitzone", () => TimeZoneInfo.Local.DisplayName),
        new("cpu", "Prozessor", () => ReadWmi("Win32_Processor", "Name", null)),
        new("cores", "Logische Prozessorkerne", () => Environment.ProcessorCount.ToString()),
        new("ram", "Arbeitsspeicher", GetTotalRam),
        new("ramfree", "Freier Arbeitsspeicher", GetFreeRam),
        new("ramspeed", "Takt des Arbeitsspeichers", () => WithUnit(ReadWmi("Win32_PhysicalMemory", "Speed", null), "MHz")),
        new("bios", "BIOS-Version", () => ReadWmi("Win32_BIOS", "SMBIOSBIOSVersion", null)),
        new("serial", "Seriennummer des Geräts", () => ReadWmi("Win32_BIOS", "SerialNumber", null)),
        new("manufacturer", "Hersteller", () => ReadWmi("Win32_ComputerSystem", "Manufacturer", null)),
        new("model", "Modell", () => ReadWmi("Win32_ComputerSystem", "Model", null)),
        new("resolution", "Auflösung des primären Monitors", () => { var (w, h) = WallpaperInterop.GetPrimaryScreenResolution(); return $"{w} x {h}"; }),
        new("scaling", "Windows-Skalierung des primären Monitors", () => $"{WallpaperInterop.GetPrimaryScreenDpi() * 100 / 96} %"),
        new("allips", "Alle IPv4-Adressen der aktiven Adapter", GetAllAddresses),
        new("adsite", "Active-Directory-Standort des Rechners", () => ReadRegistry(@"HKLM\SYSTEM\CurrentControlSet\Services\Netlogon\Parameters\DynamicSiteName")),
        new("ou", "Organisationseinheit des Rechners im Active Directory", GetOrganizationalUnit),
        new("session", "Art der Sitzung (Konsole oder RDP)", GetSessionType),
        new("rdpclient", "Name des Rechners, von dem die RDP-Sitzung kommt", () => Environment.GetEnvironmentVariable("CLIENTNAME") is { Length: > 0 } client ? client : NotAvailable),
        new("language", "Anzeigesprache von Windows", () => ReadWmi("Win32_OperatingSystem", "MUILanguages", null)),
        new("installdate", "Installationsdatum von Windows", () => ReadWmi("Win32_OperatingSystem", "InstallDate", null)),
        new("lastupdate", "Zuletzt installiertes Windows-Update", GetLastUpdate),
        new("pendingreboot", "Steht ein Neustart aus (Updates, Komponentenwartung)?", GetPendingReboot),
        new("secureboot", "Secure Boot", GetSecureBoot),
        new("antivirus", "Registrierter Virenschutz (nur Client-Windows)", GetAntivirus),
        new("virtual", "Virtuelle Maschine?", GetVirtualization),
        new("chassis", "Bauform (Desktop, Notebook, Server ...)", GetChassis),
        new("assettag", "Asset-Tag aus dem BIOS", () => ReadWmi("Win32_SystemEnclosure", "SMBIOSAssetTag", null)),
        new("uuid", "UUID des Geräts", () => ReadWmi("Win32_ComputerSystemProduct", "UUID", null)),
        new("biosdate", "Datum des BIOS", () => ValueFormat.AsDateOnly(() => ReadWmi("Win32_BIOS", "ReleaseDate", null))),
        new("gpu", "Grafikkarte", GetGraphicsAdapters),
        new("disk", "Physische Datenträger mit Größe", GetDisks),
        new("rampercent", "Belegter Arbeitsspeicher in Prozent", GetRamPercent),
        new("battery", "Ladestand des Akkus", () => WithUnit(ReadWmi("Win32_Battery", "EstimatedChargeRemaining", null), "%")),
        new("monitors", "Anzahl und Auflösung aller Monitore", () => { var all = WallpaperInterop.GetMonitors(); return $"{all.Count}: {string.Join(", ", all.Select(m => $"{m.Width} x {m.Height}"))}"; }),
        new("now", "Aktuelles Datum und Uhrzeit", () => ValueFormat.FormatDateTime(DateTime.Now)),
        new("appversion", "Version von BGInfo-Reloaded", () => GitHubUpdater.CurrentVersion.ToString()),
        // Den Wert liefert ResolveUncached aus der Konfiguration; hier steht nur der Rueckfall ohne Angabe.
        new("configversion", "Version der Konfigurationsdatei (\"version\")", () => NotAvailable),
        // Wie configversion: den Namen liefert ResolveUncached aus der Konfiguration.
        new("profile", "Profil, das auf diesem Rechner gilt (sonst \"Standardprofil\")", () => StandardProfileName)
    };

    public string Expand(string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }
        // Vor jedem Aufloesen: ein anderer Resolver kann auf diesem Thread inzwischen seine gesetzt haben.
        ValueFormat.Current = _format;
        return TokenPattern.Replace(template, match => Resolve(match.Groups[1].Value.Trim()) ?? match.Value);
    }

    private string? Resolve(string token)
    {
        if (_cache.TryGetValue(token, out var cached))
        {
            return cached;
        }

        string? value;
        try
        {
            var cut = CutPattern.Match(token);
            value = cut.Success && Resolve(token[..cut.Index].Trim()) is { } full
                ? Cut(full, int.Parse(cut.Groups[1].Value), cut.Groups[2].Success)
                : ResolveUncached(token);
        }
        catch
        {
            value = NotAvailable;
        }

        _cache[token] = value;
        return value;
    }

    /// <summary>Positive Laenge: Anfang behalten, negative: Ende behalten. "n/a" bleibt unangetastet.</summary>
    private static string Cut(string value, int length, bool ellipsis)
    {
        var keep = Math.Abs(length);
        if (value.Length <= keep || value == NotAvailable)
        {
            return value;
        }
        if (ellipsis && keep > 0)
        {
            keep--;
        }
        var mark = ellipsis ? "…" : "";
        return length >= 0 ? value[..keep].TrimEnd() + mark : mark + value[^keep..].TrimStart();
    }

    private string? ResolveUncached(string token)
    {
        if (token.Equals("configversion", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_configVersion))
        {
            return _configVersion.Trim();
        }
        if (token.Equals("profile", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_profileName))
        {
            return _profileName;
        }

        // Eigene Felder haben Vorrang vor eingebauten Namen. Innerhalb der eigenen Definition meint
        // derselbe Name wieder den eingebauten Wert ("ipv4": "{ipv4} /24") - das verhindert zugleich
        // Endlosschleifen bei Feldern, die sich gegenseitig benutzen.
        if (_fields.TryGetValue(token, out var definition) && _resolving.Add(token))
        {
            try
            {
                return Expand(definition);
            }
            finally
            {
                _resolving.Remove(token);
            }
        }

        var separator = token.IndexOf(':');
        if (separator > 0)
        {
            var argument = token[(separator + 1)..].Trim();
            switch (token[..separator].Trim().ToLowerInvariant())
            {
                case "env": return Environment.GetEnvironmentVariable(argument) ?? "";
                case "reg": return ReadRegistry(argument);
                case "wmi": return ReadWmiExpression(argument);
                // Pfade duerfen in Anfuehrungszeichen stehen ({file:"C:\Mit Leerzeichen\x.txt"}).
                case "file": return ReadFirstLine(Unquote(argument));
                case "filever": return System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ExpandEnvironmentVariables(Unquote(argument))).FileVersion ?? NotAvailable;
                case "filetime": return ReadFileTime(Unquote(argument));
                case "now": return ValueFormat.FormatWith(DateTime.Now, argument);
                case "uptime": return FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64), argument);
                default: return null;
            }
        }

        return BuiltIns.FirstOrDefault(b => string.Equals(b.Name, token, StringComparison.OrdinalIgnoreCase))?.Read();
    }

    /// <summary>Alle Eigenschaften einer WMI-Klasse (erste Instanz) als fertige Platzhalter mit aktuellem Wert.</summary>
    public static List<(string Tag, string Value)> ListWmiClass(string className, string? where = null)
    {
        var result = new List<(string, string)>();
        var suffix = string.IsNullOrWhiteSpace(where) ? "" : $" where {where.Trim()}";
        using var searcher = new ManagementObjectSearcher($"SELECT * FROM {className}{suffix}");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            foreach (var property in obj.Properties.Cast<PropertyData>().OrderBy(p => p.Name))
            {
                result.Add(($"{{wmi:{className}.{property.Name}{suffix}}}", FormatWmiValue(property) ?? ""));
            }
            break;
        }
        return result;
    }

    /// <summary>
    /// Namen der WMI-Klassen in root\cimv2, die sich sinnvoll abfragen lassen (fuer die Auswahl im Editor).
    /// Ohne Leistungsindikatoren und ohne Win32_Product - dessen Abfrage stoesst eine Konsistenzpruefung
    /// aller installierten MSI-Pakete an.
    /// </summary>
    public static List<string> ListWmiClasses()
    {
        var result = new List<string>();
        using var searcher = new ManagementObjectSearcher("SELECT * FROM meta_class WHERE __CLASS LIKE 'Win32[_]%'");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            var name = obj["__CLASS"]?.ToString();
            if (name != null && !name.StartsWith("Win32_Perf", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("Win32_Product", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(name);
            }
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>Alle Werte eines Registry-Schluessels als fertige Platzhalter mit aktuellem Wert.</summary>
    public static List<(string Tag, string Value)> ListRegistryKey(string keyPath)
    {
        keyPath = keyPath.TrimEnd('\\');
        var result = new List<(string, string)>();
        using var key = OpenRegistryKey(keyPath + "\\") ?? throw new InvalidOperationException($"Registry-Schluessel '{keyPath}' nicht gefunden.");
        foreach (var name in key.GetValueNames().Where(n => n.Length > 0).OrderBy(n => n))
        {
            result.Add(($"{{reg:{keyPath}\\{name}}}", FormatRegistryValue(key.GetValue(name))));
        }
        return result;
    }

    private static string GetUptime() => FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64), null);

    /// <summary>
    /// Laufzeit als Text: ab einem Tag Tage und Stunden, darunter Stunden und Minuten. Die Woerter
    /// richten sich nach "format.culture" - deutsch "3 Tage 4 Std.", englisch "3 days 4 hours"; ohne
    /// Angabe (und fuer andere Sprachen) die sprachneutrale Kurzform "3 d 4 h". Mit eigenem Format
    /// ({uptime:d' Tage 'h' Std.'}) gilt dieses, in der Schreibweise von TimeSpan.
    /// </summary>
    public static string FormatUptime(TimeSpan uptime, string? format)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            try
            {
                return uptime.ToString(format, ValueFormat.Culture);
            }
            catch (FormatException)
            {
                // ungueltiges Format - dann die Standardform
            }
        }

        var days = (int)uptime.TotalDays;
        var language = ValueFormat.ResolveCulture(ValueFormat.Current.Culture)?.TwoLetterISOLanguageName;
        switch (language)
        {
            case "de":
                return days >= 1
                    ? $"{days} {(days == 1 ? "Tag" : "Tage")} {uptime.Hours} Std."
                    : $"{uptime.Hours} Std. {uptime.Minutes} Min.";
            case "en":
                static string Unit(int count, string word) => $"{count} {word}{(count == 1 ? "" : "s")}";
                return days >= 1
                    ? $"{Unit(days, "day")} {Unit(uptime.Hours, "hour")}"
                    : $"{Unit(uptime.Hours, "hour")} {Unit(uptime.Minutes, "minute")}";
            default:
                return days >= 1 ? $"{days} d {uptime.Hours} h" : $"{uptime.Hours} h {uptime.Minutes} min";
        }
    }

    private static IEnumerable<NetworkInterface> ActiveAdapters() =>
        NetworkInterface.GetAllNetworkInterfaces().Where(nic =>
            nic.OperationalStatus == OperationalStatus.Up &&
            nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel));

    /// <summary>Der Adapter, dessen Adresse {ipv4} liefert; ohne IPv4-Adresse der erste aktive.</summary>
    private static NetworkInterface? PrimaryAdapter()
    {
        var adapters = ActiveAdapters().ToList();
        return adapters.FirstOrDefault(nic => nic.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
            ?? adapters.FirstOrDefault();
    }

    private static string GetDhcpServer()
    {
        var server = PrimaryAdapter()?.GetIPProperties().DhcpServerAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        return server?.ToString() ?? NotAvailable;
    }

    private static string GetNetworkSpeed()
    {
        var speed = PrimaryAdapter()?.Speed ?? -1;
        if (speed <= 0)
        {
            return NotAvailable;
        }
        return speed >= 1_000_000_000 ? $"{ValueFormat.FormatNumber(speed / 1_000_000_000.0)} Gbit/s" : $"{ValueFormat.FormatNumber(speed / 1_000_000.0)} Mbit/s";
    }

    private static string GetNetworkType() => PrimaryAdapter()?.NetworkInterfaceType switch
    {
        null => NotAvailable,
        NetworkInterfaceType.Wireless80211 => "WLAN",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => "Ethernet",
        { } other => other.ToString()
    };

    private static string GetSystemType() => ReadWmi("Win32_OperatingSystem", "ProductType", null) switch
    {
        "1" => "Workstation",
        "2" => "Domänencontroller",
        "3" => "Server",
        _ => NotAvailable
    };

    private static string GetAllAddresses()
    {
        var addresses = ActiveAdapters()
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .Distinct()
            .ToList();
        return addresses.Count > 0 ? string.Join(", ", addresses) : NotAvailable;
    }

    /// <summary>
    /// OU-Pfad von oben nach unten ("Berlin/Clients"), gelesen aus dem Distinguished Name, den die
    /// Gruppenrichtlinien-Verarbeitung in der Registry hinterlegt - ohne LDAP-Abfrage, funktioniert
    /// also auch ohne Verbindung zum Domaenencontroller.
    /// </summary>
    private static string GetOrganizationalUnit()
    {
        var dn = ReadRegistry(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Group Policy\State\Machine\Distinguished-Name");
        var units = Regex.Split(dn, @"(?<!\\),")
            .Where(part => part.TrimStart().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            .Select(part => part.Trim()[3..].Replace("\\,", ","))
            .Reverse()
            .ToList();
        return units.Count > 0 ? string.Join("/", units) : NotAvailable;
    }

    private static string GetSessionType()
    {
        var name = Environment.GetEnvironmentVariable("SESSIONNAME");
        if (string.IsNullOrEmpty(name))
        {
            return NotAvailable;
        }
        return name.StartsWith("RDP", StringComparison.OrdinalIgnoreCase) ? "RDP"
            : name.Equals("Console", StringComparison.OrdinalIgnoreCase) ? "Konsole"
            : name;
    }

    /// <summary>Juengster Eintrag aus Win32_QuickFixEngineering: "2026-09-12 (KB5031234)".</summary>
    private static string GetLastUpdate()
    {
        (DateTime Date, string Id)? latest = null;
        using var searcher = new ManagementObjectSearcher("SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            // InstalledOn kommt als Text im US-Format (M/d/yyyy).
            if (DateTime.TryParse(obj["InstalledOn"]?.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date) && (latest == null || date > latest.Value.Date))
            {
                latest = (date, obj["HotFixID"]?.ToString() ?? "");
            }
        }
        return latest is { } found ? $"{ValueFormat.FormatDate(found.Date)} ({found.Id})" : NotAvailable;
    }

    private static bool RegistryKeyExists(string path)
    {
        using var key = OpenRegistryKey(path + "\\");
        return key != null;
    }

    private static string GetPendingReboot() =>
        RegistryKeyExists(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
        RegistryKeyExists(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")
            ? "ja"
            : "nein";

    private static string GetSecureBoot() =>
        ReadRegistry(@"HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootEnabled") switch
        {
            "1" => "aktiv",
            "0" => "aus",
            _ => NotAvailable
        };

    private static string GetAntivirus()
    {
        var names = new List<string>();
        using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT displayName FROM AntiVirusProduct");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            if (obj["displayName"]?.ToString() is { Length: > 0 } name && !names.Contains(name))
            {
                names.Add(name);
            }
        }
        return names.Count > 0 ? string.Join(", ", names) : NotAvailable;
    }

    /// <summary>Am Hersteller/Modell erkannt - HypervisorPresent waere auch auf echter Hardware mit Hyper-V/VBS wahr.</summary>
    private static string GetVirtualization()
    {
        var system = $"{ReadWmi("Win32_ComputerSystem", "Manufacturer", null)} {ReadWmi("Win32_ComputerSystem", "Model", null)}";
        foreach (var (marker, product) in new[]
                 {
                     ("VMware", "VMware"), ("VirtualBox", "VirtualBox"), ("Virtual Machine", "Hyper-V"), ("KVM", "KVM"),
                     ("QEMU", "QEMU"), ("Xen", "Xen"), ("Parallels", "Parallels"), ("Amazon EC2", "AWS"), ("Google", "Google Cloud")
                 })
        {
            if (system.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return $"ja ({product})";
            }
        }
        return "nein";
    }

    private static string GetChassis()
    {
        var first = ReadWmi("Win32_SystemEnclosure", "ChassisTypes", null).Split(',')[0].Trim();
        return int.TryParse(first, out var type)
            ? type switch
            {
                3 or 4 or 5 or 6 or 7 or 13 or 15 or 16 or 24 or 35 or 36 => "Desktop",
                8 or 9 or 10 or 11 or 12 or 14 or 18 or 21 or 31 or 32 => "Notebook",
                30 => "Tablet",
                17 or 23 or 25 or 28 or 29 => "Server",
                _ => "Sonstige"
            }
            : NotAvailable;
    }

    /// <summary>Alle Grafikadapter; Spiegeltreiber von Fernwartungsprogrammen nur, wenn es sonst keinen gibt.</summary>
    private static string GetGraphicsAdapters()
    {
        var names = new List<string>();
        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            if (obj["Name"]?.ToString()?.Trim() is { Length: > 0 } name && !names.Contains(name))
            {
                names.Add(name);
            }
        }
        var real = names.Where(name => !name.Contains("Mirror", StringComparison.OrdinalIgnoreCase)).ToList();
        return names.Count == 0 ? NotAvailable : string.Join(", ", real.Count > 0 ? real : names);
    }

    private static string GetDisks()
    {
        var disks = new List<string>();
        using var searcher = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            var size = long.TryParse(obj["Size"]?.ToString(), out var bytes) ? $" ({bytes / 1024.0 / 1024.0 / 1024.0:0} GB)" : "";
            disks.Add($"{obj["Model"]?.ToString()?.Trim()}{size}");
        }
        return disks.Count > 0 ? string.Join(", ", disks) : NotAvailable;
    }

    private static string GetRamPercent()
    {
        var total = ReadWmi("Win32_OperatingSystem", "TotalVisibleMemorySize", null);
        var free = ReadWmi("Win32_OperatingSystem", "FreePhysicalMemory", null);
        return double.TryParse(total, out var totalKb) && double.TryParse(free, out var freeKb) && totalKb > 0
            ? $"{(totalKb - freeKb) / totalKb * 100:0} %"
            : NotAvailable;
    }

    private static string GetFreeRam()
    {
        var raw = ReadWmi("Win32_OperatingSystem", "FreePhysicalMemory", null); // in KB
        return long.TryParse(raw, out var freeKb) ? $"{ValueFormat.FormatNumber(freeKb / 1024.0 / 1024.0)} GB" : NotAvailable;
    }

    private static string GetMacAddress()
    {
        var bytes = ActiveAdapters().Select(nic => nic.GetPhysicalAddress().GetAddressBytes()).FirstOrDefault(b => b.Length > 0);
        return bytes != null ? string.Join("-", bytes.Select(b => b.ToString("X2"))) : NotAvailable;
    }

    private static string GetGateway()
    {
        var gateway = ActiveAdapters()
            .SelectMany(nic => nic.GetIPProperties().GatewayAddresses)
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
        return gateway?.Address.ToString() ?? NotAvailable;
    }

    private static string GetDnsServers()
    {
        var servers = ActiveAdapters()
            .SelectMany(nic => nic.GetIPProperties().DnsAddresses)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.ToString())
            .Distinct()
            .ToList();
        return servers.Count > 0 ? string.Join(", ", servers) : NotAvailable;
    }

    private static string WithUnit(string value, string unit) =>
        value == NotAvailable ? value : $"{value} {unit}";

    private static string GetTotalRam()
    {
        var raw = ReadWmi("Win32_ComputerSystem", "TotalPhysicalMemory", null);
        return long.TryParse(raw, out var totalBytes)
            ? $"{ValueFormat.FormatNumber(totalBytes / 1024.0 / 1024.0 / 1024.0)} GB"
            : NotAvailable;
    }

    private static string GetFqdn()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var host = properties.HostName;
        var domain = properties.DomainName;
        return string.IsNullOrWhiteSpace(domain) ? host : $"{host}.{domain}";
    }

    private static string GetPrimaryAddress(AddressFamily family)
    {
        var address = ActiveAdapters()
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == family && !a.IsIPv6LinkLocal);
        return address?.ToString() ?? NotAvailable;
    }

    /// <summary>Maske derselben Adresse, die {ipv4} liefert.</summary>
    private static string GetSubnetMask()
    {
        var address = ActiveAdapters()
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        return address?.IPv4Mask.ToString() ?? NotAvailable;
    }

    /// <summary>
    /// "Klasse.Eigenschaft", "Klasse.Eigenschaft where Bedingung" oder - wie in BGInfo - eine ganze
    /// WQL-Abfrage ("SELECT Eigenschaft FROM Klasse ..."); dann zaehlt die erste Eigenschaft mit Wert.
    /// </summary>
    private static string ReadWmiExpression(string expression)
    {
        if (expression.StartsWith("select ", StringComparison.OrdinalIgnoreCase))
        {
            using var searcher = new ManagementObjectSearcher(expression);
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                foreach (PropertyData property in obj.Properties)
                {
                    if (FormatWmiValue(property) is { } value)
                    {
                        return value;
                    }
                }
            }
            return NotAvailable;
        }

        string? where = null;
        var whereIndex = expression.IndexOf(" where ", StringComparison.OrdinalIgnoreCase);
        if (whereIndex > 0)
        {
            where = expression[(whereIndex + 7)..].Trim();
            expression = expression[..whereIndex].Trim();
        }

        var dot = expression.IndexOf('.');
        if (dot <= 0 || dot == expression.Length - 1)
        {
            return NotAvailable;
        }
        return ReadWmi(expression[..dot].Trim(), expression[(dot + 1)..].Trim(), where);
    }

    private static string ReadWmi(string className, string propertyName, string? where)
    {
        try
        {
            var wql = $"SELECT {propertyName} FROM {className}" + (where != null ? $" WHERE {where}" : "");
            using var searcher = new ManagementObjectSearcher(wql);
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                var value = FormatWmiValue(obj.Properties[propertyName]);
                if (value != null)
                {
                    return value;
                }
            }
        }
        catch
        {
            // WMI-Provider nicht verfuegbar / Zugriff verweigert - dann gibt's halt "n/a" statt Absturz.
        }
        return NotAvailable;
    }

    /// <summary>null, wenn die Eigenschaft keinen Wert hat.</summary>
    private static string? FormatWmiValue(PropertyData property)
    {
        if (property.Value == null)
        {
            return null;
        }
        if (property.Type == CimType.DateTime && property.Value is string wmiDate)
        {
            return ValueFormat.FormatWmiDate(ManagementDateTimeConverter.ToDateTime(wmiDate));
        }
        if (property.Value is Array array)
        {
            return string.Join(", ", array.Cast<object>());
        }
        return property.Value.ToString()?.Trim() ?? NotAvailable;
    }

    /// <summary>"HKLM\Pfad\zum\Schluessel\Wertname" - das letzte Segment ist der Wertname.</summary>
    private static string ReadRegistry(string path)
    {
        if (!path.Contains('\\'))
        {
            path = path.Replace('/', '\\');
        }

        using var key = OpenRegistryKey(path);
        return FormatRegistryValue(key?.GetValue(path[(path.LastIndexOf('\\') + 1)..]));
    }

    /// <summary>Oeffnet den Schluessel vor dem letzten Backslash ("HIVE\Pfad\Rest" -> "HIVE\Pfad").</summary>
    private static RegistryKey? OpenRegistryKey(string path)
    {
        var firstSeparator = path.IndexOf('\\');
        var lastSeparator = path.LastIndexOf('\\');
        if (firstSeparator <= 0 || lastSeparator <= firstSeparator)
        {
            return null;
        }

        RegistryHive hive;
        switch (path[..firstSeparator].ToUpperInvariant())
        {
            case "HKLM" or "HKEY_LOCAL_MACHINE": hive = RegistryHive.LocalMachine; break;
            case "HKCU" or "HKEY_CURRENT_USER": hive = RegistryHive.CurrentUser; break;
            case "HKCR" or "HKEY_CLASSES_ROOT": hive = RegistryHive.ClassesRoot; break;
            case "HKU" or "HKEY_USERS": hive = RegistryHive.Users; break;
            default: return null;
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return baseKey.OpenSubKey(path[(firstSeparator + 1)..lastSeparator]);
    }

    private static string FormatRegistryValue(object? value) => value switch
    {
        null => NotAvailable,
        string[] multi => string.Join(", ", multi),
        byte[] bytes => Convert.ToHexString(bytes),
        _ => value.ToString() ?? NotAvailable
    };

    private static string ReadFileTime(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path);
        return File.Exists(path) ? ValueFormat.FormatDateTime(File.GetLastWriteTime(path)) : NotAvailable;
    }

    /// <summary>Einfache oder doppelte Anfuehrungszeichen um einen Pfad entfernen.</summary>
    private static string Unquote(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length >= 2 && (trimmed[0] == '"' && trimmed[^1] == '"' || trimmed[0] == '\'' && trimmed[^1] == '\'')
            ? trimmed[1..^1].Trim()
            : trimmed;
    }

    private static string ReadFirstLine(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path);
        return File.ReadLines(path).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "";
    }
}
