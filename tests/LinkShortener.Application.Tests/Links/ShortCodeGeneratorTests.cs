using LinkShortener.Application.Links;

namespace LinkShortener.Application.Tests.Links;

public sealed class ShortCodeGeneratorTests
{
    [Fact]
    public void Generate_returns_seven_characters_from_the_non_ambiguous_alphabet()
    {
        var generator = new ShortCodeGenerator();

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var code = generator.Generate();

            Assert.Equal(7, code.Length);
            Assert.All(code, character => Assert.Contains(character, ShortCodeGenerator.Alphabet));
        }
    }
}
