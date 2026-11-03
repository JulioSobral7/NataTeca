using System.Globalization;
using System.Text;

namespace API.Services;

/// <summary>Comparação de textos tolerante a ruído de OCR.</summary>
public static class TextoUtil
{
    /// <summary>Minúsculas, sem acentos e sem espaços/pontuação (tolera palavras coladas pelo OCR).</summary>
    public static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Coeficiente de Dice sobre bigramas de caracteres (0 a 1).</summary>
    public static double Similaridade(string a, string b)
    {
        if (a.Length < 2 || b.Length < 2) return 0;
        var comuns = BigramasComuns(a, b);
        return 2.0 * comuns / ((a.Length - 1) + (b.Length - 1));
    }

    /// <summary>
    /// Fração dos bigramas de <paramref name="parte"/> que aparecem em <paramref name="todo"/> (0 a 1).
    /// Serve para conferir se um título/autor devolvido pelo LLM saiu mesmo do texto lido.
    /// </summary>
    public static double Contencao(string parte, string todo)
    {
        if (parte.Length == 0) return 0;
        if (parte.Length == 1) return todo.Contains(parte) ? 1 : 0;
        if (todo.Length < 2) return 0;
        return (double)BigramasComuns(parte, todo) / (parte.Length - 1);
    }

    private static readonly HashSet<string> PalavrasMinusculas = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "o", "as", "os", "e", "de", "da", "do", "das", "dos", "em", "no", "na", "nos", "nas",
        "um", "uma", "com", "por", "para", "of", "the", "and", "in", "on", "an", "to", "for", "at", "y", "la", "el"
    };

    /// <summary>
    /// "THE LORD OF THE RINGS" → "The Lord of the Rings"; "J.R.R. TOLKIEN" → "J.R.R. Tolkien".
    /// Só mexe em textos todos em maiúsculas; o resto volta como veio.
    /// </summary>
    public static string CapitalizarSeMaiusculas(string texto)
    {
        if (texto.Count(char.IsLetter) < 2 || texto.Any(char.IsLower))
            return texto;

        var palavras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < palavras.Length; i++)
        {
            var p = palavras[i];
            if (EhIniciais(p))
                continue;
            var minuscula = p.ToLower(CultureInfo.GetCultureInfo("pt-BR"));
            palavras[i] = i > 0 && PalavrasMinusculas.Contains(minuscula)
                ? minuscula
                : char.ToUpper(minuscula[0], CultureInfo.GetCultureInfo("pt-BR")) + minuscula[1..];
        }
        return string.Join(' ', palavras);
    }

    /// <summary>"J.R.R.", "J.K." — letras isoladas separadas por ponto.</summary>
    private static bool EhIniciais(string palavra) =>
        palavra.Contains('.') && palavra.Split('.', StringSplitOptions.RemoveEmptyEntries).All(s => s.Length == 1);

    private static int BigramasComuns(string a, string b)
    {
        var bigramasB = new Dictionary<string, int>();
        for (var i = 0; i < b.Length - 1; i++)
        {
            var bg = b.Substring(i, 2);
            bigramasB[bg] = bigramasB.GetValueOrDefault(bg) + 1;
        }

        var comuns = 0;
        for (var i = 0; i < a.Length - 1; i++)
        {
            var bg = a.Substring(i, 2);
            if (bigramasB.TryGetValue(bg, out var n) && n > 0)
            {
                comuns++;
                bigramasB[bg] = n - 1;
            }
        }
        return comuns;
    }
}
