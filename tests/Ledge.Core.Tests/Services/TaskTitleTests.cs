using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class TaskTitleTests
{
    [Fact]
    public void Normalize_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Buy milk", TaskTitle.Normalize("  Buy milk \t"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Normalize_ReturnsNull_ForNothingToSave(string? input)
    {
        Assert.Null(TaskTitle.Normalize(input));
    }

    [Fact]
    public void Normalize_KeepsTitleOfExactlyMaxLength()
    {
        var title = new string('a', TaskTitle.MaxLength);

        Assert.Equal(title, TaskTitle.Normalize(title));
    }

    [Fact]
    public void Normalize_CutsLongTitleAtMaxLength()
    {
        var result = TaskTitle.Normalize(new string('a', TaskTitle.MaxLength + 50));

        Assert.Equal(new string('a', TaskTitle.MaxLength), result);
    }

    [Fact]
    public void Normalize_TrimsBeforeMeasuring()
    {
        var title = new string('a', TaskTitle.MaxLength);

        Assert.Equal(title, TaskTitle.Normalize("   " + title + "   "));
    }

    [Fact]
    public void Normalize_NeverSplitsAnEmoji()
    {
        // 199 letters + 😀 (two UTF-16 chars) = 201 chars; a plain cut at 200
        // would keep only the first half of the emoji.
        var input = new string('a', TaskTitle.MaxLength - 1) + "😀";

        var result = TaskTitle.Normalize(input);

        Assert.Equal(new string('a', TaskTitle.MaxLength - 1), result);
    }

    [Fact]
    public void Normalize_KeepsAnEmojiThatFitsCompletely()
    {
        var input = new string('a', TaskTitle.MaxLength - 2) + "😀" + "bbb";

        Assert.Equal(new string('a', TaskTitle.MaxLength - 2) + "😀", TaskTitle.Normalize(input));
    }

    [Fact]
    public void Normalize_TrimsSpaceExposedByTheCut()
    {
        var input = new string('a', TaskTitle.MaxLength - 1) + " next words";

        Assert.Equal(new string('a', TaskTitle.MaxLength - 1), TaskTitle.Normalize(input));
    }
}
