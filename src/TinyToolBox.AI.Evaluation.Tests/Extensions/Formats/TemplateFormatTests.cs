using System.Text.Json;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Evaluation.Extensions;
using TinyToolBox.AI.Evaluation.Templates;
using VerifyTests;
using VerifyXunit;

namespace TinyToolBox.AI.Evaluation.Tests.Extensions.Formats;

public sealed class TemplateFormatTests
{
    [Fact]
    public async Task Configuration()
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        foreach (var (name, yaml) in YamlTemplate.FromManifestResource())
        {
            var promptTemplateConfig = yaml.ToPromptTemplateConfig(name);

            var setting = new VerifySettings();
            setting.UseFileName($"{name}.{nameof(PromptTemplateConfig)}");

            var json = JsonSerializer.Serialize(promptTemplateConfig, jsonSerializerOptions);
            await Verifier.Verify(json, setting);
        }
    }
}