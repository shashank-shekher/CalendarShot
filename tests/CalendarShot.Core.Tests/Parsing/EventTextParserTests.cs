using CalendarShot.Core.Parsing;

namespace CalendarShot.Core.Tests.Parsing;

public class EventTextParserTests
{
    [Theory]
    [InlineData("9:30 AM Standup", "Standup", "09:30", null)]
    [InlineData("9:30am Standup", "Standup", "09:30", null)]
    [InlineData("10am Review", "Review", "10:00", null)]
    [InlineData("12pm Lunch", "Lunch", "12:00", null)]
    [InlineData("12am Release", "Release", "00:00", null)]
    [InlineData("14:00 Planning", "Planning", "14:00", null)]
    [InlineData("14:00-15:30 Planning", "Planning", "14:00", "15:30")]
    [InlineData("9 – 10am Design sync", "Design sync", "09:00", "10:00")]
    [InlineData("11-1pm Workshop", "Workshop", "11:00", "13:00")]
    [InlineData("9:00 AM - 10:30 AM Interview", "Interview", "09:00", "10:30")]
    [InlineData("Lunch, 12-1pm", "Lunch", "12:00", "13:00")]
    [InlineData("Standup (9:15am)", "Standup", "09:15", null)]
    [InlineData("• 3pm Dentist", "Dentist", "15:00", null)]
    [InlineData("9:O0 AM Standup", "Standup", "09:00", null)]
    [InlineData("Call at 4pm", "Call", "16:00", null)]
    [InlineData("9a Gym", "Gym", "09:00", null)]
    [InlineData("I I AM Dentist", "Dentist", "11:00", null)]
    [InlineData("I Iam Dentist", "Dentist", "11:00", null)]
    [InlineData("1 lam - 12:30pm Planning", "Planning", "11:00", "12:30")]
    [InlineData("9 - IOam Design review", "Design review", "09:00", "10:00")]
    [InlineData("gam Team standup", "Team standup", "09:00", null)]
    public void Parses_times_and_titles(string input, string title, string start, string? end)
    {
        ParsedEventText parsed = EventTextParser.Parse(input);

        Assert.Equal(title, parsed.Title);
        Assert.Equal(TimeOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), parsed.Start);
        Assert.Equal(end is null ? null : TimeOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), parsed.End);
    }

    [Theory]
    [InlineData("Company offsite")]
    [InlineData("Sprint 5 retro")]
    [InlineData("10 Downing Street visit")]
    [InlineData("Room 5")]
    [InlineData("I am out of office")]
    [InlineData("Go live")]
    public void Text_without_a_clock_time_is_all_day(string input)
    {
        ParsedEventText parsed = EventTextParser.Parse(input);

        Assert.Equal(input, parsed.Title);
        Assert.Null(parsed.Start);
    }

    [Fact]
    public void All_day_marker_is_removed()
    {
        ParsedEventText parsed = EventTextParser.Parse("All day Public holiday");

        Assert.Equal("Public holiday", parsed.Title);
        Assert.Null(parsed.Start);
    }

    [Fact]
    public void Time_only_text_has_empty_title()
    {
        ParsedEventText parsed = EventTextParser.Parse("10 – 11am");

        Assert.Equal(string.Empty, parsed.Title);
        Assert.Equal(new TimeOnly(10, 0), parsed.Start);
        Assert.Equal(new TimeOnly(11, 0), parsed.End);
    }

    [Theory]
    [InlineData("+2 more")]
    [InlineData("3 more")]
    [InlineData("+4")]
    public void Recognises_hidden_event_markers(string input)
    {
        Assert.True(EventTextParser.IsHiddenEventsMarker(input));
        Assert.True(EventTextParser.IsNoise(input));
    }

    [Theory]
    [InlineData("Today")]
    [InlineData("•")]
    [InlineData("12")]
    [InlineData("No events")]
    public void Recognises_calendar_chrome_as_noise(string input)
    {
        Assert.True(EventTextParser.IsNoise(input));
    }

    [Fact]
    public void Titles_are_limited_in_length()
    {
        ParsedEventText parsed = EventTextParser.Parse(new string('x', 500));

        Assert.Equal(EventTextParser.MaxTitleLength, parsed.Title.Length);
    }

    [Theory]
    [InlineData("9 AM", 9 * 60)]
    [InlineData("12 PM", 12 * 60)]
    [InlineData("13:00", 13 * 60)]
    [InlineData("7", 7 * 60)]
    public void Parses_axis_labels(string input, int minutes)
    {
        Assert.True(EventTextParser.TryParseAxisLabel(input, out int parsed));
        Assert.Equal(minutes, parsed);
    }

    [Theory]
    [InlineData("09:30", 9, 30)]
    [InlineData("9:30 pm", 21, 30)]
    [InlineData("9pm", 21, 0)]
    [InlineData("18.45", 18, 45)]
    public void Parses_user_entered_times(string input, int hour, int minute)
    {
        Assert.True(EventTextParser.TryParseTime(input, out TimeOnly time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("13pm")]
    [InlineData("noon-ish")]
    public void Rejects_invalid_user_entered_times(string input)
    {
        Assert.False(EventTextParser.TryParseTime(input, out _));
    }
}
