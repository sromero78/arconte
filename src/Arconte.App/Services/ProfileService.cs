using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Arconte.App.Models;

namespace Arconte.App.Services;

public sealed class ProfileService
{
    private static readonly Regex CodigoRegex = new(@"^[A-Z]{3}$", RegexOptions.Compiled);
    private static readonly string[] OrdenRequerido = { "FECHA", "CODIGO", "MATERIA", "IDENTIFICADOR" };

    private readonly string _ruta;

    public List<Profile> Perfiles { get; private set; } = new();
    public string PerfilActivoId { get; private set; } = "";
    public string? UltimoError { get; private set; }

    public ProfileService(string? ruta = null)
    {
        _ruta = ruta ?? Path.Combine(AppContext.BaseDirectory, "perfiles.json");
    }

    public Profile? PerfilActivo => Perfiles.FirstOrDefault(p => p.Id == PerfilActivoId) ?? Perfiles.FirstOrDefault();

    public string LeerConfiguracion() => File.ReadAllText(_ruta);

    public List<Profile> CrearCopiaEditable()
        => Perfiles.Select(CopiarPerfil).ToList();

    private static Profile CopiarPerfil(Profile p) => new()
    {
        Id = p.Id,
        Nombre = p.Nombre,
        FormatoFecha = p.FormatoFecha,
        Separador = p.Separador,
        LongitudCodigo = p.LongitudCodigo,
        ReglaCaja = p.ReglaCaja,
        ExtensionMayusculas = p.ExtensionMayusculas,
        OrdenEstructura = new List<string>(p.OrdenEstructura),
        Catalogo = p.Catalogo.Select(e => new CatalogoEntry(e.Codigo, e.Categoria, e.Descripcion)).ToList(),
    };

