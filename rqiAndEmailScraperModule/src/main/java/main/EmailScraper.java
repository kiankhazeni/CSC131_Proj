package main;

import org.jsoup.Jsoup;

import java.io.*;
import java.util.*;
import java.util.regex.*;
import java.text.SimpleDateFormat;
import java.text.ParseException;
import java.util.Date;

public class EmailScraper {

    private final AppConfig config;

    private final String msgIdsFile;
    private final String outputFile;
    private final String outputRqiCsvFile;
    private final String outputAhaCsvFile;
    private final String spreadsheetAha;
    private final String spreadsheetRqi;
    private final String sheetNameAha;
    private final String sheetNameRqi;

    public EmailScraper(AppConfig config) {
        this.config = config;

        this.msgIdsFile         = config.getRequired("file.msgIds");

        this.outputFile         = config.getRequired("file.emailDump");
        this.outputRqiCsvFile   = config.getRequired("file.rqiCsv");
        this.outputAhaCsvFile   = config.getRequired("file.ahaCsv");

        this.spreadsheetAha     = config.getRequired("google.spreadsheetAha");
        this.spreadsheetRqi     = config.getRequired("google.spreadsheetRqi");

        this.sheetNameAha       = config.getRequired("google.sheetNameAha");
        this.sheetNameRqi       = config.getRequired("google.sheetNameRqi");
    }

    private static final String[] RQI_HEADER = {
            "LocationID",
            "LocationName",
            "UserID",
            "FirstName",
            "MiddleName",
            "LastName",
            "Email",
            "JobCode",
            "JobName",
            "HireDate",
            "Status",
            "DateOfBirth",
            "Gender",
            "YearsofExperiences",
            "ActiveDate",
            "InactiveDate",
            "Group"
    };

    private static final String[] AHA_HEADER = {
            "EMAIL",
            "First Name",
            "M",
            "Last Name",
            "Phone",
            "Course",
            "Date",
            "Acuity Regist.",
            "AHA Regist.",
            "Reminder email sent"
    };

    public static class ProcessResult {
        private final Set<String> updatedProcessedIds;
        private final int newRowsWritten;

        public ProcessResult(Set<String> updatedProcessedIds, int newRowsWritten) {
            this.updatedProcessedIds = updatedProcessedIds;
            this.newRowsWritten = newRowsWritten;
        }

        public Set<String> getUpdatedProcessedIds() {
            return updatedProcessedIds;
        }

        public int writeChangedRows() {
            return newRowsWritten;
        }
    }

