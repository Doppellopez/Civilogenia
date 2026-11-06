using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ---------------------------------------------------------------------------------------------------------------------
// Idioma del juego (español / inglés). Todo el juego está escrito en español; al pintar un texto en pantalla (LocGUI) o al
// añadirlo al registro se traduce con la tabla de LocData. Lo que no esté en la tabla se ve en español, sin fallar.
// El idioma elegido se guarda entre partidas.
// ---------------------------------------------------------------------------------------------------------------------
public static class Loc
{
    const string PrefKey = "civ_language";   // 0 = español, 1 = inglés, 2 = alemán

    static bool loaded;
    static int language;                             // 0 = español, 1 = inglés, 2 = alemán
    static Dictionary<string, string> exactOnly;     // "=texto": solo si el texto entero coincide
    static Dictionary<string, string> whole;         // texto entero de cualquier entrada
    static string[] keys;                            // entradas de trozo, de más larga a más corta
    static string[] values;
    static bool[] isWord;
    static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    static readonly Regex LeadingLas = new Regex(@"^Las (\d+) ", RegexOptions.Compiled);

    public static bool English
    {
        get { EnsureLoaded(); return language == 1; }
    }

    public static bool German
    {
        get { EnsureLoaded(); return language == 2; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        cache.Clear();
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        try { language = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2); }
        catch (Exception) { language = 0; }

        exactOnly = new Dictionary<string, string>();
        whole = new Dictionary<string, string>();
        List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();

        // Tabla del idioma elegido (en español no hace falta ninguna: los textos ya están en español)
        List<string[]> tables = new List<string[]>();
        if (language == 1) tables.Add(LocData.Pairs);
        else if (language == 2) { tables.Add(LocDataDe1.Pairs); tables.Add(LocDataDe2.Pairs); tables.Add(LocDataDe3.Pairs); }

        foreach (string[] pairs in tables)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                string es = pairs[i], tr = pairs[i + 1];
                if (es.StartsWith("=")) { exactOnly[es.Substring(1)] = tr; continue; }
                whole[es] = tr;
                if (es != tr) list.Add(new KeyValuePair<string, string>(es, tr));
            }
        }

        // Las entradas más largas primero: "Gran Biblioteca de Alejandría" antes que "Biblioteca"
        list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        keys = new string[list.Count];
        values = new string[list.Count];
        isWord = new bool[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            keys[i] = list[i].Key;
            values[i] = list[i].Value;
            isWord[i] = IsWord(list[i].Key);
        }
    }

    static bool IsWord(string s)
    {
        foreach (char c in s) if (!char.IsLetter(c)) return false;
        return s.Length > 0;
    }

    public static void SetEnglish(bool value)
    {
        SetLanguage(value ? 1 : 0);
    }

    /// <summary>0 = español, 1 = inglés, 2 = alemán. Se guarda entre partidas y recarga la tabla de traducciones.</summary>
    public static void SetLanguage(int value)
    {
        EnsureLoaded();
        value = Mathf.Clamp(value, 0, 2);
        if (language == value) return;

        try { PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); }
        catch (Exception) { }

        loaded = false;   // EnsureLoaded vuelve a leer el idioma y construye la tabla nueva
        cache.Clear();
        language = value;
        EnsureLoaded();
        language = value;   // por si no se pudo guardar la preferencia
    }

    /// <summary>Traduce un texto al idioma elegido (en español lo devuelve tal cual).</summary>
    public static string T(string text)
    {
        EnsureLoaded();
        if (language == 0 || string.IsNullOrEmpty(text)) return text;

        string result;
        if (cache.TryGetValue(text, out result)) return result;

        result = Translate(text);
        if (cache.Count > 6000) cache.Clear();   // los textos con números cambian sin parar: se evita que la caché crezca sin fin
        cache[text] = result;
        return result;
    }

    public static string[] T(string[] texts)
    {
        EnsureLoaded();
        if (language == 0 || texts == null) return texts;

        string[] translated = new string[texts.Length];
        for (int i = 0; i < texts.Length; i++) translated[i] = T(texts[i]);
        return translated;
    }

    static string Translate(string text)
    {
        string result;
        if (exactOnly.TryGetValue(text, out result)) return result;
        if (whole.TryGetValue(text, out result)) return result;

        string s = text;
        for (int i = 0; i < keys.Length; i++)
        {
            if (s.IndexOf(keys[i], StringComparison.Ordinal) < 0) continue;
            s = isWord[i] ? ReplaceWord(s, keys[i], values[i]) : s.Replace(keys[i], values[i]);
        }

        return LeadingLas.Replace(s, language == 2 ? "Die $1 " : "The $1 ");
    }

    // Sustituye solo palabras enteras ("Caza" sí, "Cazador" no)
    static string ReplaceWord(string s, string key, string value)
    {
        int i = s.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return s;

        StringBuilder sb = null;
        int last = 0;
        while (i >= 0)
        {
            int end = i + key.Length;
            bool leftOk = i == 0 || !char.IsLetterOrDigit(s[i - 1]);
            bool rightOk = end >= s.Length || !char.IsLetterOrDigit(s[end]);
            if (leftOk && rightOk)
            {
                if (sb == null) sb = new StringBuilder(s.Length + 16);
                sb.Append(s, last, i - last).Append(value);
                last = end;
            }

            i = s.IndexOf(key, end, StringComparison.Ordinal);
        }

        if (sb == null) return s;
        sb.Append(s, last, s.Length - last);
        return sb.ToString();
    }
}

