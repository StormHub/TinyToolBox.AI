using VerifyTests;
using VerifyXunit;

namespace TinyToolBox.AI.Evaluation.Tests.Templates.Formats;

public sealed class PromptTemplateFormatTests
{
    [Fact]
    public async Task Format()
    {
        foreach (var template in PromptTemplate.DefaultCollection())
        {
            var arguments = template.GetParameterNames()
                .Select(x => new KeyValuePair<string, object>(x, $"{x}-value"))
                .ToArray();
            var result = template.Format(arguments);

            var setting = new VerifySettings();
            setting.UseFileName($"{nameof(PromptTemplate)}.{nameof(Format)}.{template.Name}");
            await Verifier.Verify(result, setting);
        }
    }
}