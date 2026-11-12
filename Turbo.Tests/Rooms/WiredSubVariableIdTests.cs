using FluentAssertions;
using Turbo.Primitives.Rooms.Wired.Variable;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo's files (G-Presets) name a sub-variable "s" + its parent variable's id + "-" + the furni
/// id of the add-on deriving it, and "-" + which of the add-on's it is past the first. Turbo reads
/// it as the add-on's own id for that sub-variable.
/// </summary>
public sealed class WiredSubVariableIdTests
{
    [Theory]
    [InlineData("s123-456", "123", 456, 0)]
    [InlineData("s123-456-2", "123", 456, 2)]
    [InlineData("s-120-456", "-120", 456, 0)]
    [InlineData("s-120-456-3", "-120", 456, 3)]
    [InlineData("s~5-456", "~5", 456, 0)]
    public void A_sub_variable_id_names_its_parent_and_add_on(
        string text,
        string parent,
        int addon,
        int index
    )
    {
        WiredVariableId.TryParseSub(text, out var p, out var a, out var i).Should().BeTrue();

        (p, a, i).Should().Be((parent, addon, index));
        WiredVariableId.TryParse(text, out var id).Should().BeTrue();
        id.Should().Be(WiredVariableId.OfBox(addon, index));
        WiredVariableId.SubText(parent, addon, index).Should().Be(text);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("s123")]
    [InlineData("s-456")]
    [InlineData("s123-x")]
    [InlineData("s123-0")]
    [InlineData("sabc")]
    public void Anything_else_is_refused(string text)
    {
        WiredVariableId.TryParseSub(text, out _, out _, out _).Should().BeFalse();
        WiredVariableId.TryParse(text, out _).Should().BeFalse();
    }
}
