using Api.Services.Households;

namespace Api.Tests.Households;

public class InvitationCodeTests
{
    [Fact]
    public void Generate_UsesOnlyUnambiguousCharacters()
    {
        for (var i = 0; i < 1000; i++)
        {
            var code = InvitationCode.Generate();

            Assert.Equal(InvitationCode.Length, code.Length);
            Assert.All(code, c => Assert.Contains(c, InvitationCode.Alphabet));
        }

        Assert.DoesNotContain('0', InvitationCode.Alphabet);
        Assert.DoesNotContain('O', InvitationCode.Alphabet);
        Assert.DoesNotContain('1', InvitationCode.Alphabet);
        Assert.DoesNotContain('I', InvitationCode.Alphabet);
    }

    [Fact]
    public void Generate_ProducesDifferentCodes()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => InvitationCode.Generate()).ToHashSet();

        Assert.Equal(1000, codes.Count);
    }

    [Theory]
    [InlineData("ABCD2345", "ABCD2345")]
    [InlineData("abcd2345", "ABCD2345")]
    [InlineData(" abcd-2345 ", "ABCD2345")]
    [InlineData("AB CD 23 45", "ABCD2345")]
    public void Normalize_ToleratesCaseSpacesAndDashes(string input, string expected)
    {
        Assert.Equal(expected, InvitationCode.Normalize(input));
    }
}
