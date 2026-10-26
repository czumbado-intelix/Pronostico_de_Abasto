using System.Globalization;
using System.Text;

namespace PronosticosAbasto.Core.Tables;

/// <summary>
/// Consulta del buscador de cada modulo, ya interpretada.
/// </summary>
/// <remarks>
/// <para>
/// Antes cada buscador comparaba la frase completa con <c>Contains</c>: "pvc codo"
/// no encontraba "CODO PVC 1/2", "tamano" no encontraba "tamaño", y pegar una
/// lista de articulos copiada de un correo o de Excel no encontraba nada.
/// </para>
/// <para>Reglas, pensadas para el operador de abasto:</para>
/// <list type="bullet">
/// <item>Sin distinguir mayusculas ni tildes: "codo pvc" encuentra "CODO PVC", "tamano" encuentra "TAMAÑO".</item>
/// <item>Varias palabras = todas deben aparecer, en cualquier orden: "pvc codo" encuentra "CODO PVC 1/2".</item>
/// <item>Coma, punto y coma, salto de linea, tabulador o barra separan alternativas: "codo, tee" encuentra los dos.
/// La coma decimal no separa: "2,5" busca "2,5".</item>
/// <item>Una lista de codigos separados por espacios (lo que queda al pegar de un correo) se lee primero como
/// "todas las palabras" —asi "2000017658 100002040" encuentra ese articulo en esa expedicion— y, si ninguna fila
/// los tiene todos, como lista de alternativas: "100002040 100002041" encuentra ambos articulos.</item>
/// <item>Cuando hay varias alternativas, un codigo se compara como palabra completa: "0002001" no encuentra
/// "100002001", ni "2026" encuentra una fecha. Escrito solo, un codigo parcial sigue encontrando todo lo que lo contiene.</item>
/// </list>
/// </remarks>
public sealed class SearchQuery
{
    /// <summary>Largo minimo para que un token se considere codigo (articulo, expedicion, pallet, zona).</summary>
    private const int MinimumCodeLength = 4;

    private readonly SearchAlternative[] _alternatives;

    private SearchQuery(SearchAlternative[] alternatives, SearchQuery? allWordsReading)
    {
        _alternatives = alternatives;
        Alternatives = alternatives.Select(alternative => (IReadOnlyList<string>)alternative.Terms).ToArray();
        AllWordsReading = allWordsReading;
    }

    /// <summary>Consulta vacia: todo coincide.</summary>
    public static SearchQuery Empty { get; } = new([], null);

    /// <summary>Si la consulta no restringe nada.</summary>
    public bool IsEmpty => _alternatives.Length == 0;

    /// <summary>Cuantas alternativas tiene (para mostrar "18 de 20").</summary>
    public int AlternativeCount => _alternatives.Length;

    /// <summary>Terminos normalizados de cada alternativa; todos los de una alternativa deben aparecer juntos.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Alternatives { get; }

    /// <summary>
    /// Para una lista de codigos separados solo por espacios: la misma consulta
    /// leida como "todas las palabras". <see cref="SearchIndex{TRow}"/> la usa si
    /// alguna fila la cumple; si ninguna, se queda con la lista.
    /// </summary>
    public SearchQuery? AllWordsReading { get; }

    /// <summary>Si la alternativa se compara como palabra completa (codigo dentro de una lista).</summary>
    public bool IsWholeToken(int alternativeIndex) => _alternatives[alternativeIndex].WholeToken;

    /// <summary>Texto para mostrarle al operador (por ejemplo, en "sin resultado").</summary>
    public string DisplayText(int alternativeIndex) => string.Join(' ', _alternatives[alternativeIndex].Terms);

    public static SearchQuery Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        var groups = SplitAlternatives(text)
            .Select(group => Normalize(group).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray())
            .Where(terms => terms.Length > 0)
            .ToList();

        // Una sola linea de codigos separados por espacios es ambigua: una
        // expedicion y un articulo (todas) o una lista pegada (cualquiera).
        SearchQuery? allWordsReading = null;
        var expanded = new List<string[]>();
        foreach (var terms in groups)
        {
            if (terms.Length > 1 && terms.All(IsCodeLike))
            {
                if (groups.Count == 1)
                {
                    allWordsReading = new SearchQuery([new SearchAlternative(terms, WholeToken: false)], null);
                }

                expanded.AddRange(terms.Select(term => new[] { term }));
            }
            else
            {
                expanded.Add(terms);
            }
        }

        var distinct = expanded
            .DistinctBy(terms => string.Join(' ', terms), StringComparer.Ordinal)
            .ToList();

        if (distinct.Count > 1)
        {
            // Al pegar una fila de Excel ("100002040 <tab> SELLADOR <tab> 25") la
            // cantidad suelta encontraria casi todas las filas.
            var withoutShortNumbers = distinct.Where(terms => !IsShortNumber(terms)).ToList();
            if (withoutShortNumbers.Count > 0)
            {
                distinct = withoutShortNumbers;
            }
        }

