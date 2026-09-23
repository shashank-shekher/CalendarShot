using CalendarShot.Core.Calendar;
using CalendarShot.Core.Parsing;
using CalendarShot.Core.Recognition;
using CalendarShot.Core.Tests.Support;

namespace CalendarShot.Core.Tests.Parsing;

public class AgendaParserTests
{
    private static readonly DateOnly Today = new(2026, 9, 22);

    private readonly CalendarScreenshotParser _parser = new(DateVocabulary.Create());

    [Fact]
    public void Reads_headings_followed_by_events()
    {
        OcrPage page = new PageBuilder()
            .Text(20, 20, "Tue, Sep 22")
            .Text(40, 45, "9:00 AM Standup")
            .Text(40, 70, "All day Company offsite")
            .Text(20, 110, "Wednesday, September 23, 2026")
            .Text(40, 135, "10am-11am Planning")
            .Build();

        RecognitionResult result = _parser.Parse(page, Today).Result!;

        Assert.Equal(CalendarLayout.Agenda, result.Layout);
        Assert.Equal(new DateOnly(2026, 9, 22), result.CoveredFrom);
        Assert.Equal(new DateOnly(2026, 9, 23), result.CoveredTo);
        Assert.Equal(
            [
                new CalendarEvent(new DateOnly(2026, 9, 22), null, null, "Company offsite"),
                new CalendarEvent(new DateOnly(2026, 9, 22), new TimeOnly(9, 0), null, "Standup"),
                new CalendarEvent(new DateOnly(2026, 9, 23), new TimeOnly(10, 0), new TimeOnly(11, 0), "Planning"),
            ],
            result.Events);
    }

    [Fact]
    public void Reads_schedule_rows_with_the_date_on_the_same_line()
    {
        OcrPage page = new PageBuilder()
            .Text(20, 20, "24 SEP, THU")
            .Text(200, 20, "2:00 – 3:00pm")
            .Text(400, 20, "Dentist")
            .Text(200, 45, "5pm")
            .Text(400, 45, "Choir practice")
            .Build();

        RecognitionResult result = _parser.Parse(page, Today).Result!;

        Assert.Equal(
            [
                new CalendarEvent(new DateOnly(2026, 9, 24), new TimeOnly(14, 0), new TimeOnly(15, 0), "Dentist"),
                new CalendarEvent(new DateOnly(2026, 9, 24), new TimeOnly(17, 0), null, "Choir practice"),
            ],
            result.Events);
    }

    [Fact]
    public void Resolves_relative_headings()
    {
        OcrPage page = new PageBuilder()
            .Text(20, 20, "Today")
            .Text(40, 45, "4pm Review")
            .Text(20, 80, "Tomorrow")
            .Text(40, 105, "Offsite")
            .Build();

        RecognitionResult result = _parser.Parse(page, Today).Result!;

        Assert.Equal(Today, result.Events[0].Date);
        Assert.Equal(Today.AddDays(1), result.Events[1].Date);
    }

    [Fact]
    public void Attaches_a_time_on_its_own_line_to_the_following_title()
    {
        OcrPage page = new PageBuilder()
            .Text(20, 20, "Fri, Sep 25")
            .Text(40, 45, "11:30 AM")
            .Text(40, 70, "Customer call")
            .Build();

        RecognitionResult result = _parser.Parse(page, Today).Result!;

        Assert.Equal(new TimeOnly(11, 30), Assert.Single(result.Events).Start);
    }

    [Fact]
    public void Unrecognised_text_reports_an_error()
    {
        OcrPage page = new PageBuilder().Text(20, 20, "Hello world").Build();

        ParseOutcome outcome = _parser.Parse(page, Today);

        Assert.False(outcome.Success);
        Assert.NotNull(outcome.Error);
    }
}
