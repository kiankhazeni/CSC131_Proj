package reminder;

import com.microsoft.aad.msal4j.*;

import java.io.*;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.*;

public class RegistrationReminder {

    private static final int EMAIL_COL = 0;
    private static final int FIRST_NAME_COL = 1;
    private static final int ACUITY_REGIST_COL = 7;
    private static final int AHA_REGIST_COL = 8;
    private static final int REMINDER_SENT_COL = 9;

    private static final int COLUMN_COUNT = 10;

    private final AppConfig config;
    private final boolean dryRun;

    private final PublicClientApplication app;
    private final Set<String> scopes = Set.of("Mail.Send");

    public RegistrationReminder(AppConfig config) throws Exception {
        this.config = config;
        this.dryRun = config.getRequiredBoolean("reminder.dryRun");

        String clientId = config.getRequired("outlook.clientId");
        String tenantId = config.getRequired("outlook.tenantId");
        String authority = "https://login.microsoftonline.com/" + tenantId;
        String tokenCacheFile = config.getRequired("reminder.tokenCacheFile");

        this.app = PublicClientApplication
                .builder(clientId)
                .authority(authority)
                .setTokenCacheAccessAspect(new MsalFileTokenCache(tokenCacheFile))
                .build();
    }

    public void run() throws Exception {
        String ahaCsvFile = config.getRequired("file.ahaCsv");

        List<String[]> rows = readCsv(new File(ahaCsvFile));

        if (rows.size() <= 1) {
            System.out.println("No AHA rows found.");
            return;
        }

        String accessToken = dryRun ? "" : getAccessToken();
        HttpClient httpClient = HttpClient.newHttpClient();

        int remindersSent = 0;
        int remindersSkipped = 0;
        int reminderWouldSend = 0;

        for (int i = 1; i < rows.size(); i++) {
            String[] row = normalizeRow(rows.get(i));
            rows.set(i, row);

            if (!needsRegistrationReminder(row)) {
                remindersSkipped++;
                continue;
            }

            String email = row[EMAIL_COL].trim();
            String firstName = row[FIRST_NAME_COL].trim();

            if (firstName.isBlank()) {
                firstName = "there";
            }

            String subject = config.getRequired("reminder.registration.subject");
            String body = buildEmailBody(firstName);

            if (dryRun) {
                reminderWouldSend++;
                System.out.println("[DRY RUN] Would send registration reminder to: " + email);
            } else {
                sendEmail(httpClient, accessToken, email, subject, body);
                row[REMINDER_SENT_COL] = LocalDateTime.now().format(DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss"));
                remindersSent++;
                System.out.println("Sent registration reminder to: " + email);
            }
        }

        if (!dryRun && remindersSent > 0) {
            writeCsv(new File(ahaCsvFile), rows);
        }

        System.out.println("=================================");
        System.out.println("Registration reminder complete.");
        System.out.println("Dry run: " + dryRun);
        System.out.println("Would send: " + reminderWouldSend);
        System.out.println("Sent: " + remindersSent);
        System.out.println("Skipped: " + remindersSkipped);
        System.out.println("=================================");
    }

    private boolean needsRegistrationReminder(String[] row) {
        String email = row[EMAIL_COL].trim();
        String acuityRegist = row[ACUITY_REGIST_COL].trim();
        String ahaRegist = row[AHA_REGIST_COL].trim();
        String reminderSent = row[REMINDER_SENT_COL].trim();

        return !email.isBlank()
                && ahaRegist.equalsIgnoreCase("YES")
                && acuityRegist.isBlank()
                && reminderSent.isBlank();
    }

    private String getAccessToken() throws Exception {
        try {
            Set<IAccount> accounts = app.getAccounts().join();

            if (!accounts.isEmpty()) {
                IAccount account = accounts.iterator().next();

                IAuthenticationResult result = app.acquireTokenSilently(
                        SilentParameters.builder(scopes, account).build()
                ).join();

                return result.accessToken();
            }
        } catch (Exception e) {
            System.out.println("Silent sign-in failed. Device-code sign-in is required.");
        }

        DeviceCodeFlowParameters parameters = DeviceCodeFlowParameters
                .builder(scopes, deviceCode -> System.out.println(deviceCode.message()))
                .build();

        IAuthenticationResult result = app.acquireToken(parameters).get();

        return result.accessToken();
    }

    private void sendEmail(
            HttpClient httpClient,
            String accessToken,
            String to,
            String subject,
            String body
    ) throws Exception {

        String json = "{"
                + "\"message\":{"
                + "\"subject\":\"" + escapeJson(subject) + "\","
                + "\"body\":{"
                + "\"contentType\":\"Text\","
                + "\"content\":\"" + escapeJson(body) + "\""
                + "},"
                + "\"toRecipients\":["
                + "{"
                + "\"emailAddress\":{"
                + "\"address\":\"" + escapeJson(to) + "\""
                + "}"
                + "}"
                + "]"
                + "},"
                + "\"saveToSentItems\":true"
                + "}";

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create("https://graph.microsoft.com/v1.0/me/sendMail"))
                .header("Authorization", "Bearer " + accessToken)
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(json))
                .build();

