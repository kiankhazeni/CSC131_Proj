package AHAautomation;

import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.regex.Pattern;

public class AhaCsvWriter {

    private static final String[] HEADER = {
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

    private static final int EMAIL_COL = 0;
    private static final int FIRST_NAME_COL = 1;
    private static final int MIDDLE_COL = 2;
    private static final int LAST_NAME_COL = 3;
    private static final int PHONE_COL = 4;
    private static final int COURSE_COL = 5;
    private static final int DATE_COL = 6;
    private static final int ACUITY_COL = 7;
    private static final int AHA_REGIST_COL = 8;
    private static final int REMINDER_SENT_COL = 9;

    public static void insertStudents(String csvFilePath, List<AhaScraper.StudentInfo> students) throws IOException {
        Path path = Paths.get(csvFilePath);

        if (path.getParent() != null) {
            Files.createDirectories(path.getParent());
        }

        List<String[]> rows = readCsv(path);
        ensureHeader(rows);

        Map<String, Integer> emailToRowIndex = buildEmailIndex(rows);

        int appended = 0;
        int updated = 0;

        for (AhaScraper.StudentInfo student : students) {
            String emailKey = normalizeEmail(student.email);

            if (emailKey.isBlank()) {
                System.out.println("Skipped CSV row with no email: " + student);
                continue;
            }

            Integer existingIndex = emailToRowIndex.get(emailKey);

            if (existingIndex != null) {
                String[] row = rows.get(existingIndex);
                updateExistingRow(row, student);
                updated++;
                System.out.println("Updated aha.csv row for: " + student.email);
            } else {
                String[] row = createNewRow(student);
                rows.add(row);
                emailToRowIndex.put(emailKey, rows.size() - 1);
                appended++;
                System.out.println("Appended aha.csv row for: " + student.email);
            }
        }

        writeCsv(path, rows);

        System.out.println("aha.csv update complete. Updated: " + updated + ", appended: " + appended);
    }

    private static List<String[]> readCsv(Path path) throws IOException {
        List<String[]> rows = new ArrayList<>();

        if (!Files.exists(path)) {
            return rows;
        }

        try (BufferedReader reader = Files.newBufferedReader(path)) {
            String line;

            while ((line = reader.readLine()) != null) {
                rows.add(parseCsvLine(line));
            }
        }

        return rows;
    }

    private static void ensureHeader(List<String[]> rows) {
        if (rows.isEmpty()) {
            rows.add(HEADER);
            return;
        }

        String[] firstRow = normalizeRowLength(rows.get(0));

        if (!"EMAIL".equalsIgnoreCase(firstRow[EMAIL_COL].replace("\"", "").trim())) {
            rows.add(0, HEADER);
        } else {
            rows.set(0, firstRow);
        }
    }

    private static Map<String, Integer> buildEmailIndex(List<String[]> rows) {
        Map<String, Integer> emailToRowIndex = new HashMap<>();

        for (int i = 1; i < rows.size(); i++) {
            String[] row = normalizeRowLength(rows.get(i));
            rows.set(i, row);

            String email = normalizeEmail(row[EMAIL_COL]);

            if (!email.isBlank()) {
                emailToRowIndex.put(email, i);
            }
        }

        return emailToRowIndex;
    }

    private static void updateExistingRow(String[] row, AhaScraper.StudentInfo student) {
        row = normalizeRowLengthInPlace(row);

        // Update if not empty (missing rows are handled by Rqi Email Utility)
        if (!student.email.isBlank()) {
            row[EMAIL_COL] = student.email.trim();
        }

        if (!student.firstName.isBlank()) {
            row[FIRST_NAME_COL] = student.firstName.trim();
        }

        if (!student.lastName.isBlank()) {
            row[LAST_NAME_COL] = student.lastName.trim();
        }

        if (!student.phone.isBlank()) {
            row[PHONE_COL] = normalizePhone(student.phone);
        }

        if (!student.date.isBlank()) {
            row[DATE_COL] = student.date.trim();
        }

        row[AHA_REGIST_COL] = "YES";
    }

    private static String[] createNewRow(AhaScraper.StudentInfo student) {
        String[] row = new String[HEADER.length];

        Arrays.fill(row, "");

        row[EMAIL_COL] = student.email.trim();
        row[FIRST_NAME_COL] = student.firstName.trim();
        row[MIDDLE_COL] = "";
        row[LAST_NAME_COL] = student.lastName.trim();
        row[PHONE_COL] = normalizePhone(student.phone);
        row[COURSE_COL] = "";
        row[DATE_COL] = student.date.trim();
        row[ACUITY_COL] = "";
        row[AHA_REGIST_COL] = "YES";
        row[REMINDER_SENT_COL] = "";

        return row;
    }

    private static void writeCsv(Path path, List<String[]> rows) throws IOException {
        try (BufferedWriter writer = Files.newBufferedWriter(path)) {
            for (String[] row : rows) {
                writer.write(toCsvLine(normalizeRowLength(row)));
                writer.newLine();
            }
        }
    }

    private static String[] normalizeRowLength(String[] row) {
        String[] normalized = new String[HEADER.length];

        for (int i = 0; i < HEADER.length; i++) {
            normalized[i] = i < row.length && row[i] != null ? row[i] : "";
        }

        return normalized;
    }

    private static String[] normalizeRowLengthInPlace(String[] row) {
        if (row.length == HEADER.length) {
            return row;
        }

        return normalizeRowLength(row);
    }

    private static String normalizeEmail(String email) {
        if (email == null) {
            return "";
        }

        return email.trim().toLowerCase();
    }

    private static String normalizePhone(String phone) {
        if (phone == null) {
            return "";
        }

        return phone.replaceAll("\\D", "");
    }

    private static String[] parseCsvLine(String line) {
        Pattern csvSplit = Pattern.compile(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        String[] cells = csvSplit.split(line, -1);

        for (int i = 0; i < cells.length; i++) {
            cells[i] = unescapeCsv(cells[i]);
        }

        return cells;
    }

    private static String toCsvLine(String[] row) {
        List<String> escaped = new ArrayList<>();

        for (String cell : row) {
            escaped.add(escapeCsv(cell));
        }

        return String.join(",", escaped);
    }

    private static String escapeCsv(String value) {
        if (value == null) {
            value = "";
        }

        return "\"" + value.replace("\"", "\"\"") + "\"";
    }

    private static String unescapeCsv(String value) {
        if (value == null) {
            return "";
        }

        String trimmed = value.trim();

        if (trimmed.startsWith("\"") && trimmed.endsWith("\"") && trimmed.length() >= 2) {
            trimmed = trimmed.substring(1, trimmed.length() - 1);
            trimmed = trimmed.replace("\"\"", "\"");
        }

        return trimmed;
    }
}