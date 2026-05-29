package com.enrollment;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashSet;
import java.util.Set;

/**
 * EnrollmentWatcher
 *
 * Polls the signed-in user's Outlook inbox for AHA enrollment emails.
 * When a new one is found it:
 *   1. Parses the instructor name + appointment date/time from the body.
 *   2. Creates a calendar event on the signed-in user's own calendar
 *      (delegated auth — no separate app credentials needed).
 *
 * Auth: Device Code Flow (user visits a URL and enters a short code once).
 * Token is refreshed silently on every poll cycle.
 */
public class EnrollmentWatcher {

    // -----------------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------------

    // Azure App Registration — must have Mail.Read + Calendars.ReadWrite
    // delegated permissions (no admin consent required for personal accounts).
    private static AppConfig config;

    private static int fetchTop; // How many recent messages to scan per poll

    private static boolean runContinuously;
    private static int runInterval; // Seconds between inbox polls

    private static String calendarTimeZone;
    private static String seenIdsFile;

    // Only process emails with this exact subject
    private static final String TARGET_SUBJECT =
            "Notification from Atlas: Incoming Enrollment Request";

    // -----------------------------------------------------------------------
    // STATE
    // -----------------------------------------------------------------------
    private static String accessToken;

    // Tracks email IDs we've already handled (avoids duplicate events)
    private static final Set<String> seenIds = new HashSet<>();

    // -----------------------------------------------------------------------
    // ENTRY POINT
    // -----------------------------------------------------------------------
    public static void main(String[] args) throws Exception {
        config = new AppConfig();
        loadConfig();
        readSeenIds();

        // 1. Authenticate once via device code flow
        accessToken = OutlookOAuth.getAccessToken(config);

        HttpClient httpClient = HttpClient.newHttpClient();

        System.out.println("Watching inbox for enrollment emails...");
        System.out.println("(Press Ctrl+C to stop)\n");

        // 2. Poll on a fixed interval
        while (true) {
            try {
                checkInbox(httpClient);
                writeSeenIds();
            } catch (Exception e) {
                System.out.println("Error during inbox check:");
                e.printStackTrace();
                System.out.println("Program will try again on the next cycle.");
            }
            if (!runContinuously) {
                System.out.println("Single-run mode complete. Exiting.");
                break;
            }

            System.out.printf("Next check in %d seconds...%n%n", runInterval);
            Thread.sleep(runInterval * 1_000L);
        }
    }

    // -----------------------------------------------------------------------
    // Config Helpers
    // -----------------------------------------------------------------------
    private static void loadConfig() {
        fetchTop = config.getRequiredInt("email.maxCount");

        runContinuously = config.getRequiredBoolean("calendar.runContinuously");
        runInterval = config.getRequiredInt("calendar.runInterval");

        calendarTimeZone = config.getRequired("calendar.timeZone");

        seenIdsFile = config.getRequired("calendarFile.seenIds");

        if (fetchTop <= 0) {
            throw new IllegalArgumentException("email.maxCount must be greater than 0.");
        }

        if (runInterval <= 0) {
            throw new IllegalArgumentException("calendar.runInterval must be greater than 0.");
        }
    }

    // -----------------------------------------------------------------------
    // INBOX POLLING
    // -----------------------------------------------------------------------
    private static void checkInbox(HttpClient httpClient) throws Exception {
        String url = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages"
                + "?$top=" + fetchTop
                + "&$orderby=receivedDateTime%20desc"
                + "&$select=id,subject,body";

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .header("Authorization", "Bearer " + accessToken)
                .header("Accept", "application/json")
                .header("Prefer", "outlook.body-content-type=\"text\"")
                .GET()
                .build();

        HttpResponse<String> response =
                httpClient.send(request, HttpResponse.BodyHandlers.ofString());

        if (response.statusCode() == 401) {
            System.out.println("Access token expired. Refreshing token and retrying...");

            accessToken = OutlookOAuth.getAccessToken(config);

            request = HttpRequest.newBuilder()
                    .uri(URI.create(url))
                    .header("Authorization", "Bearer " + accessToken)
                    .header("Accept", "application/json")
                    .header("Prefer", "outlook.body-content-type=\"text\"")
                    .GET()
                    .build();

            response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
        }

        if (response.statusCode() != 200) {
            System.out.println("Error reading inbox — HTTP " + response.statusCode());
            System.out.println(response.body());
            return;
        }

        JSONArray messages = new JSONObject(response.body()).getJSONArray("value");
        boolean foundNew   = false;

        for (int i = 0; i < messages.length(); i++) {
            JSONObject msg     = messages.getJSONObject(i);
            String     emailId = msg.optString("id", "");
            String     subject = msg.optString("subject", "");

            // Skip if wrong subject or already processed
            if (!TARGET_SUBJECT.equals(subject) || seenIds.contains(emailId)) {
                continue;
            }

            seenIds.add(emailId);
            foundNew = true;

            // Strip HTML tags
            String rawBody  = msg.getJSONObject("body").optString("content", "");
            String bodyText = rawBody.replaceAll("<[^>]*>", " ")
                    .replaceAll("\\s+", " ")
                    .trim();

            System.out.println("New enrollment email detected!");
            processEnrollment(httpClient, bodyText);
            System.out.println("════════════════════════════════");
        }

        if (!foundNew) {
            System.out.println("No new enrollment emails.");
        }
    }

    // Helpers to avoid re-reading read emails
    private static void readSeenIds() {
        Path path = Path.of(seenIdsFile);

        if (!Files.exists(path)) {
            return;
        }

        try {
            for (String line : Files.readAllLines(path)) {
                if (line != null && !line.isBlank()) {
                    seenIds.add(line.trim());
                }
            }

            System.out.println("Loaded " + seenIds.size() + " seen email ID(s).");
        } catch (IOException e) {
            System.out.println("Could not read seen IDs file: " + seenIdsFile);
            e.printStackTrace();
        }
    }
    private static void writeSeenIds() {
        Path path = Path.of(seenIdsFile);

        try {
            Path parent = path.getParent();

            if (parent != null) {
                Files.createDirectories(parent);
            }

            Files.write(path, seenIds.stream().sorted().toList());
        } catch (IOException e) {
            System.out.println("Could not write seen IDs file: " + seenIdsFile);
            e.printStackTrace();
        }
    }

    // -----------------------------------------------------------------------
    // PROCESS ONE ENROLLMENT EMAIL
    // -----------------------------------------------------------------------
    private static void processEnrollment(HttpClient httpClient, String bodyText) {

        // Parse instructor name and appointment datetime from email body
        String instructorName = EmailParser.parseInstructorName(bodyText);
        String startDateTime  = EmailParser.parseStartDateTime(bodyText);
        String endDateTime    = EmailParser.buildEndDateTime(startDateTime);

        System.out.println("   Instructor : " + (instructorName != null ? instructorName : "(not found)"));
        System.out.println("   Start      : " + (startDateTime  != null ? startDateTime  : "(not found)"));
        System.out.println("   End        : " + (endDateTime    != null ? endDateTime    : "(not found)"));

        if (startDateTime == null) {
            System.out.println("   Skipping calendar event — could not parse date.");
            return;
        }

        // Create the calendar event on the signed-in user's own calendar
        try {
            CalendarService.createEvent(
                    httpClient,
                    accessToken,
                    instructorName,
                    startDateTime,
                    endDateTime,
                    calendarTimeZone
            );
        } catch (Exception e) {
            System.out.println("   Error creating calendar event: " + e.getMessage());
        }
    }
}