        HttpResponse<String> response = httpClient.send(
                request,
                HttpResponse.BodyHandlers.ofString()
        );

        if (response.statusCode() != 202) {
            throw new RuntimeException(
                    "Failed to send email to " + to +
                            ". HTTP " + response.statusCode() +
                            ": " + response.body()
            );
        }
    }

    private String buildEmailBody(String firstName) {
        String templatePath = config.getRequired("reminder.registration.template");

        try {

            String template = new String(java.nio.file.Files.readAllBytes(java.nio.file.Paths.get(templatePath)));

            return template.replace("{firstName}", firstName);

        } catch (IOException e) {
            throw new RuntimeException("Could not load email template: " + templatePath, e);
        }
    }

    private List<String[]> readCsv(File file) throws IOException {
        List<String[]> rows = new ArrayList<>();

        if (!file.exists()) {
            return rows;
        }

        try (BufferedReader reader = new BufferedReader(new FileReader(file))) {
            String line;

            while ((line = reader.readLine()) != null) {
                rows.add(parseCsvLine(line));
            }
        }

        return rows;
    }

    private void writeCsv(File file, List<String[]> rows) throws IOException {
        try (BufferedWriter writer = new BufferedWriter(new FileWriter(file, false))) {
            for (String[] row : rows) {
                writer.write(toCsvLine(row));
                writer.newLine();
            }
        }
    }

    private String[] parseCsvLine(String line) {
        String[] cells = line.split(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", -1);

        for (int i = 0; i < cells.length; i++) {
            cells[i] = unescapeCsv(cells[i]);
        }

        return cells;
    }

    private String toCsvLine(String[] row) {
        List<String> escaped = new ArrayList<>();

        for (String cell : normalizeRow(row)) {
            escaped.add(escapeCsv(cell));
        }

        return String.join(",", escaped);
    }

    private String[] normalizeRow(String[] row) {
        String[] normalized = new String[COLUMN_COUNT];

        for (int i = 0; i < COLUMN_COUNT; i++) {
            normalized[i] = i < row.length && row[i] != null ? row[i] : "";
        }

        return normalized;
    }

    private String escapeCsv(String value) {
        if (value == null) {
            value = "";
        }

        return "\"" + value.replace("\"", "\"\"") + "\"";
    }

    private String unescapeCsv(String value) {
        if (value == null) {
            return "";
        }

        String text = value.trim();

        if (text.startsWith("\"") && text.endsWith("\"") && text.length() >= 2) {
            text = text.substring(1, text.length() - 1);
            text = text.replace("\"\"", "\"");
        }

        return text;
    }

    private String escapeJson(String value) {
        if (value == null) {
            return "";
        }

        return value
                .replace("\\", "\\\\")
                .replace("\"", "\\\"")
                .replace("\r", "")
                .replace("\n", "\\n");
    }
}