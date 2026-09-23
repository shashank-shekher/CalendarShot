using CalendarShot.Core.Calendar;
using CalendarShot.Core.Parsing;
using CalendarShot.Core.Tests.Support;

namespace CalendarShot.Core.Tests.Parsing;

public class TimeGridParserTests
{
    private const double FirstColumnCenter = 160;
    private const double ColumnWidth = 170;
    private const double NineAmY = 200;
    private const double HourHeight = 60;

    private static readonly DateOnly Today = new(2026, 9, 23);

    private readonly CalendarScreenshotParser _parser = new(DateVocabulary.Create());

    [Fact]
    public void Reads_a_week_view_with_printed_times()
    {
        PageBuilder page = CreateWeek();
        page.Text(ColumnLeft(1), YFor(10, 0) + 2, "Design review")
            .Text(ColumnLeft(1), YFor(10, 0) + 20, "10 – 11am");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        Assert.Equal(CalendarLayout.TimeGrid, result.Layout);
        Assert.Equal(new DateOnly(2026, 9, 20), result.CoveredFrom);
        Assert.Equal(new DateOnly(2026, 9, 26), result.CoveredTo);
        Assert.Equal(
            new CalendarEvent(new DateOnly(2026, 9, 21), new TimeOnly(10, 0), new TimeOnly(11, 0), "Design review"),
            Assert.Single(result.Events));
    }

    [Fact]
    public void Estimates_times_from_the_hour_axis_when_none_are_printed()
    {
        PageBuilder page = CreateWeek();
        page.Text(ColumnLeft(3), YFor(11, 0) + 2, "Focus time")
            .Text(ColumnLeft(3), YFor(11, 0) + 20, "Room 4");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        CalendarEvent e = Assert.Single(result.Events);
        Assert.Equal(new DateOnly(2026, 9, 23), e.Date);
        Assert.Equal(new TimeOnly(11, 0), e.Start);
        Assert.Equal("Focus time", e.Title);
        Assert.Equal("Room 4", e.Notes);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Events_above_the_hour_grid_are_all_day()
    {
        PageBuilder page = CreateWeek();
        page.Text(ColumnLeft(5), 130, "Public holiday");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        CalendarEvent e = Assert.Single(result.Events);
        Assert.Equal(new DateOnly(2026, 9, 25), e.Date);
        Assert.True(e.IsAllDay);
    }

    [Fact]
    public void Reads_stacked_weekday_and_number_headers()
    {
        var page = new PageBuilder();
        page.Text(20, 10, "September 2026", height: 24);
        string[] names = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
        for (int i = 0; i < 7; i++)
        {
            page.Centered(FirstColumnCenter + (i * ColumnWidth), 60, names[i]);
            page.Centered(FirstColumnCenter + (i * ColumnWidth), 80, (20 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), height: 22);
        }

        AddAxis(page);
        page.Text(ColumnLeft(4), YFor(14, 0) + 2, "Dentist, 2pm");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        CalendarEvent e = Assert.Single(result.Events);
        Assert.Equal(new DateOnly(2026, 9, 24), e.Date);
        Assert.Equal(new TimeOnly(14, 0), e.Start);
    }

    private static PageBuilder CreateWeek()
    {
        var page = new PageBuilder();
        page.Text(20, 10, "September 2026", height: 24);
        string[] names = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
        for (int i = 0; i < 7; i++)
        {
            page.Centered(FirstColumnCenter + (i * ColumnWidth), 70, $"{names[i]} {20 + i}");
        }

        AddAxis(page);
        return page;
    }

    private static void AddAxis(PageBuilder page)
    {
        string[] labels = ["9 AM", "10 AM", "11 AM", "12 PM", "1 PM", "2 PM", "3 PM"];
        for (int i = 0; i < labels.Length; i++)
        {
            page.Text(20, NineAmY + (i * HourHeight) - (PageBuilder.TextHeight / 2), labels[i]);
        }
    }

    private static double ColumnLeft(int column) => FirstColumnCenter + (column * ColumnWidth) - (ColumnWidth / 2) + 6;

    private static double YFor(int hour, int minute) => NineAmY + ((hour - 9 + (minute / 60.0)) * HourHeight);
}
