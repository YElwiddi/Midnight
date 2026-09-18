#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Minimal JSON reader used by the editor localization tools to walk compiled Ink stories
/// (JsonUtility cannot deserialize Ink's heterogeneous arrays). Objects become
/// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double, plus string/bool/null.
/// </summary>
public static class MiniJson
{
    public static object Deserialize(string json)
    {
        if (json == null) return null;
        int i = 0;
        SkipBom(json, ref i);
        return ParseValue(json, ref i);
    }

    private static void SkipBom(string s, ref int i)
    {
        if (i < s.Length && s[i] == '﻿') i++;
    }

    private static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static object ParseValue(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
        char c = s[i];
        switch (c)
        {
            case '{': return ParseObject(s, ref i);
            case '[': return ParseArray(s, ref i);
            case '"': return ParseString(s, ref i);
            case 't': Expect(s, ref i, "true"); return true;
            case 'f': Expect(s, ref i, "false"); return false;
            case 'n': Expect(s, ref i, "null"); return null;
            default: return ParseNumber(s, ref i);
        }
    }

    private static void Expect(string s, ref int i, string word)
    {
        if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException("Expected " + word + " at " + i);
        i += word.Length;
    }

    private static Dictionary<string, object> ParseObject(string s, ref int i)
    {
        var d = new Dictionary<string, object>();
        i++; // {
        SkipWs(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return d; }
        while (i < s.Length)
        {
            SkipWs(s, ref i);
            string key = ParseString(s, ref i);
            SkipWs(s, ref i);
            if (s[i] != ':') throw new FormatException("Expected ':' at " + i);
            i++;
            d[key] = ParseValue(s, ref i);
            SkipWs(s, ref i);
            if (s[i] == ',') { i++; continue; }
            if (s[i] == '}') { i++; return d; }
            throw new FormatException("Expected ',' or '}' at " + i);
        }
        throw new FormatException("Unterminated object");
    }

    private static List<object> ParseArray(string s, ref int i)
    {
        var l = new List<object>();
        i++; // [
        SkipWs(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return l; }
        while (i < s.Length)
        {
            l.Add(ParseValue(s, ref i));
            SkipWs(s, ref i);
            if (s[i] == ',') { i++; continue; }
            if (s[i] == ']') { i++; return l; }
            throw new FormatException("Expected ',' or ']' at " + i);
        }
        throw new FormatException("Unterminated array");
    }

    private static string ParseString(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("Expected string at " + i);
        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) break;
            char e = s[i++];
            switch (e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                    i += 4;
                    break;
                default: sb.Append(e); break;
            }
        }
        throw new FormatException("Unterminated string");
    }

    private static object ParseNumber(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        string num = s.Substring(start, i - start);
        double d;
        if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            throw new FormatException("Bad number '" + num + "' at " + start);
        return d;
    }
}
#endif
