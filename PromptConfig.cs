
using System.Text.Json.Serialization;

public class PromptConfig
{
    [JsonPropertyName("nomeController")]
    public string NomeController { get; set; } = string.Empty;

    // NOVO CAMPO: tipoEndpoint (antes de nomeEndpoint no JSON)
    [JsonPropertyName("tipoEndpoint")]
    public string TipoEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("nomeEndpoint")]
    public string NomeEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("nomeMetodo")]
    public string NomeMetodo { get; set; } = string.Empty;

    [JsonPropertyName("colunas")]
    public string Colunas { get; set; } = string.Empty;

    [JsonPropertyName("pastaDestino")]
    public string PastaDestino { get; set; } = string.Empty;
}
