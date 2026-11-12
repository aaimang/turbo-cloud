using System.Globalization;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Figures;

/// <summary>
/// Whose a figure is, from its pieces: each piece of the figure data is a man's (<c>M</c>), a
/// woman's (<c>F</c>) or anyone's (<c>U</c>). A look copied from elsewhere comes without a gender;
/// this is what the avatar editor would have had it as.
/// </summary>
public static class FigureGender
{
    /// <summary>
    /// The gender most of the figure's gendered pieces are; null when none is gendered, the two
    /// are even, or the figure data is empty.
    /// </summary>
    public static AvatarGenderType? Infer(FigureData data, string? figure)
    {
        if (data.IsEmpty || string.IsNullOrWhiteSpace(figure))
            return null;

        var male = 0;
        var female = 0;

        foreach (var part in figure.Split('.'))
        {
            var fields = part.Split('-');

            if (
                fields.Length < 2
                || !data.SetTypes.TryGetValue(fields[0], out var type)
                || !int.TryParse(
                    fields[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var setId
                )
                || !type.Sets.TryGetValue(setId, out var set)
            )
                continue;

            switch (char.ToUpperInvariant(set.Gender))
            {
                case 'M':
                    male++;
                    break;
                case 'F':
                    female++;
                    break;
            }
        }

        return male > female ? AvatarGenderType.Male
            : female > male ? AvatarGenderType.Female
            : null;
    }
}
