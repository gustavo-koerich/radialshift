using System.Text;
using System.Text.Json;
using RadialShift.Ferramentas;

// Uso: RadialShift.Ferramentas.exe --entrada "<arquivo.json>"
// Escreve uma única linha JSON no stdout e sai com o código da seção 7.2 da spec.
Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

var entrada = args.Length == 2 && args[0] == "--entrada" ? args[1] : null;
if (entrada is null)
    return Write(new ToolOutput(false, Array.Empty<string>(), "Uso: --entrada <arquivo.json>", Array.Empty<ToolError>()),
        ExitCodes.Unexpected);

ToolInput? input;
try
{
    input = JsonSerializer.Deserialize<ToolInput>(File.ReadAllText(entrada, Encoding.UTF8));
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
{
    input = null;
}
if (input is null || string.IsNullOrWhiteSpace(input.Acao) || input.Arquivos is null)
    return Write(new ToolOutput(false, Array.Empty<string>(), "Arquivo de entrada inválido", Array.Empty<ToolError>()),
        ExitCodes.Unexpected);

var (output, code) = Tool.Run(input, Console.In);
return Write(output, code);

static int Write(ToolOutput output, int code)
{
    Console.Out.WriteLine(JsonSerializer.Serialize(output));
    Console.Out.Flush();
    return code;
}
