using CalendarShot.Core.Calendar;
using CalendarShot.Core.Parsing;
using CalendarShot.Core.Tests.Support;

namespace CalendarShot.Core.Tests.Parsing;

public class MonthGridParserTests
{
    // September 2026 starts on a Tuesday, so a Sunday-first grid starts on 30 August.
    private const double GridLeft = 20;
    private const double CellWidth = 140;
    private const double HeaderY = 60;
    private const double FirstRowY = 90;
    private const double RowHeight = 120;

    private static readonly string[] SundayFirst = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private static readonly int[][] SeptemberWeeks =
    [
        [30, 31, 1, 2, 3, 4, 5],
        [6, 7, 8, 9, 10, 11, 12],
        [13, 14, 15, 16, 17, 18, 19],
        [20, 21, 22, 23, 24, 25, 26],
        [27, 28, 29, 30, 1, 2, 3],
    ];

    private static readonly DateOnly Today = new(2026, 9, 23);

    private readonly CalendarScreenshotParser _parser = new(DateVocabulary.Create());

    [Fact]
    public void Reads_a_month_grid()
    {
        PageBuilder page = CreateSeptemberGrid();
        page.Text(CellX(2), EventY(0, 0), "9:00 AM Standup")
            .Text(CellX(5), EventY(3, 0), "2pm-3:30pm Review")
            .Text(CellX(5), EventY(3, 1), "Team lunch")
            .Text(CellX(4), EventY(4, 0), "10am Release");

        ParseOutcome outcome = _parser.Parse(page.Build(), Today);

        Assert.True(outcome.Success, outcome.Error);
        RecognitionResult result = outcome.Result!;
        Assert.Equal(CalendarLayout.MonthGrid, result.Layout);
        Assert.Equal(new DateOnly(2026, 8, 30), result.CoveredFrom);
        Assert.Equal(new DateOnly(2026, 10, 3), result.CoveredTo);
        Assert.Equal(
            [
                new CalendarEvent(new DateOnly(2026, 9, 1), new TimeOnly(9, 0), null, "Standup"),
                new CalendarEvent(new DateOnly(2026, 9, 25), null, null, "Team lunch"),
                new CalendarEvent(new DateOnly(2026, 9, 25), new TimeOnly(14, 0), new TimeOnly(15, 30), "Review"),
                new CalendarEvent(new DateOnly(2026, 10, 1), new TimeOnly(10, 0), null, "Release"),
            ],
            result.Events);
    }

    [Fact]
    public void Splits_text_that_ocr_merged_across_cells()
    {
        PageBuilder page = CreateSeptemberGrid();
        page.MergedLine(
            EventY(1, 0),
            (CellX(1), "9am Planning"),
            (CellX(2), "Dentist"),
            (CellX(3), "4pm Yoga"));

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        Assert.Equal(
            [
                new CalendarEvent(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), null, "Planning"),
                new CalendarEvent(new DateOnly(2026, 9, 8), null, null, "Dentist"),
                new CalendarEvent(new DateOnly(2026, 9, 9), new TimeOnly(16, 0), null, "Yoga"),
            ],
            result.Events);
    }

    [Fact]
    public void Tolerates_a_week_whose_day_numbers_were_not_read()
    {
        PageBuilder page = CreateSeptemberGrid(skipWeek: 2);
        page.Text(CellX(3), EventY(3, 0), "Board meeting");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        CalendarEvent single = Assert.Single(result.Events);
        Assert.Equal(new DateOnly(2026, 9, 23), single.Date);
    }

    [Fact]
    public void Works_without_a_month_caption()
    {
        PageBuilder page = CreateSeptemberGrid(includeCaption: false);
        page.Text(CellX(2), EventY(0, 0), "Kickoff");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(result.Events).Date);
    }

    [Fact]
    public void Warns_about_hidden_events()
    {
        PageBuilder page = CreateSeptemberGrid();
        page.Text(CellX(1), EventY(2, 0), "Workshop")
            .Text(CellX(1), EventY(2, 1), "+2 more");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        Assert.Single(result.Events);
        Assert.Contains(result.Warnings, w => w.Contains("more events", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignores_a_mini_calendar_in_the_side_bar()
    {
        PageBuilder page = CreateSeptemberGrid(offsetX: 260);

        // Narrow mini calendar on the left showing the same month.
        for (int i = 0; i < 7; i++)
        {
            page.Text(10 + (i * 30), 400, SundayFirst[i][..1]);
        }

        for (int week = 0; week < SeptemberWeeks.Length; week++)
        {
            for (int day = 0; day < 7; day++)
            {
                page.Text(10 + (day * 30), 420 + (week * 20), SeptemberWeeks[week][day].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        page.Text(CellX(2, 260), EventY(0, 0), "Standup");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(result.Events).Date);
    }

    [Fact]
    public void Reads_centred_headers_with_left_aligned_events()
    {
        // Google Calendar style: weekday names and day numbers are centred in
        // their cells while event chips start at the left border.
        var page = new PageBuilder();
        page.Text(GridLeft, 10, "September 2026", height: 24);
        string[] names = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
        for (int i = 0; i < 7; i++)
        {
            double center = GridLeft + (i * CellWidth) + (CellWidth / 2);
            page.Centered(center, HeaderY, names[i]);
            for (int week = 0; week < SeptemberWeeks.Length; week++)
            {
                page.Centered(center, FirstRowY + (week * RowHeight), SeptemberWeeks[week][i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        page.Text(CellX(3), EventY(2, 0), "11:30am Budget sync")
            .Text(CellX(4), EventY(2, 0), "Offsite day two");

        RecognitionResult result = _parser.Parse(page.Build(), Today).Result!;

        Assert.Equal(
            [
                new CalendarEvent(new DateOnly(2026, 9, 16), new TimeOnly(11, 30), null, "Budget sync"),
                new CalendarEvent(new DateOnly(2026, 9, 17), null, null, "Offsite day two"),
            ],
            result.Events);
    }

    private static PageBuilder CreateSeptemberGrid(bool includeCaption = true, int skipWeek = -1, double offsetX = 0)
    {
        var page = new PageBuilder(1400 + offsetX);
        if (includeCaption)
        {
            page.Text(GridLeft + offsetX, 10, "September 2026", height: 24);
        }

        for (int i = 0; i < 7; i++)
        {
            page.Text(CellX(i, offsetX), HeaderY, SundayFirst[i]);
        }

        for (int week = 0; week < SeptemberWeeks.Length; week++)
        {
            if (week == skipWeek)
            {
                continue;
            }

            for (int day = 0; day < 7; day++)
            {
                int number = SeptemberWeeks[week][day];
                string label = week == 4 && number == 1 ? "Oct 1" : number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                page.Text(CellX(day, offsetX), FirstRowY + (week * RowHeight), label);
            }
        }

        return page;
    }

    private static double CellX(int column, double offsetX = 0) => GridLeft + offsetX + (column * CellWidth) + 6;

    private static double EventY(int week, int line) => FirstRowY + (week * RowHeight) + 22 + (line * 20);
}