    public static string SerializarConfiguracion(IEnumerable<Profile> perfiles, string perfilActivoId)
    {
        var perfilesNodo = new JsonObject();
        foreach (var p in perfiles)
        {
            var catalogo = new JsonArray();
            foreach (var e in p.Catalogo)
            {
                catalogo.Add(new JsonObject
                {
                    ["codigo"] = e.Codigo,
                    ["familia"] = e.Categoria,
                    ["concepto"] = e.Descripcion,
                });
            }
            var orden = new JsonArray();
            foreach (var o in p.OrdenEstructura)
                orden.Add(o);
            perfilesNodo[p.Id] = new JsonObject
            {
                ["nombre_perfil"] = p.Nombre,
                ["formato_fecha"] = p.FormatoFecha,
                ["separador"] = p.Separador,
                ["longitud_codigo"] = p.LongitudCodigo,
                ["regla_caja"] = p.ReglaCaja,
                ["extension_mayusculas"] = p.ExtensionMayusculas,
                ["orden_estructura"] = orden,
                ["catalogo_codigos"] = catalogo,
            };
        }
        var raiz = new JsonObject
        {
            ["perfil_activo"] = perfilActivoId,
            ["perfiles"] = perfilesNodo,
        };
        return raiz.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });
    }

    public bool GuardarPerfiles(IEnumerable<Profile> perfiles, string perfilActivoId, out string error)
    {
        error = "";
        try
        {
            var copia = perfiles.Select(CopiarPerfil).ToList();
            var json = SerializarConfiguracion(copia, perfilActivoId);
            return GuardarConfiguracion(json, out error);
        }
        catch (Exception ex)
        {
            error = "No se pudieron guardar los perfiles: " + ex.Message;
            return false;
        }
    }

    public void Cargar()
    {
        UltimoError = null;
        var json = LeerConfiguracion();
        if (!Validar(json, out var perfiles, out var activo, out var error))
            throw new InvalidDataException(error);
        Perfiles = perfiles;
        PerfilActivoId = activo;
    }

    public bool GuardarConfiguracion(string json, out string error)
    {
        error = "";
        UltimoError = null;
        if (!Validar(json, out var perfiles, out var activo, out error))
            return false;

        JsonNode raiz;
        try
        {
            raiz = JsonNode.Parse(json)!;
        }
        catch (JsonException ex)
        {
            error = "JSON no válido: " + ex.Message;
            return false;
        }

        var tmp = _ruta + ".tmp";
        try
        {
            File.WriteAllText(tmp, raiz.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }));
            File.Move(tmp, _ruta, true);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            error = $"No se pudo guardar perfiles.json: {ex.Message}. Compruebe permisos de escritura en la carpeta de la aplicación.";
            UltimoError = error;
            return false;
        }

        Perfiles = perfiles;
        PerfilActivoId = activo;
        return true;
    }

    private static bool Validar(string json, out List<Profile> perfiles, out string activo, out string error)
    {
        perfiles = new List<Profile>();
        activo = "";
        error = "";

        JsonObject? raiz;
        try
        {
            raiz = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException ex)
        {
            error = "JSON no válido: " + ex.Message;
            return false;
        }
        if (raiz == null)
        {
            error = "La configuración debe ser un objeto JSON";
            return false;
        }

        if (raiz["perfil_activo"] is not JsonValue vActivo ||
            !vActivo.TryGetValue<string>(out var activoStr) || string.IsNullOrWhiteSpace(activoStr))
        {
            error = "Falta la clave 'perfil_activo' o no es un texto válido";
            return false;
        }

        if (raiz["perfiles"] is not JsonObject perfilesNodo || perfilesNodo.Count == 0)
        {
            error = "Falta la clave 'perfiles' o está vacía";
            return false;
        }
        if (!perfilesNodo.ContainsKey(activoStr))
        {
            error = $"El perfil activo '{activoStr}' no existe en 'perfiles'";
            return false;
        }

        foreach (var (id, nodo) in perfilesNodo)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "Hay un perfil con identificador vacío";
                return false;
            }
            if (nodo is not JsonObject p)
            {
                error = $"El perfil '{id}' debe ser un objeto";
                return false;
            }

            var perfil = new Profile { Id = id };

            if (!Texto(p, "nombre_perfil", out var nombre) || string.IsNullOrWhiteSpace(nombre))
            { error = $"Perfil '{id}': 'nombre_perfil' es obligatorio"; return false; }
            perfil.Nombre = nombre;

            if (!Texto(p, "formato_fecha", out var formato) || formato != "YYYYMMDD")
            { error = $"Perfil '{id}': 'formato_fecha' debe ser exactamente \"YYYYMMDD\""; return false; }
            perfil.FormatoFecha = formato;

            if (!Texto(p, "separador", out var separador) || separador.Length == 0)
            { error = $"Perfil '{id}': 'separador' es obligatorio"; return false; }
            perfil.Separador = separador;

            if (!Entero(p, "longitud_codigo", out var longitud) || longitud != 3)
            { error = $"Perfil '{id}': 'longitud_codigo' debe ser exactamente 3"; return false; }
            perfil.LongitudCodigo = longitud;

            if (!Texto(p, "regla_caja", out var regla) || string.IsNullOrWhiteSpace(regla))
            { error = $"Perfil '{id}': 'regla_caja' es obligatoria"; return false; }
            perfil.ReglaCaja = regla;

            if (p["extension_mayusculas"] is not JsonValue vExt || !vExt.TryGetValue<bool>(out var extMay))
            { error = $"Perfil '{id}': 'extension_mayusculas' debe ser booleano"; return false; }
            perfil.ExtensionMayusculas = extMay;

            if (p["orden_estructura"] is not JsonArray orden)
            { error = $"Perfil '{id}': falta 'orden_estructura'"; return false; }
            var claves = new List<string>();
            foreach (var o in orden)
            {
                if (o is not JsonValue vo || !vo.TryGetValue<string>(out var s))
                { error = $"Perfil '{id}': 'orden_estructura' debe contener solo textos"; return false; }
                claves.Add(s);
            }
            if (claves.Count != OrdenRequerido.Length || claves.Distinct().Count() != claves.Count ||
                OrdenRequerido.Any(k => !claves.Contains(k, StringComparer.Ordinal)))
            {
                error = $"Perfil '{id}': 'orden_estructura' debe contener exactamente una vez cada {string.Join(", ", OrdenRequerido)}";
                return false;
            }
            perfil.OrdenEstructura = claves;

            if (p["catalogo_codigos"] is not JsonArray catalogo || catalogo.Count == 0)
            { error = $"Perfil '{id}': 'catalogo_codigos' debe ser una lista no vacía"; return false; }
            var codigos = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in catalogo)
            {
                if (c is not JsonObject e)
                { error = $"Perfil '{id}': cada entrada del catálogo debe ser un objeto"; return false; }
                if (!Texto(e, "codigo", out var codigo) || !CodigoRegex.IsMatch(codigo))
                { error = $"Perfil '{id}': cada 'codigo' debe ser 3 letras mayúsculas (recibido: '{codigo}')"; return false; }
                if (!codigos.Add(codigo))
                { error = $"Perfil '{id}': código duplicado '{codigo}'"; return false; }
                if (!Texto(e, "familia", out var familia) || string.IsNullOrWhiteSpace(familia))
                { error = $"Perfil '{id}': 'familia' es obligatoria en el código '{codigo}'"; return false; }
                if (!Texto(e, "concepto", out var concepto) || string.IsNullOrWhiteSpace(concepto))
                { error = $"Perfil '{id}': 'concepto' es obligatorio en el código '{codigo}'"; return false; }
                perfil.Catalogo.Add(new CatalogoEntry(codigo, familia, concepto));
            }
            perfiles.Add(perfil);
        }

        activo = activoStr;
        return true;
    }

    private static bool Texto(JsonObject obj, string clave, out string valor)
    {
        valor = "";
        if (obj[clave] is JsonValue v && v.TryGetValue<string>(out var s))
        {
            valor = s;
            return true;
        }
        return false;
    }

    private static bool Entero(JsonObject obj, string clave, out int valor)
    {
        valor = 0;
        return obj[clave] is JsonValue v && v.TryGetValue(out valor);
    }

    public bool GuardarPerfilActivo(string id)
    {
        UltimoError = null;
        if (!Perfiles.Any(p => p.Id == id))
        {
            UltimoError = $"El perfil '{id}' no existe en la configuración.";
            return false;
        }
        PerfilActivoId = id;
        try
        {
            var json = File.ReadAllText(_ruta);
            var raiz = JsonNode.Parse(json)!.AsObject();
            raiz["perfil_activo"] = id;
            var tmp = _ruta + ".tmp";
            File.WriteAllText(tmp, raiz.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }));
            File.Move(tmp, _ruta, true);
            return true;
        }
        catch (Exception ex)
        {
            UltimoError = $"No se pudo guardar perfiles.json: {ex.Message}. Compruebe permisos de escritura en la carpeta de la aplicación.";
            return false;
        }
    }
}

