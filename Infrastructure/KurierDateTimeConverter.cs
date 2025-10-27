using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace BennerKurierWorker.Infrastructure;

/// <summary>
/// Conversor customizado para DateTime que suporta múltiplos formatos da API Kurier
/// </summary>
public class KurierDateTimeConverter : JsonConverter<DateTime>
{
    private readonly string[] _dateFormats = 
    {
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "dd-MM-yyyy HH:mm:ss",
        "dd-MM-yyyy"
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        
        if (string.IsNullOrEmpty(dateString))
        {
            return DateTime.MinValue;
        }

        // Tentar converter com os formatos conhecidos
        foreach (var format in _dateFormats)
        {
            if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        // Tentar parsing padrão se nenhum formato específico funcionou
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate;
        }

        // Se tudo falhar, log e retorna data mínima
        Console.WriteLine($"⚠️ Não foi possível converter a data: '{dateString}'. Usando DateTime.MinValue");
        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss"));
    }
}

/// <summary>
/// Conversor customizado para DateTime? (nullable) que suporta múltiplos formatos da API Kurier
/// </summary>
public class KurierNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private readonly KurierDateTimeConverter _converter = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        try
        {
            return _converter.Read(ref reader, typeof(DateTime), options);
        }
        catch
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            _converter.Write(writer, value.Value, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}