/// <summary>Las llamadas de GUI y GUILayout con el texto ya traducido al idioma elegido.</summary>
public static class LocGUI
{
    public static void Label(Rect r, string text) { GUI.Label(r, Loc.T(text)); }
    public static void Label(Rect r, string text, GUIStyle style) { GUI.Label(r, Loc.T(text), style); }
    public static void Label(Rect r, Texture image) { GUI.Label(r, image); }
    public static void Label(Rect r, Texture image, GUIStyle style) { GUI.Label(r, image, style); }
    public static void Label(Rect r, GUIContent content) { GUI.Label(r, content); }
    public static void Label(Rect r, GUIContent content, GUIStyle style) { GUI.Label(r, content, style); }

    public static bool Button(Rect r, string text) { return GUI.Button(r, Loc.T(text)); }
    public static bool Button(Rect r, string text, GUIStyle style) { return GUI.Button(r, Loc.T(text), style); }
    public static bool Button(Rect r, Texture image) { return GUI.Button(r, image); }
    public static bool Button(Rect r, Texture image, GUIStyle style) { return GUI.Button(r, image, style); }
    public static bool Button(Rect r, GUIContent content) { return GUI.Button(r, content); }
    public static bool Button(Rect r, GUIContent content, GUIStyle style) { return GUI.Button(r, content, style); }

    public static int Toolbar(Rect r, int selected, string[] texts, GUIStyle style) { return GUI.Toolbar(r, selected, Loc.T(texts), style); }
    public static int Toolbar(Rect r, int selected, string[] texts) { return GUI.Toolbar(r, selected, Loc.T(texts)); }
    public static int SelectionGrid(Rect r, int selected, string[] texts, int columns, GUIStyle style) { return GUI.SelectionGrid(r, selected, Loc.T(texts), columns, style); }
    public static int SelectionGrid(Rect r, int selected, string[] texts, int columns) { return GUI.SelectionGrid(r, selected, Loc.T(texts), columns); }

    public static void LayoutLabel(string text, params GUILayoutOption[] options) { GUILayout.Label(Loc.T(text), options); }
    public static void LayoutLabel(string text, GUIStyle style, params GUILayoutOption[] options) { GUILayout.Label(Loc.T(text), style, options); }
    public static void LayoutLabel(Texture image, params GUILayoutOption[] options) { GUILayout.Label(image, options); }
    public static void LayoutLabel(GUIContent content, params GUILayoutOption[] options) { GUILayout.Label(content, options); }
    public static void LayoutLabel(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { GUILayout.Label(content, style, options); }

    public static bool LayoutButton(string text, params GUILayoutOption[] options) { return GUILayout.Button(Loc.T(text), options); }
    public static bool LayoutButton(string text, GUIStyle style, params GUILayoutOption[] options) { return GUILayout.Button(Loc.T(text), style, options); }
    public static bool LayoutButton(Texture image, params GUILayoutOption[] options) { return GUILayout.Button(image, options); }
    public static bool LayoutButton(GUIContent content, params GUILayoutOption[] options) { return GUILayout.Button(content, options); }
    public static bool LayoutButton(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { return GUILayout.Button(content, style, options); }

    public static int LayoutToolbar(int selected, string[] texts, params GUILayoutOption[] options) { return GUILayout.Toolbar(selected, Loc.T(texts), options); }
    public static int LayoutToolbar(int selected, string[] texts, GUIStyle style, params GUILayoutOption[] options) { return GUILayout.Toolbar(selected, Loc.T(texts), style, options); }
}
