package com.enrollment;

import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.*;
import java.util.regex.*;

/**
 * EmailParser
 *
 * Extracts the instructor name and appointment date/time from the plain-text
 * body of an AHA "Incoming Enrollment Request" email.
 *
 * Handles date formats seen in AHA emails:
 *   "June 15, 2025"   →  2025-06-15
 *   "06/15/2025"      →  2025-06-15
 *   "2025-06-15"      →  2025-06-15
 *
 * Time extraction (optional — falls back to DEFAULT_START_TIME):
 *   "10:00 AM", "14:30", "09:00:00"
 */
public class EmailParser {

    // Used when no time is found in the email body
    private static final String DEFAULT_START_TIME    = "09:00:00";

    // How long a class session is assumed to last
    private static final int    DEFAULT_DURATION_HRS  = 2;

    // -----------------------------------------------------------------------
    // NAME
    // -----------------------------------------------------------------------

    /**
     * Extracts the instructor name from the greeting line.
     *
     * Looks for patterns like:
     *   "Dear John,"
     *   "Dear John Doe,"
     *   "Dear Dr. Smith"
     *
     * Returns null if no match.
     */
    public static String parseInstructorName(String bodyText) {
        // Grab everything after "Dear" up to a comma, period, or line end
        Pattern p = Pattern.compile(
                "Dear\\s+([A-Za-z][A-Za-z.'-]*(?:\\s+[A-Za-z][A-Za-z.'-]*)*)",
                Pattern.CASE_INSENSITIVE
        );
        Matcher m = p.matcher(bodyText);
        if (m.find()) {
            // Strip trailing punctuation
            return m.group(1).replaceAll("[,.]$", "").trim();
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // DATETIME
    // -----------------------------------------------------------------------

    /**
     * Returns "YYYY-MM-DDTHH:MM:SS" if a date (and optionally time) is found
     * in the email body. Returns null otherwise.
     */
    public static String parseStartDateTime(String bodyText) {
        String isoDate = tryMonthName(bodyText);      // "June 15, 2025"
        if (isoDate == null) isoDate = trySlash(bodyText);   // "06/15/2025"
        if (isoDate == null) isoDate = tryIso(bodyText);     // "2025-06-15"
        if (isoDate == null) return null;

        String time = extractTime(bodyText);
        return isoDate + "T" + (time != null ? time : DEFAULT_START_TIME);
    }

    /**
     * Adds DEFAULT_DURATION_HRS to a "YYYY-MM-DDTHH:MM:SS" string.
     * Returns null if parsing fails.
     */
    public static String buildEndDateTime(String startDateTime) {
        if (startDateTime == null) return null;
        try {
            SimpleDateFormat fmt = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss");
            Date start = fmt.parse(startDateTime);
            Calendar cal = Calendar.getInstance();
            cal.setTime(start);
            cal.add(Calendar.HOUR_OF_DAY, DEFAULT_DURATION_HRS);
            return fmt.format(cal.getTime());
        } catch (ParseException e) {
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static String tryMonthName(String text) {
        // Matches "June 15, 2025" or "June 15 2025"
        Matcher m = Pattern.compile("([A-Za-z]+ \\d{1,2},?\\s*\\d{4})").matcher(text);
        if (!m.find()) return null;
        String raw = m.group(1).replaceAll(",", "").trim();
        return parseDate(raw, "MMMM d yyyy", "MMM d yyyy");
    }

    private static String trySlash(String text) {
        Matcher m = Pattern.compile("(\\d{1,2}/\\d{1,2}/\\d{4})").matcher(text);
        if (!m.find()) return null;
        return parseDate(m.group(1), "M/d/yyyy");
    }

    private static String tryIso(String text) {
        Matcher m = Pattern.compile("(\\d{4}-\\d{2}-\\d{2})").matcher(text);
        if (!m.find()) return null;
        return parseDate(m.group(1), "yyyy-MM-dd");
    }

    private static String parseDate(String raw, String... formats) {
        SimpleDateFormat out = new SimpleDateFormat("yyyy-MM-dd");
        for (String fmt : formats) {
            try {
                SimpleDateFormat in = new SimpleDateFormat(fmt, Locale.ENGLISH);
                in.setLenient(false);
                return out.format(in.parse(raw));
            } catch (ParseException ignored) {}
        }
        return null;
    }

    private static String extractTime(String text) {
        // Matches "10:00 AM", "2:30 PM", "14:30", "09:00:00"
        Pattern p = Pattern.compile(
                "\\b(\\d{1,2}:\\d{2}(?::\\d{2})?\\s*(?:AM|PM)?)\\b",
                Pattern.CASE_INSENSITIVE
        );
        Matcher m = p.matcher(text);
        if (!m.find()) return null;

        String raw = m.group(1).trim().toUpperCase();
        try {
            if (raw.contains("AM") || raw.contains("PM")) {
                String pattern = raw.split(":").length == 3 ? "hh:mm:ss a" : "hh:mm a";
                SimpleDateFormat in  = new SimpleDateFormat(pattern, Locale.ENGLISH);
                SimpleDateFormat out = new SimpleDateFormat("HH:mm:ss");
                return out.format(in.parse(raw));
            }
            String pattern = raw.split(":").length == 3 ? "HH:mm:ss" : "HH:mm";
            SimpleDateFormat in  = new SimpleDateFormat(pattern);
            SimpleDateFormat out = new SimpleDateFormat("HH:mm:ss");
            return out.format(in.parse(raw));
        } catch (ParseException e) {
            return null;
        }
    }
}

