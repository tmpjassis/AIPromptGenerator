using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

// ---------------------------
// Modelos auxiliares
// ---------------------------

public class TemplateRoot
{
    // steps modeladas como dicionário dinâmico para aceitar campos diversos
    public List<Dictionary<string, object>> steps { get; set; } = new();
}

// ---------------------------
// Funções auxiliares
// ---------------------------
static class PromptHelper
{
    public static string NormalizeHttpVerb(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Get";
        var s = input.Trim();

        // Normaliza valores comuns (GET/POST/PUT/DELETE/PATCH/HEAD)
        if (string.Equals(s, "GET", StringComparison.OrdinalIgnoreCase)) return "Get";
        if (string.Equals(s, "POST", StringComparison.OrdinalIgnoreCase)) return "Post";
        if (string.Equals(s, "PUT", StringComparison.OrdinalIgnoreCase)) return "Put";
        if (string.Equals(s, "DELETE", StringComparison.OrdinalIgnoreCase)) return "Delete";
        if (string.Equals(s, "PATCH", StringComparison.OrdinalIgnoreCase)) return "Patch";
        if (string.Equals(s, "HEAD", StringComparison.OrdinalIgnoreCase)) return "Head";

        // PascalCase genérico: "get" -> "Get"
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    public static int Validate(PromptConfig cfg, out string error)
    {
        if (string.IsNullOrWhiteSpace(cfg.NomeController)) { error = "nomeController está vazio."; return 1; }
        if (string.IsNullOrWhiteSpace(cfg.TipoEndpoint)) { error = "tipoEndpoint está vazio."; return 1; }
        if (string.IsNullOrWhiteSpace(cfg.NomeEndpoint)) { error = "nomeEndpoint está vazio."; return 1; }
        if (string.IsNullOrWhiteSpace(cfg.NomeMetodo)) { error = "nomeMetodo está vazio."; return 1; }
        if (string.IsNullOrWhiteSpace(cfg.Colunas)) { error = "colunas está vazio."; return 1; }
        if (string.IsNullOrWhiteSpace(cfg.PastaDestino)) { error = "pastaDestino está vazio."; return 1; }
        error = string.Empty;
        return 0;
    }

    /// <summary>
    /// Lê o JSON selecionado na pasta 'Template' e concatena todas as steps em uma única string.
    /// Cada propriedade textual de cada etapa é incluída.
    /// </summary>
    public static string BuildTemplateFromJson(string templateFilePath)
    {
        var jsonContent = File.ReadAllText(templateFilePath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        TemplateRoot? root;
        try
        {
            root = JsonSerializer.Deserialize<TemplateRoot>(jsonContent, options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON inválido em '{templateFilePath}': {ex.Message}", ex);
        }

        if (root == null || root.steps == null || root.steps.Count == 0)
        {
            throw new InvalidDataException("O JSON não contém o array 'steps' com conteúdo.");
        }

        var lines = new List<string>();

        for (int i = 0; i < root.steps.Count; i++)
        {
            var etapa = root.steps[i];
            lines.Add($"===== Etapa {i + 1} =====");

            foreach (var kvp in etapa)
            {
                string key = kvp.Key;
                string valueStr = ConvertValueToString(kvp.Value);

                if (!string.IsNullOrWhiteSpace(valueStr))
                {
                    // Cabeçalho com a chave, para contexto
                    lines.Add($"# {key}");
                    lines.Add(valueStr.Trim());
                    lines.Add(""); // linha em branco
                }
            }

            lines.Add(""); // separador entre steps
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ConvertValueToString(object? value)
    {
        if (value == null) return string.Empty;

        if (value is string s) return s;

        if (value is IFormattable f) return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Aplica os placeholders do template a partir do PromptConfig.
    /// </summary>
    public static string BuildPrompt(PromptConfig cfg, string rawTemplate)
    {
        var tipo = NormalizeHttpVerb(cfg.TipoEndpoint);

        return rawTemplate
            .Replace("{nomeController}", cfg.NomeController)
            .Replace("{nomeEndpoint}", cfg.NomeEndpoint)
            .Replace("{tipoEndpoint}", tipo)
            .Replace("{nomeMetodo}", cfg.NomeMetodo)
            .Replace("{colunas}", cfg.Colunas);
    }

    /// <summary>
    /// Lista os arquivos .json da pasta 'Template' (ao lado do executável),
    /// pergunta ao usuário qual deseja usar e retorna o caminho selecionado.
    /// </summary>
    public static string SelectTemplateFileOrThrow()
    {
        var baseDir = AppContext.BaseDirectory;
        var templateDir = Path.Combine(baseDir, "..\\..\\..\\Template");

        if (!Directory.Exists(templateDir))
            throw new DirectoryNotFoundException($"Pasta 'Template' não encontrada em: {templateDir}");

        var jsonFiles = Directory.GetFiles(templateDir, "*.json", SearchOption.TopDirectoryOnly)
                                 .OrderBy(Path.GetFileName)
                                 .ToList();

        if (jsonFiles.Count == 0)
            throw new FileNotFoundException("Nenhum arquivo .json encontrado na pasta 'Template'.");

        Console.WriteLine("Selecione o arquivo JSON de template a utilizar:");
        for (int i = 0; i < jsonFiles.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {Path.GetFileName(jsonFiles[i])}");
        }

        int choice = ReadChoice(jsonFiles.Count);
        var selectedFile = jsonFiles[choice - 1];
        Console.WriteLine($"\nArquivo selecionado: {Path.GetFileName(selectedFile)}");

        return selectedFile;
    }

    public static int ReadChoice(int max)
    {
        while (true)
        {
            Console.Write("\nDigite o número da opção desejada: ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out int n) && n >= 1 && n <= max)
            {
                return n;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Opção inválida. Tente novamente.");
            Console.ResetColor();
        }
    }
}

// ---------------------------
// Programa principal (Main)
// ---------------------------
public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            // Caminho do arquivo de configuração PromptConfig
            string configPath = args.Length > 0 ? args[0] : "..\\..\\..\\prompt-config.json";

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"Arquivo de configuração não encontrado: {configPath}");
                return 1;
            }

            var json = File.ReadAllText(configPath);
            var cfg = JsonSerializer.Deserialize<PromptConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (cfg is null)
            {
                Console.Error.WriteLine("Não foi possível desserializar o JSON de configuração.");
                return 1;
            }

            if (PromptHelper.Validate(cfg, out var error) != 0)
            {
                Console.Error.WriteLine($"Configuração inválida: {error}");
                return 1;
            }

            // 1) Seleciona o arquivo de template na pasta 'Template'
            var templateFilePath = PromptHelper.SelectTemplateFileOrThrow();

            // 2) Constrói a variável 'template' concatenando todas as steps do JSON
            var template = PromptHelper.BuildTemplateFromJson(templateFilePath);

            // 3) Aplica os placeholders com base no PromptConfig
            var prompt = PromptHelper.BuildPrompt(cfg, template);

            // 4) Grava o prompt no destino
            Directory.CreateDirectory(cfg.PastaDestino);
            var filePath = Path.Combine(cfg.PastaDestino, $"{cfg.NomeEndpoint}_Prompt.txt");

            File.WriteAllText(filePath, prompt, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine($"✅ Prompt gerado com sucesso! Salvo em: {filePath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("❌ Erro ao gerar prompt:");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}
