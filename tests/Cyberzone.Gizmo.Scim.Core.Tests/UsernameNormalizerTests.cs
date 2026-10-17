using Cyberzone.Gizmo.Scim.Core.Sync;

namespace Cyberzone.Gizmo.Scim.Core.Tests;

public class UsernameNormalizerTests
{
    [Theory]
    [InlineData("john", "john")]
    [InlineData("John Doe", "John_Doe")]
    [InlineData("  a  b  ", "a_b")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j")]
    [InlineData("name.", "name")]
    [InlineData("name...", "name")]
    [InlineData("CON", "CON_")]
    [InlineData("com1", "com1_")]
    [InlineData("nul.txt", "nul.txt_")]
    [InlineData("Иван_Петров", "Иван_Петров")]
    [InlineData("", "user")]
    [InlineData("///", "user")]
    [InlineData("user@example.com", "user@example.com")]
    public void Normalize_applies_gizmo_rules(string input, string expected)
    {
        Assert.Equal(expected, UsernameNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_cuts_to_30_chars_without_trailing_dot()
    {
        var result = UsernameNormalizer.Normalize("abcdefghijklmnopqrstuvwxyz012.456789");
        Assert.Equal("abcdefghijklmnopqrstuvwxyz012", result);
        Assert.True(result.Length <= UsernameNormalizer.MaxLength);
    }

    [Fact]
    public void MakeUnique_appends_suffix_within_limit()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "abcdefghijklmnopqrstuvwxyz0123", "abcdefghijklmnopqrstuvwxyz01_2" };
        var result = UsernameNormalizer.MakeUnique("abcdefghijklmnopqrstuvwxyz0123456", taken.Contains);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz01_3", result);
    }

    [Fact]
    public void MakeUnique_is_case_insensitive_through_callback()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Player" };
        Assert.Equal("player_2", UsernameNormalizer.MakeUnique("player", taken.Contains));
    }
}