        if (distinct.Count == 0)
        {
            return Empty;
        }

        // Dentro de una lista, un codigo es un codigo entero, no un pedazo de otro.
        var isList = distinct.Count > 1;
        var alternatives = distinct
            .Select(terms => new SearchAlternative(terms, WholeToken: isList && terms.Length == 1 && IsCodeLike(terms[0])))
            .ToArray();
        return new SearchQuery(alternatives, isList ? allWordsReading : null);
    }

    /// <summary>
    /// Decide si un texto ya normalizado con <see cref="Normalize"/> coincide.
    /// </summary>
    /// <remarks>
    /// Recibe el texto normalizado y no el crudo a proposito: normalizar es lo
    /// caro, y el texto de cada fila no cambia mientras el operador escribe, asi
    /// que <see cref="SearchIndex{TRow}"/> lo calcula una sola vez por fila.
    /// </remarks>
    public bool Matches(string normalizedHaystack) => Matches(new SearchKey(normalizedHaystack));

    public bool Matches(SearchKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (IsEmpty)
        {
            return true;
        }

        for (var index = 0; index < _alternatives.Length; index++)
        {
            if (MatchesAlternative(index, key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Si una alternativa concreta coincide con la fila.</summary>
    public bool MatchesAlternative(int alternativeIndex, SearchKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var alternative = _alternatives[alternativeIndex];
        if (alternative.WholeToken)
        {
            return key.Tokens.Contains(alternative.Terms[0]);
        }

        foreach (var term in alternative.Terms)
        {
            if (!key.Text.Contains(term, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Atajo para textos sueltos (sin indice). No normaliza nada si la consulta esta vacia.</summary>
    public bool MatchesRaw(params string?[] fields) => IsEmpty || Matches(Normalize(string.Join(' ', fields)));

    /// <summary>
    /// Mayusculas, sin tildes ni dieresis (ñ queda como n), sin caracteres
    /// invisibles (espacios de ancho cero que traen Teams u Outlook) y con
    /// cualquier espacio en blanco colapsado a un solo espacio.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format or UnicodeCategory.Control)
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Separa alternativas. La coma entre digitos solo separa si de ambos lados
    /// hay un codigo ("100002040,100002041"); si no, es coma decimal o de miles
    /// ("2,5 MM", "1,250") y queda dentro del termino.
    /// </summary>
    private static IEnumerable<string> SplitAlternatives(string text)
    {
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var separates = character switch
            {
                ';' or '\r' or '\n' or '\t' or '|' => true,
                ',' => !IsDecimalComma(text, index),
                _ => false,
            };

            if (separates)
            {
                yield return text[start..index];
                start = index + 1;
            }
        }

        yield return text[start..];
    }

    private static bool IsDecimalComma(string text, int commaIndex)
    {
        var left = 0;
        for (var index = commaIndex - 1; index >= 0 && char.IsAsciiDigit(text[index]); index--)
        {
            left++;
        }

        var right = 0;
        for (var index = commaIndex + 1; index < text.Length && char.IsAsciiDigit(text[index]); index++)
        {
            right++;
        }

        return left > 0 && right > 0 && (left < MinimumCodeLength || right < MinimumCodeLength);
    }

    /// <summary>Codigo: al menos 4 caracteres, solo letras, digitos o guion, y al menos un digito (100002040, 06019J85E1, ZA23).</summary>
    private static bool IsCodeLike(string term) =>
        term.Length >= MinimumCodeLength &&
        term.Any(char.IsAsciiDigit) &&
        term.All(character => char.IsLetterOrDigit(character) || character == '-');

    private static bool IsShortNumber(string[] terms) =>
        terms.Length == 1 && terms[0].Length < MinimumCodeLength && terms[0].All(char.IsAsciiDigit);

    private sealed record SearchAlternative(string[] Terms, bool WholeToken);
}

/// <summary>
/// Texto de busqueda de una fila, ya normalizado, con sus palabras completas a
/// mano para comparar codigos enteros.
/// </summary>
public sealed class SearchKey
{
    private HashSet<string>? _tokens;

    public SearchKey(string normalizedText)
    {
        Text = normalizedText ?? string.Empty;
    }

    public string Text { get; }

    /// <summary>Palabras completas: secuencias de letras, digitos o guiones ("2026-09-01" es una sola).</summary>
    public IReadOnlySet<string> Tokens => _tokens ??= Tokenize(Text);

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var start = -1;
        for (var index = 0; index <= text.Length; index++)
        {
            var isTokenChar = index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '-');
            if (isTokenChar)
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start >= 0)
            {
                var token = text[start..index].Trim('-');
                if (token.Length > 0)
                {
                    tokens.Add(token);
                }

                start = -1;
            }
        }

        return tokens;
    }
}
