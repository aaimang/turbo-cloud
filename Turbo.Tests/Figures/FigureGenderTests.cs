using FluentAssertions;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Rooms.Enums;
using Xunit;

namespace Turbo.Tests.Figures;

/// <summary>A look copied from elsewhere has no gender: it is the one most of its gendered pieces are.</summary>
public sealed class FigureGenderTests
{
    private static FigureSetType Type(string type, params (int Id, char Gender)[] sets) =>
        new(
            type,
            1,
            false,
            false,
            false,
            false,
            sets.ToDictionary(
                x => x.Id,
                x => new FigureSet(x.Id, x.Gender, 0, true, true, false, false, 1)
            )
        );

    private static readonly FigureData DATA = new(
        new Dictionary<string, FigureSetType>
        {
            ["hd"] = Type("hd", (180, 'M'), (600, 'F'), (99, 'U')),
            ["ch"] = Type("ch", (210, 'M'), (630, 'F')),
            ["lg"] = Type("lg", (270, 'U'), (700, 'F')),
        },
        new Dictionary<int, FigurePalette>()
    );

    [Theory]
    [InlineData("hd-180-1.ch-210-66.lg-270-82", AvatarGenderType.Male)]
    [InlineData("hd-600-1.ch-630-62.lg-700-82", AvatarGenderType.Female)]
    [InlineData("hd-180-1.ch-630-62.lg-700-82", AvatarGenderType.Female)]
    public void A_figure_is_the_gender_most_of_its_pieces_are(
        string figure,
        AvatarGenderType gender
    ) => FigureGender.Infer(DATA, figure).Should().Be(gender);

    [Theory]
    [InlineData("hd-99-1.lg-270-82")]
    [InlineData("hd-180-1.ch-630-62")]
    [InlineData("xx-1-1.hd-12345-1")]
    [InlineData("")]
    public void Without_a_majority_there_is_no_gender(string figure) =>
        FigureGender.Infer(DATA, figure).Should().BeNull();

    [Fact]
    public void Without_figure_data_there_is_no_gender() =>
        FigureGender.Infer(FigureData.Empty, "hd-180-1").Should().BeNull();
}
