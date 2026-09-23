# CalendarShot User Manual

CalendarShot turns screenshots of your calendar into `.ics` calendar files. It is meant for situations where you can see your calendar but cannot export, share or subscribe to it.

This manual covers version 1.0.

## Contents

1. [How it works](#how-it-works)
2. [Requirements](#requirements)
3. [Installing](#installing)
4. [First-time setup](#first-time-setup)
5. [Capturing your calendar](#capturing-your-calendar)
6. [Reviewing events](#reviewing-events)
7. [The monthly files](#the-monthly-files)
8. [Importing into a calendar app](#importing-into-a-calendar-app)
9. [The tray menu](#the-tray-menu)
10. [Getting the best results](#getting-the-best-results)
11. [Where CalendarShot keeps its files](#where-calendarshot-keeps-its-files)
12. [Troubleshooting](#troubleshooting)
13. [Updating and uninstalling](#updating-and-uninstalling)
14. [Privacy](#privacy)

## How it works

1. At the time you choose, CalendarShot shows a small reminder in the corner of the screen.
2. You open your calendar, usually in month view, and click **Capture**.
3. CalendarShot takes a screenshot, reads the dates and events in it, and shows you what it found.
4. You correct anything that was misread and click **Export**.
5. CalendarShot updates one `.ics` file per month in your export folder.
6. You import those files into the calendar where you need them.

## Requirements

- Windows 10 version 2004 or later, or Windows 11, 64-bit.
- A Windows display language that supports text recognition. English and most major languages do. To check or add one, open **Settings > Time & language > Language & region**.
- No administrator rights are needed.

## Installing

1. Download `CalendarShot-<version>-win-x64.zip` from the project's **Releases** page.
2. Optional but recommended: check the download. Open PowerShell in the download folder and run:

   ```powershell
   Get-FileHash .\CalendarShot-1.0.0-win-x64.zip -Algorithm SHA256
   ```

   The result must match the value in the `.sha256` file published with the release.
3. Extract the zip to a folder where it can stay, for example `C:\Users\<you>\AppData\Local\Programs\CalendarShot`. Do not run it from inside the zip or from your Downloads folder, because Windows starts CalendarShot from this location every time you sign in.
4. Double-click `CalendarShot.exe`.

Windows SmartScreen may warn about an unrecognised app the first time. Choose **More info > Run anyway** if you downloaded the file from the official Releases page and the checksum matched.

## First-time setup

On first start CalendarShot opens its settings window. Every setting can be changed later from the tray menu (**Settings…**).

| Setting | What it does | Default |
| --- | --- | --- |
| **Save .ics files to** | The folder where the monthly calendar files are written. | Your Downloads folder |
| **Remind me to capture my calendar** | Turns the daily reminder on or off. | On |
| **Reminder time** | Time of day when the reminder appears. | 9:00 |
| **On these days** | Days on which you are reminded. | Monday to Friday |
| **Capture delay** | Seconds between clicking Capture and the screenshot being taken, so you can switch to your calendar. Use 0 to capture at once. | 5 |
| **Default event length** | Length used for events whose end time is not visible, for example "9:00 AM Standup" in a month view. | 60 minutes |
| **Text recognition language** | Language used to read the screenshot. *Automatic* uses your Windows display language. Pick the language your calendar is shown in if it differs. | Automatic |
| **Start CalendarShot when I sign in to Windows** | Starts CalendarShot automatically in the notification area. | On |
| **Keep captured screenshots** | Saves each screenshot in `Documents\CalendarShot\screenshots`. Useful when something is not recognised, but it keeps copies of your calendar on disk. | Off |

Click **Save and start**. CalendarShot now runs in the notification area, the icons next to the clock. If you cannot see its icon, click the **^** arrow to show hidden icons. You can drag the icon onto the taskbar to keep it visible.

## Capturing your calendar

### When the reminder appears

The reminder has three buttons:

- **Capture** starts a capture (see below).
- **Snooze 15 min** hides the reminder for 15 minutes.
- **Skip today** hides it until the next reminder day. Closing the window does the same.

If your PC was switched off or asleep at the reminder time, the reminder appears as soon as CalendarShot is running again on the same day.

### Taking the capture

1. Open your calendar and switch to the view you want to capture. **Month view** gives the most coverage per capture.
2. Make sure the calendar window is on the monitor where your **mouse pointer** is. CalendarShot captures the whole of that monitor.
3. Click **Capture**, or double-click the tray icon at any time.
4. A countdown appears in the corner. Bring the calendar to the front before it reaches zero. Click **Cancel** to stop.

CalendarShot then reads the screenshot. This usually takes a few seconds.

### Other ways to provide a screenshot

From the tray menu you can also use:

- **Import screenshot from file…** for a PNG, JPEG or BMP image you already have.
- **Import screenshot from clipboard** after using **Windows + Shift + S** or **Print Screen**. This is handy for capturing just the calendar area.

## Reviewing events

After reading the screenshot CalendarShot opens the **Review events** window. The screenshot is on the left and the recognised events are on the right.

At the top you see which view was detected (month, day/week or agenda), the date range the screenshot covers, and any warnings. For example, a warning appears when a day showed "+2 more" and some events were hidden.

Each row is one event:

| Column | Meaning |
| --- | --- |
| **Use** | Untick to leave the event out. |
| **Date** | In the form `2026-09-23`. |
| **Start / End** | 24-hour time such as `09:30`. You may also type `9:30 pm`. Leave **Start** empty for an all-day event. **End** may be empty, in which case the default event length is used. |
| **Title** | The event name. |
| **Notes** | Optional extra text, such as a room name read from a week view. |

Buttons:

- **Add** creates a new row for an event that was missed.
- **Remove selected** deletes the selected rows.
- **Export** writes the files.
- **Cancel** discards this capture. Nothing is written.

### "Replace previously exported events from … to …"

This option is ticked by default and set to the date range visible in the screenshot. When it is ticked, events you exported earlier for those dates are replaced by the events in this capture. That way, a meeting that was cancelled or moved also disappears from, or moves in, your monthly file.

Untick it to only **add** events and keep everything exported before. That is useful when you import a partial screenshot, for example a single day from the clipboard.

If a row shows a warning icon after you click **Export**, point at the icon to see what needs fixing.

## The monthly files

Files are named `CalendarShot-YYYY-MM.ics`, for example `CalendarShot-2026-09.ics`, and saved in your export folder.

- A month view usually shows a few days of the previous and next month. Those days go into the files for those months, so one capture can update up to three files.
- Each file is **cumulative**. It holds every event exported for that month so far, not just the latest capture.
- Each event has a stable ID. Most calendar apps update existing events instead of creating duplicates when you import a newer version of the same file.
- Timed events are stored in UTC, calculated from your PC's time zone, so they show at the right time wherever you import them.

After an export a notification lists the files that were written. Click it to open the export folder.

## Importing into a calendar app

CalendarShot only creates the files. Importing them is up to you. Some common options:

- **Outlook (classic desktop):** File > Open & Export > Import/Export > *Import an iCalendar (.ics) or vCalendar file* > choose the file > **Import**. Choose *Open as New* instead if you want a separate calendar.
- **Outlook on the web / new Outlook:** Calendar > Add calendar > Upload from file.
- **Google Calendar (web):** Settings > Import & export > Select file from your computer > choose a destination calendar > Import.
- **Apple Calendar (macOS):** File > Import.
- **Phones:** send the file to yourself and open it, or import it on the web version of your calendar service so it syncs to the phone.

Tip: import into a dedicated calendar, for example "Work (CalendarShot)". If something goes wrong you can delete that calendar and import again without touching your other events.

Note that importing adds or updates events but usually does not delete events that are no longer in the file. To reflect cancellations exactly, delete the dedicated calendar's events for that month and import the latest file again.

## The tray menu

Right-click the CalendarShot icon in the notification area:

| Item | Action |
| --- | --- |
| **Capture calendar now** | Start a capture immediately. Double-clicking the icon does the same. |
| **Import screenshot from file…** | Read an existing image. |
| **Import screenshot from clipboard** | Read the image on the clipboard. |
| **Open export folder** | Open the folder with your `.ics` files. |
| **Open data folder** | Open `Documents\CalendarShot`. |
| **Settings…** | Change settings. Starting `CalendarShot.exe` again while it is running also opens this. |
| **About** | Version and licence information. |
| **Exit** | Close CalendarShot until the next sign-in, or until you start it again. |

## Getting the best results

- **Use month view** for everyday captures and **week view** for busy weeks. Month views often truncate titles and hide events behind "+N more". Week views show more detail and times.
- **Maximise the calendar window** so that text is as large as possible. At 100% display scaling, zooming the calendar in (Ctrl + plus in a browser) also helps.
- **Very small calendar text** (HCL Notes, for example) is read with extra passes, but letters can still be misread. If titles come out garbled, increase the calendar's font size, or raise Windows display scaling under **Settings > System > Display > Scale** while capturing.
- **Make sure weekday names are visible** ("Mon", "Tuesday", "SUN"…), as well as the month and year title such as "September 2026". CalendarShot uses them to work out the dates.
- **Close pop-ups and tooltips** that cover the calendar before the countdown ends.
- **Keep the capture regular.** Capturing every working day means each month's file stays current and changes are picked up quickly.
- Light and dark themes both work.

## Where CalendarShot keeps its files

Everything is stored in your **Documents** folder under `CalendarShot`:

| Path | Contents |
| --- | --- |
| `settings.json` | Your settings. |
| `state.json` | When the last reminder was shown. |
| `data\YYYY-MM.json` | The events exported for each month. The `.ics` files are generated from these, so you can move or delete exported files without losing history. |
| `logs\` | Daily log files, kept for 14 days. They record what CalendarShot did, never your event titles. |
| `screenshots\` | Only if **Keep captured screenshots** is on. |

If your Documents folder is synchronised with OneDrive, your settings and history are synchronised too.

To start completely fresh, exit CalendarShot and delete `Documents\CalendarShot`.

## Troubleshooting

**The layout could not be recognised.**
Check that the weekday names and dates are visible and not cut off, and that the calendar was in front when the countdown ended. Try a larger window or zoom. You can still add events by hand in the review window.

**Dates are shifted by a day or a week.**
Make sure the month and year title is visible in the screenshot. If it is not, CalendarShot works out the month from the day numbers and today's date, which can be ambiguous. Correct the dates in the review window, or capture again with the title visible.

**Times are wrong or missing.**
Month views often show only start times; the default event length is used for the end. In week views without printed times, start times are estimated from the event's position and a warning is shown. Check these in the review window.

**"Windows text recognition is not available."**
Add a display language with text recognition support in **Settings > Time & language > Language & region**, then restart CalendarShot. You can also choose the language explicitly under **Text recognition language**.

**The reminder never appears.**
Check that reminders are on and today is one of the selected days. The reminder appears only once per day. If you already captured, skipped or closed it today, it returns on the next reminder day.

**CalendarShot does not start with Windows.**
Make sure **Start CalendarShot when I sign in to Windows** is ticked. Also check **Task Manager > Startup apps**, where CalendarShot must be *Enabled*. If you moved `CalendarShot.exe`, start it once from the new location and the startup entry is updated automatically.

**The wrong monitor was captured.**
CalendarShot captures the monitor under the mouse pointer when the countdown ends. Move the pointer onto the calendar's monitor, or use **Import screenshot from clipboard**.

If a problem persists, look at the most recent file in `Documents\CalendarShot\logs` and include the relevant lines when you report an issue. Please do not post screenshots of your real calendar.

## Updating and uninstalling

**Updating:** exit CalendarShot from the tray menu, replace `CalendarShot.exe` with the new version in the same folder, and start it again. Your settings and history are kept.

**Uninstalling:**

1. Open **Settings…** and untick **Start CalendarShot when I sign in to Windows**, then click **Save**. This removes the startup entry.
2. Exit CalendarShot from the tray menu.
3. Delete the folder that contains `CalendarShot.exe`.
4. Optional: delete `Documents\CalendarShot` to remove settings, history and logs. Exported `.ics` files in your export folder are not affected.

## Privacy

- CalendarShot makes **no network connections**. There is no telemetry, no update check and no cloud service.
- Text recognition uses the engine built into Windows and runs on your PC.
- Screenshots are kept in memory and discarded after each capture, unless you turn on **Keep captured screenshots**.
- Logs never contain event titles or notes.
- Your calendar data is stored only in `Documents\CalendarShot` and in the export folder you chose.

Make sure your use of CalendarShot is consistent with your organisation's policies on handling calendar information.
