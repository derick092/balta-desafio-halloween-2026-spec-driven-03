using System.Security.Cryptography;

namespace LinkShortener.Application.Links;

public interface IShortCodeGenerator
{
    string Generate();
}

public sealed class ShortCodeGenerator : IShortCodeGenerator
{
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int GeneratedCodeLength = 7;

    public string Generate()
    {
        return string.Create(GeneratedCodeLength, Alphabet, static (code, alphabet) =>
        {
            for (var index = 0; index < code.Length; index++)
            {
                code[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }
}
