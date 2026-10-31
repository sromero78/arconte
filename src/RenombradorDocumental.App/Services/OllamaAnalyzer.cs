using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RenombradorDocumental.App.Models;

namespace RenombradorDocumental.App.Services;

public sealed class OllamaAnalyzer : ILocalDocumentAnalyzer
{
    private const int MaxCaracteresTexto = 12000;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _modelo;

    public OllamaAnalyzer(string endpoint = "http://127.0.0.1:11434", string modelo = "qwen2.5:3b", HttpClient? http = null)
    {
        if (!IsLoopbackEndpoint(endpoint))
            throw new ArgumentException($"El endpoint de Ollama debe ser loopback (localhost/127.0.0.1/::1): {endpoint}");
        _endpoint = new Uri(endpoint.TrimEnd('/') + "/api/generate");
        _modelo = modelo;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public static bool IsLoopbackEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
        return uri.IsLoopback;
    }

    public async Task<DocumentProposal> AnalyzeAsync(string extractedText, string folderName, string originalName, Profile profile, CancellationToken ct)
    {
        var texto = extractedText.Length > MaxCaracteresTexto
            ? extractedText[..MaxCaracteresTexto]
            : extractedText;

        var catalogo = string.Join(", ", profile.Catalogo.Select(c => c.Codigo));
        var systemPrompt =
            "Eres un asistente que clasifica documentos en español. Devuelve SOLO un objeto JSON " +
            "con las claves \"fecha\", \"codigo\", \"materia\" e \"identificador\". " +
            "\"fecha\" debe ser la fecha principal del documento en formato YYYYMMDD (8 dígitos); " +
            "si no hay una fecha fiable en el texto, devuelve \"fecha\" como cadena vacía, no inventes fechas. " +
            "\"codigo\" debe ser exactamente uno de estos códigos de catálogo: " + catalogo + ". " +
            "\"materia\" e \"identificador\" deben ser textos cortos sin caracteres inválidos de nombre de archivo.";

        var prompt =
            $"Carpeta contenedora: {folderName}\nNombre original del archivo: {originalName}\n" +
            $"Texto extraído del documento:\n{texto}\n\nDevuelve solo el JSON.";

        var payload = new
        {
            model = _modelo,
            prompt = systemPrompt + "\n\n" + prompt,
            stream = false,
            format = "json",
        };

        using var contenido = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var respuesta = await _http.PostAsync(_endpoint, contenido, ct).ConfigureAwait(false);
        respuesta.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var cuerpo = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() ?? "" : "";

        return ParsearPropuesta(cuerpo, profile);
    }

    public static DocumentProposal ParsearPropuesta(string cuerpo, Profile profile)
    {
        var json = cuerpo.Trim();
        var fence = Regex.Match(json, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline);
        if (fence.Success) json = fence.Groups[1].Value;
        else
        {
            var llave = Regex.Match(json, @"\{.*\}", RegexOptions.Singleline);
            if (llave.Success) json = llave.Value;
        }

        string fecha = "", codigo = "", materia = "", identificador = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            fecha = GetStr(raiz, "fecha");
            codigo = GetStr(raiz, "codigo");
            materia = GetStr(raiz, "materia");
            identificador = GetStr(raiz, "identificador");
        }
        catch (JsonException) { }

        return new DocumentProposal(
            NameBuilder.NormalizeFecha(fecha),
            NameBuilder.NormalizeCodigo(codigo, profile),
            NameBuilder.NormalizeMateria(materia),
            NameBuilder.NormalizeIdentificador(identificador));
    }

    private static string GetStr(JsonElement raiz, string clave)
        => raiz.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
