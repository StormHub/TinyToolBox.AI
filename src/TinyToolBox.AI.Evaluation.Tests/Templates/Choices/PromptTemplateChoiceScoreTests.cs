using VerifyTests;
using VerifyXunit;

namespace TinyToolBox.AI.Evaluation.Tests.Templates.Choices;

public sealed class PromptTemplateChoiceScoreTests
{
    [Fact]
    public async Task ChoiceScores()
    {
        foreach (var template in PromptTemplate.DefaultCollection())
        {
            var choiceScore = new Dictionary<string, float>(template.GetChoiceScores());
            
            var setting = new VerifySettings();
            setting.UseFileName($"{nameof(PromptTemplate)}.{nameof(ChoiceScores)}.{template.Name}");
            await Verifier.Verify(choiceScore, setting);
        }
    }
}