#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Translator-facing tooling around the string tables in Resources/Localization/Tables:
///   • Report Missing Translations — per locale, which source strings have no entry.
///   • Export CSV — one spreadsheet (key, context, en, zh-Hans, …) for translators.
///   • Import CSV — writes edited columns back into the per-locale JSON tables.
/// Run "Extract Source Strings" first so the report/CSV know what the game contains.
/// </summary>
public static class LocalizationTools
{
    public const string TablesFolder = "Assets/Resources/Localization/Tables";
    public const string CsvPath = "Assets/Localization/strings.csv";
    public const string ReportPath = "Assets/Localization/missing_report.txt";

    // ------------------------------------------------------------------ tables

    public static string TablePath(string code) => TablesFolder + "/" + code + ".json";

    public static Dictionary<string, string> LoadTable(string code)
    {
        string path = TablePath(code);
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);
        return StringTableData.Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    public static void SaveTable(string code, IDictionary<string, string> table)
    {
        Directory.CreateDirectory(TablesFolder);
        var ordered = table.OrderBy(kv => IsExplicitKey(kv.Key) ? 0 : 1).ThenBy(kv => kv.Key, StringComparer.Ordinal);
        File.WriteAllText(TablePath(code), StringTableData.Serialize(code, ordered), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(TablePath(code));
    }

    /// <summary>Explicit keys are dotted identifiers without spaces ("ui.menu.play"); anything else is source text.</summary>
    public static bool IsExplicitKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.IndexOf(' ') >= 0 || key.IndexOf('\n') >= 0) return false;
        int dot = key.IndexOf('.');
        return dot > 0 && dot < key.Length - 1 && key.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-');
    }

    public static List<string> LocaleCodes()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>("Assets/Resources/" + LocalizationSettings.ResourcesPath + ".asset");
        if (settings != null && settings.locales.Count > 0) return settings.locales.Select(l => l.code).ToList();
        if (!Directory.Exists(TablesFolder)) return new List<string> { "en" };
        return Directory.GetFiles(TablesFolder, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(c => c == "en" ? "" : c).ToList();
    }

    // ------------------------------------------------------------------ report

    [MenuItem("Tools/Graveyard/Localization/2. Report Missing Translations")]
    public static void ReportMissingMenu()
    {
        var src = LocalizationExtractor.LoadSourceStrings();
        if (src == null)
        {
            if (EditorUtility.DisplayDialog("Localization", "No source_strings.json yet. Extract source strings now?", "Extract", "Cancel"))
                src = LocalizationExtractor.Extract(true);
            if (src == null) return;
        }

        string report = ReportMissing(src);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Localization", "Report written to " + ReportPath + " (also in the Console).", "OK");
    }

    /// <summary>Builds the per-locale missing/unused report, writes it to <see cref="ReportPath"/> and returns it.</summary>
    public static string ReportMissing(LocalizationExtractor.SourceStringsFile src)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Missing translation report — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine("Source strings: " + src.entries.Count);
        sb.AppendLine();

        foreach (string code in LocaleCodes())
        {
            if (code == "en") continue;
            var table = LoadTable(code);
            var missing = src.entries.Where(e => !table.ContainsKey(e.key) || string.IsNullOrEmpty(table[e.key])).ToList();
            var stale = table.Keys.Where(k => !src.entries.Any(e => e.key == k)).ToList();
            sb.AppendLine("== " + code + ": " + (src.entries.Count - missing.Count) + "/" + src.entries.Count + " translated, " + missing.Count + " missing, " + stale.Count + " unused entries");
            foreach (var m in missing) sb.AppendLine("   MISSING  " + Short(m.key) + "    [" + m.context + "]");
            foreach (var s in stale) sb.AppendLine("   UNUSED   " + Short(s));
            sb.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(ReportPath);
        return sb.ToString();
    }

    private static string Short(string s)
    {
        s = s.Replace("\n", "⏎");
        return s.Length > 90 ? s.Substring(0, 90) + "…" : s;
    }

    // ------------------------------------------------------------------ CSV

    [MenuItem("Tools/Graveyard/Localization/3. Export CSV (for translators)")]
    public static void ExportCsvMenu()
    {
        var src = LocalizationExtractor.LoadSourceStrings();
        if (src == null)
        {
            EditorUtility.DisplayDialog("Localization", "Run 'Extract Source Strings' first.", "OK");
            return;
        }
        ExportCsv(src);
        EditorUtility.RevealInFinder(CsvPath);
    }

    /// <summary>Writes the translator spreadsheet (key, context, one column per locale) to <see cref="CsvPath"/>.</summary>
    public static void ExportCsv(LocalizationExtractor.SourceStringsFile src)
    {
        var codes = LocaleCodes();
        var tables = codes.ToDictionary(c => c, LoadTable);

        var sb = new StringBuilder();
        sb.Append("key,context");
        foreach (var c in codes) sb.Append(',').Append(c);
        sb.AppendLine();

        foreach (var e in src.entries)
        {
            sb.Append(Csv(e.key)).Append(',').Append(Csv(e.context));
            foreach (var c in codes)
            {
                string v;
                if (c == "en") v = e.text;
                else tables[c].TryGetValue(e.key, out v);
                sb.Append(',').Append(Csv(v ?? ""));
            }
            sb.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CsvPath));
        File.WriteAllText(CsvPath, sb.ToString(), new UTF8Encoding(true)); // BOM so Excel opens it as UTF-8
        AssetDatabase.ImportAsset(CsvPath);
        Debug.Log("LocalizationTools: exported " + src.entries.Count + " rows × " + codes.Count + " locales → " + CsvPath);
    }

    [MenuItem("Tools/Graveyard/Localization/4. Import CSV (into tables)")]
    public static void ImportCsv()
    {
        if (!File.Exists(CsvPath))
        {
            EditorUtility.DisplayDialog("Localization", "No CSV at " + CsvPath, "OK");
            return;
        }
        var rows = ParseCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
        if (rows.Count < 2) return;
        var header = rows[0];
        int keyCol = Array.IndexOf(header, "key");
        if (keyCol < 0) { Debug.LogError("CSV has no 'key' column"); return; }

        int updated = 0;
        for (int col = 0; col < header.Length; col++)
        {
            string code = header[col];
            if (code == "key" || code == "context" || string.IsNullOrEmpty(code)) continue;
            var table = LoadTable(code);
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length <= Math.Max(keyCol, col)) continue;
                string key = row[keyCol];
                string value = row[col];
                if (string.IsNullOrEmpty(key)) continue;
                if (string.IsNullOrEmpty(value)) continue; // never blank an existing translation from an empty cell
                string old;
                if (!table.TryGetValue(key, out old) || old != value) { table[key] = value; updated++; }
            }
            SaveTable(code, table);
        }
        AssetDatabase.Refresh();
        Debug.Log("LocalizationTools: imported CSV, " + updated + " entries updated.");
        EditorUtility.DisplayDialog("Localization", updated + " entries updated from " + CsvPath, "OK");
    }

    private static string Csv(string s)
    {
        if (s == null) return "";
        bool quote = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        return quote ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>RFC-4180 CSV parser (quoted fields, embedded newlines, doubled quotes).</summary>
    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = 0;
        if (text.Length > 0 && text[0] == '﻿') i = 1;
        for (; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            if (c == '"') { inQuotes = true; continue; }
            if (c == ',') { row.Add(field.ToString()); field.Length = 0; continue; }
            if (c == '\r') continue;
            if (c == '\n') { row.Add(field.ToString()); field.Length = 0; rows.Add(row.ToArray()); row.Clear(); continue; }
            field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
#endif