    // =================================
    //    Message Processing
    // =================================
    public Set<String> readProcessedMessageIds() {
        Set<String> ids = new HashSet<>();

        File file = new File(msgIdsFile);
        if (!file.exists()) {
            return ids;
        }

        try (BufferedReader reader = new BufferedReader(new FileReader(file))) {
            String line;
            while ((line = reader.readLine()) != null) {
                if (!line.isBlank()) {
                    ids.add(line.trim());
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        }

        return ids;
    }

    public void writeProcessedMessageIds(Set<String> ids) {
        try (BufferedWriter writer = new BufferedWriter(new FileWriter(msgIdsFile, false))) {
            List<String> sortedIds = new ArrayList<>(ids);
            Collections.sort(sortedIds);

            for (String id : sortedIds) {
                writer.write(id);
                writer.newLine();
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public ProcessResult processMessages(
            List<OutlookEmailMessage> messages,
            Set<String> processedIds
    ) throws Exception {
        int changedRows = 0;

        Set<String> updatedProcessedIds = new HashSet<>(processedIds);

        List<String[]> rqiRows = new ArrayList<>();
        List<String[]> ahaRows = new ArrayList<>();

        try (BufferedWriter writer = new BufferedWriter(new FileWriter(outputFile, true))) {

            for (OutlookEmailMessage msg : messages) {
                String messageId = msg.getStableId();

                if (messageId == null || messageId.isBlank()) {
                    continue;
                }

                if (updatedProcessedIds.contains(messageId)) {
                    continue;
                }

                String from = msg.getFrom() != null ? msg.getFrom() : "[Unknown]";
                String subject = msg.getSubject() != null ? msg.getSubject() : "[No Subject]";

                // Mark non-appointment messages as seen so they're not checked every run
                if (!subject.toLowerCase().contains("appointment")) {
                    updatedProcessedIds.add(messageId);
                    continue;
                }

                String received = msg.getReceived() != null ? msg.getReceived() : "[No Date]";
                String body = getTextFromGraphBody(msg.getBody());

                String name         = extractNameField(body);
                String[] nameParts  = extractName(name);
                String phone        = extractPhone(body);
                String course       = extractCourse(body);
                String email        = extractEmailAddress(body);
                String date         = extractDate(body);
                String location     = extractLocation(body);
                String group        = extractGroup(body);
                String acuity       = extractAcuity(body);

                // Ignore empty
                if (email.isBlank()) {
                    System.out.println("=================================");
                    System.out.println("Skipping message because no email address was found");

                    updatedProcessedIds.add(messageId);

                    continue;
                }

                // Print console output
                System.out.println("=================================");
                System.out.println("Message ID: " + messageId);
                System.out.println("From: " + from);
                System.out.println("Subject: " + subject);
                System.out.println("Received: " + received);

                // Save raw text
                writer.write("Message ID: " + messageId + "\n");
                writer.write("From: " + from + "\n");
                writer.write("Subject: " + subject + "\n");
                writer.write("Received: " + received + "\n");
                writer.write("=================================\n");

                // Save CSVs
                rqiRows.add(createRqiRow(location, email, nameParts, group));
                ahaRows.add(createAhaRow(email, nameParts, phone, course, date, acuity));

                updatedProcessedIds.add(messageId);
            }
        }

        if (!rqiRows.isEmpty() || !ahaRows.isEmpty()) {
            System.out.println("=================================");
        }

        changedRows += updateCsv(outputRqiCsvFile, RQI_HEADER, rqiRows, 2);
        changedRows += updateCsv(outputAhaCsvFile, AHA_HEADER, ahaRows, 0);

        return new ProcessResult(updatedProcessedIds, changedRows);
    }

    private String getTextFromGraphBody(String body) {
        if (body == null || body.isBlank()) {
            return "";
        }

        // Graph isn't supposed to return HTML, but if it does, add line breaks
        String withBreaks = body
                .replaceAll("(?i)<br\\s*/?>", "\n")
                .replaceAll("(?i)</p>", "\n")
                .replaceAll("(?i)</div>", "\n")
                .replaceAll("(?i)</tr>", "\n")
                .replaceAll("(?i)</li>", "\n");

        String text = Jsoup.parse(withBreaks).text();

        return normalizeBodyForParsing(text);
    }

    private String normalizeBodyForParsing(String text) {
        if (text == null) {
            return "";
        }

        String normalized = text.replace('\u00A0', ' ');

        String labels =
                "Name|Phone|Email|Price|Paid Online|Location|Address|" +
                        "Street Address Line 1|Street Address Line 2|City|State|ZIP|" +
                        "Cancellation/Rescheduling info|Certificate Code";

        normalized = normalized.replaceAll(
                "(?i)\\b(" + labels + ")\\s*=+\\s*",
                "\n$1: "
        );

        normalized = normalized.replaceAll(
                "(?i)\\s+(" + labels + ")\\s*:",
                "\n$1:"
        );

        normalized = normalized
                .replaceAll("\\r\\n?", "\n")
                .replaceAll("[ \\t]+", " ")
                .replaceAll(" *\\n+ *", "\n")
                .trim();

        return normalized;
    }

    // =================================
    //    Email Parsing
    // =================================
    private String extractField(String text, String label) {
        if (text == null || text.isBlank()) {
            return "";
        }

        String cleanLabel = label.replace(":", "").trim();

        String stopLabels =
                "Name|Phone|Email|Price|Paid Online|Location|Address|" +
                        "Street Address Line 1|Street Address Line 2|City|State|ZIP|" +
                        "Cancellation/Rescheduling info|Certificate Code";

        Pattern pattern = Pattern.compile(
                "(?is)(?:^|\\n)" +
                        Pattern.quote(cleanLabel) +
                        "\\s*:\\s*" +
                        "(.*?)" +
                        "(?=\\n(?:" + stopLabels + ")\\s*:|$)"
        );

        Matcher matcher = pattern.matcher(text);

        return matcher.find() ? matcher.group(1).trim() : "";
    }

    private String extractNameField(String text) {
        String name = extractField(text, "Name");

        // Safety cleanup in case the body was still partly flattened.
        name = name.replaceAll(
                "(?i)\\b(Phone|Email|Price|Paid Online|Location|Address|Cancellation/Rescheduling info)\\b.*$",
                ""
        ).trim();

        return name;
    }

    private String extractEmailAddress(String text) {
        String emailField = extractField(text, "Email");

        if (emailField.isBlank()) {
            emailField = text;
        }

        Pattern pattern = Pattern.compile(
                "[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}",
                Pattern.CASE_INSENSITIVE
        );

        Matcher matcher = pattern.matcher(emailField);

        return matcher.find() ? matcher.group().trim() : "";
    }

    private String extractPhone(String text) {
        String phoneField = extractField(text, "Phone");

        if (phoneField.isBlank()) {
            phoneField = text;
        }

        Pattern pattern = Pattern.compile(
                "(\\+?\\d[\\d\\s().-]{7,}\\d)"
        );

        Matcher matcher = pattern.matcher(phoneField);

        if (!matcher.find()) {
            return "";
        }

        String phone = matcher.group(1).trim();

        // Normalize phone number while preserving leading +
        if (phone.startsWith("+")) {
            return "+" + phone.substring(1).replaceAll("\\D", "");
        }

        return phone.replaceAll("\\D", "");
    }

    private String[] extractName(String text) {

        if (text == null || text.isEmpty())
            return new String[]{"","",""};

        String[] parts = text.trim().split("\\s+");

        String first = parts[0];
        String last = parts[parts.length - 1];
        String middle = "";

        if (parts.length > 2) {
            StringBuilder m = new StringBuilder();
            for (int i = 1; i < parts.length - 1; i++) {
                m.append(parts[i]).append(" ");
            }
            middle = m.toString().trim();
        }

        return new String[]{first, middle, last};
    }

    private String extractLocation(String text) {

        if (text == null) return "";

        if (text.contains("Film")) return "TN Film";
        if (text.contains("Music")) return "TN Music";
        if (text.contains("Brentwood")) return "TN Brentwood";
        if (text.contains("Bartlett")) return "TN Bartlett";
        if (text.contains("Sycamore")) return "TN Sycamore";
        if (text.contains("Perkins")) return "TN Perkins";
        if (text.contains("Poplar")) return "TN Poplar";
        if (text.contains("Chamblee")) return "GA Chamblee";
        if (text.contains("Decatur")) return "GA Decatur";
        if (text.contains("Exchange")) return "GA Exchange";

        return "";
    }

    private String extractGroup(String text) {

        if (text == null) return "";

        if (text.contains("ACLS") && text.contains("Skills")) return "HeartCode ACLS Skills - 2025";

        if (text.contains("ACLS")) return "HeartCode ACLS Complete - 2025";

        if (text.contains("BLS") && text.contains("Skills")) return "HeartCode BLS Skills - 2025";

        if (text.contains("BLS")) return "HeartCode BLS Complete - 2025";

        if (text.contains("PALS") && text.contains("Skills")) return "HeartCode PALS Skills - 2025";

        if (text.contains("PALS")) return "HeartCode PALS Complete - 2025";

        return "";
    }

    private String extractCourse(String text) {

        if (text == null) return "";

        if (text.contains("ACLS")) return "ACLS";

        if (text.contains("BLS")) return "BLS";

        if (text.contains("PALS")) return "PALS";

        return "";
    }

    private String extractDate(String text) {
        Pattern pattern = Pattern.compile("([A-Za-z]+ \\d{1,2}, \\d{4})");
        Matcher matcher = pattern.matcher(text);

        // Reformat to m/d/yyyy
        if (matcher.find()) {
            String rawDate = matcher.group(1).trim();
            try {
                SimpleDateFormat inputFormat = new SimpleDateFormat("MMMM d, yyyy", Locale.ENGLISH);
                Date date = inputFormat.parse(rawDate);

                SimpleDateFormat outputFormat = new SimpleDateFormat("M/d/yyyy");
                return outputFormat.format(date);
            } catch (ParseException e) {
                return rawDate;
            }
        }
        return "";
    }

    private String extractAcuity(String text) {
        return (text.contains("Acuity Scheduling") || text.contains("acuityscheduling")) ? "YES" : "";
    }

    // =================================
    //    CSV Helpers
    // =================================
    private String escapeCsv(Object value) {
        if (value == null) {
            return "\"\"";
        }
        String text = value.toString().replace("\"", "\"\"");

        return "\"" + text + "\"";
    }

    private int updateCsv(
            String csvFilePath,
            String[] header,
            List<String[]> newRows,
            int keyColumnIndex
    ) throws IOException {

        if (newRows.isEmpty()) {
            return 0;
        }

        File file = new File(csvFilePath);
        File parent = file.getParentFile();

        if (parent != null) {
            parent.mkdirs();
        }

        List<String[]> rows = readCsvRows(file);
        ensureHeader(rows, header);

        Map<String, Integer> keyToRowIndex = new HashMap<>();

        for (int i = 1; i < rows.size(); i++) {
            String[] row = normalizeRowLength(rows.get(i), header.length);
            rows.set(i, row);

            String key = normalizeKey(row[keyColumnIndex]);

            if (!key.isBlank()) {
                keyToRowIndex.put(key, i);
            }
        }

        int changes = 0;

        for (String[] newRowRaw : newRows) {
            String[] newRow = normalizeRowLength(newRowRaw, header.length);
            String key = normalizeKey(newRow[keyColumnIndex]);

            if (key.isBlank()) {
                continue;
            }

            Integer existingIndex = keyToRowIndex.get(key);

            if (existingIndex == null) {
                rows.add(newRow);
                keyToRowIndex.put(key, rows.size() - 1);
                changes++;
                continue;
            }

            String[] existingRow = rows.get(existingIndex);

            boolean changed = fillBlanksOnly(existingRow, newRow);

            if (changed) {
                changes++;
            }
        }

        writeCsvRows(file, rows);

        System.out.println("Updated " + changes + " row(s) in " + csvFilePath);

        return changes;
    }

    private String[] createRqiRow(
            String location,
            String email,
            String[] nameParts,
            String group
    ) {
        return new String[] {
                "",                 // LocationID
                location,           // LocationName
                email,              // UserID
                nameParts[0],       // FirstName
                nameParts[1],       // MiddleName
                nameParts[2],       // LastName
                email,              // Email
                "",                 // JobCode
                "",                 // JobName
                "",                 // HireDate
                "Active",           // Status
                "",                 // DateOfBirth
                "",                 // Gender
                "",                 // YearsofExperiences
                "",                 // ActiveDate
                "",                 // InactiveDate
                group               // Group
        };
    }

    private String[] createAhaRow(
            String email,
            String[] nameParts,
            String phone,
            String course,
            String date,
            String acuity
    ) {
        return new String[] {
                email,              // EMAIL
                nameParts[0],       // First Name
                nameParts[1],       // M
                nameParts[2],       // Last Name
                phone,              // Phone
                course,             // Course
                date,               // Date
                acuity,             // Acuity Regist.
                "",                 // AHA Regist.
                ""                  // Reminder email sent
        };
    }

    private boolean fillBlanksOnly(String[] existingRow, String[] newRow) {
        boolean changed = false;

        for (int i = 0; i < existingRow.length && i < newRow.length; i++) {
            if (isBlank(existingRow[i]) && !isBlank(newRow[i])) {
                existingRow[i] = newRow[i];
                changed = true;
            }
        }

        return changed;
    }

    private void ensureHeader(List<String[]> rows, String[] header) {
        if (rows.isEmpty()) {
            rows.add(header);
            return;
        }

        String firstCell = rows.get(0).length > 0 ? rows.get(0)[0] : "";

        if (!firstCell.equalsIgnoreCase(header[0])) {
            rows.add(0, header);
        } else {
            rows.set(0, normalizeRowLength(rows.get(0), header.length));
        }
    }

    private List<String[]> readCsvRows(File file) throws IOException {
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

    private void writeCsvRows(File file, List<String[]> rows) throws IOException {
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

        for (String cell : row) {
            escaped.add(escapeCsv(cell));
        }

        return String.join(",", escaped);
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

    private String[] normalizeRowLength(String[] row, int length) {
        String[] normalized = new String[length];

        for (int i = 0; i < length; i++) {
            normalized[i] = i < row.length && row[i] != null ? row[i] : "";
        }

        return normalized;
    }

    private boolean isBlank(String value) {
        return value == null || value.trim().isEmpty();
    }

    private String normalizeKey(String value) {
        if (value == null) {
            return "";
        }

        return value.trim().toLowerCase();
    }

    // =================================
    //    Sheets/RQI Upload
    // =================================
    // Merge CSV temp files and upload to Google Sheets
    public void syncCsvsToSheets() throws Exception {
        SheetsUploader.uploadCSV(spreadsheetRqi, outputRqiCsvFile, config, 2, sheetNameRqi);
        SheetsUploader.uploadCSV(spreadsheetAha, outputAhaCsvFile, config, 0, sheetNameAha);
    }
}
