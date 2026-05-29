package com.enrollment;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;

/**
 * CalendarService
 *
 * Creates a calendar event on the signed-in user's own Outlook calendar
 * using the Microsoft Graph API with a delegated access token
 * (Calendars.ReadWrite scope — no admin consent required).
 *
 * The event subject will be:
 *   "CPR Certification (BLS) — [Instructor Name]"
 *
 * No attendees are added since we only have the instructor name (not email).
 * To add attendees later, add their email to the attendees array in buildJson().
 */
public class CalendarService {

    // Graph API endpoint — /me/events writes to the signed-in user's calendar
    private static final String GRAPH_EVENTS_URL =
            "https://graph.microsoft.com/v1.0/me/events";

    /**
     * @param httpClient     Shared HttpClient instance
     * @param accessToken    Delegated token from device code flow (Calendars.ReadWrite)
     * @param instructorName Parsed from the email greeting, e.g. "John"
     * @param startDateTime  ISO-8601 local time, e.g. "2025-06-15T09:00:00"
     * @param endDateTime    ISO-8601 local time, e.g. "2025-06-15T11:00:00"
     * @param timeZone       IANA timezone,       e.g. "America/Los_Angeles"
     */
    public static void createEvent(
            HttpClient httpClient,
            String accessToken,
            String instructorName,
            String startDateTime,
            String endDateTime,
            String timeZone
    ) throws Exception {

        String subject = "CPR Certification (BLS)"
                + (instructorName != null ? " — " + instructorName : "");

        String bodyContent = "Enrollment notification received from AHA Atlas.\n"
                + (instructorName != null ? "Instructor: " + instructorName + "\n" : "")
                + "Start: " + startDateTime + "\n"
                + "End: " + endDateTime;

        String json = buildJson(subject, bodyContent, startDateTime, endDateTime, timeZone);

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(GRAPH_EVENTS_URL))
                .header("Authorization", "Bearer " + accessToken)
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(json))
                .build();

        HttpResponse<String> response =
                httpClient.send(request, HttpResponse.BodyHandlers.ofString());

        if (response.statusCode() == 201) {
            System.out.println("   Calendar event created: \"" + subject + "\"");
        } else {
            System.out.println("   Failed to create event — HTTP " + response.statusCode());
            System.out.println("   " + response.body());
        }
    }

    // -----------------------------------------------------------------------
    // Builds the JSON payload for the Graph API POST /me/events
    // -----------------------------------------------------------------------
    private static String buildJson(
            String subject,
            String bodyContent,
            String startDateTime,
            String endDateTime,
            String timeZone
    ) {
        return "{"
                + "\"subject\": \""  + escapeJson(subject)      + "\","
                + "\"body\": {"
                +   "\"contentType\": \"text\","
                +   "\"content\": \"" + escapeJson(bodyContent) + "\""
                + "},"
                + "\"start\": {"
                +   "\"dateTime\": \"" + startDateTime + "\","
                +   "\"timeZone\": \"" + timeZone      + "\""
                + "},"
                + "\"end\": {"
                +   "\"dateTime\": \"" + endDateTime   + "\","
                +   "\"timeZone\": \"" + timeZone      + "\""
                + "},"
                // showAs: "free" so the event doesn't block your calendar
                + "\"showAs\": \"free\","
                + "\"isReminderOn\": true,"
                + "\"reminderMinutesBeforeStart\": 15"
                + "}";
    }

    // -----------------------------------------------------------------------
    // Escapes characters that would break a JSON string value
    // -----------------------------------------------------------------------
    private static String escapeJson(String s) {
        if (s == null) return "";
        return s.replace("\\", "\\\\")
                .replace("\"", "\\\"")
                .replace("\n", "\\n")
                .replace("\r", "\\r")
                .replace("\t", "\\t");
    }
}
