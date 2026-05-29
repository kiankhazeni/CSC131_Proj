package main;

import com.google.api.client.http.javanet.NetHttpTransport;
import com.google.api.client.json.jackson2.JacksonFactory;

import com.google.api.services.sheets.v4.Sheets;
import com.google.api.services.sheets.v4.SheetsScopes;
import com.google.api.services.sheets.v4.model.ValueRange;

import com.google.auth.oauth2.GoogleCredentials;
import com.google.auth.http.HttpCredentialsAdapter;

import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.FileReader;
import java.util.*;

public class SheetsUploader {

    public static Sheets getSheetsService(AppConfig config) throws Exception {

        String credentialFile = config.getRequired("google.credentialsFile");

        GoogleCredentials credentials = GoogleCredentials
                .fromStream(new FileInputStream(credentialFile))
                .createScoped(Collections.singleton(SheetsScopes.SPREADSHEETS));

        return new Sheets.Builder(
                new NetHttpTransport(),
                JacksonFactory.getDefaultInstance(),
                new HttpCredentialsAdapter(credentials))
                .setApplicationName("Email Parser")
                .build();
    }

    public static List<List<Object>> readCSV(String filePath, boolean includeHeader) throws Exception {
        List<List<Object>> rows = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(filePath))) {
            String line;
            boolean firstLine = true;

            while ((line = br.readLine()) != null) {
                if (firstLine && !includeHeader) {
                    firstLine = false;
                    continue;
                }
                firstLine = false;

                // Split CSV by comma (handles quoted commas)
                String[] cells = line.split(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", -1);
                List<Object> row = new ArrayList<>();
                for (String cell : cells) {
                    if (cell.startsWith("\"") && cell.endsWith("\"")) {
                        cell = cell.substring(1, cell.length() - 1);
                    }
                    row.add(cell);
                }
                rows.add(row);
            }
        }
        return rows;
    }

    public static void uploadCSV(
            String spreadsheetId,
            String csvFilePath,
            AppConfig config,
            int keyColumnIndex,
            String sheetName
    ) throws Exception {
        Sheets service = getSheetsService(config);

        List<List<Object>> csvRows = readCSV(csvFilePath, true);

        if (csvRows.isEmpty()) {
            return;
        }

        int columnCount = csvRows.get(0).size();

        // Read current sheet data
        ValueRange existingResponse = service.spreadsheets()
                .values()
                .get(spreadsheetId, sheetName + "!A:Z")
                .execute();

        List<List<Object>> existingRows = existingResponse.getValues();

        if (existingRows == null || existingRows.isEmpty()) {
            ValueRange body = new ValueRange().setValues(csvRows);

            service.spreadsheets().values()
                    .append(spreadsheetId, sheetName + "!A1", body)
                    .setValueInputOption("USER_ENTERED")
                    .execute();

            System.out.println("Initialized Google Sheet from " + csvFilePath);
            return;
        }

        Map<String, Integer> keyToSheetRowNumber = new HashMap<>();

        for (int i = 1; i < existingRows.size(); i++) {
            List<Object> row = existingRows.get(i);

            if (row.size() > keyColumnIndex) {
                String key = normalizeKey(row.get(keyColumnIndex));

                if (!key.isBlank()) {
                    keyToSheetRowNumber.put(key, i + 1);
                }
            }
        }

        int updatedRows = 0;
        int appendedRows = 0;

        List<List<Object>> rowsToAppend = new ArrayList<>();

        for (int i = 1; i < csvRows.size(); i++) {
            List<Object> csvRow = normalizeRowLength(csvRows.get(i), columnCount);

            if (csvRow.size() <= keyColumnIndex) {
                continue;
            }

            String key = normalizeKey(csvRow.get(keyColumnIndex));

            if (key.isBlank()) {
                continue;
            }

            Integer sheetRowNumber = keyToSheetRowNumber.get(key);

            if (sheetRowNumber == null) {
                rowsToAppend.add(csvRow);
                keyToSheetRowNumber.put(key, -1);
                appendedRows++;
                continue;
            }

            List<Object> existingRow = getExistingRow(existingRows, sheetRowNumber, columnCount);
            List<Object> mergedRow = new ArrayList<>(existingRow);

            boolean changed = fillBlanksOnly(mergedRow, csvRow);

            if (changed) {
                ValueRange updateBody = new ValueRange()
                        .setValues(Collections.singletonList(mergedRow));

                service.spreadsheets().values()
                        .update(
                                spreadsheetId,
                                sheetName + "!A" + sheetRowNumber + ":" + columnLetter(columnCount) + sheetRowNumber,
                                updateBody
                        )
                        .setValueInputOption("USER_ENTERED")
                        .execute();

                updatedRows++;
            }
        }

        if (!rowsToAppend.isEmpty()) {
            ValueRange appendBody = new ValueRange().setValues(rowsToAppend);

            service.spreadsheets().values()
                    .append(spreadsheetId, sheetName + "!A1", appendBody)
                    .setValueInputOption("USER_ENTERED")
                    .execute();
        }

        System.out.println("Synced " + csvFilePath + " to Google Sheets: updated " + updatedRows + ", appended " + appendedRows);
    }

    private static List<Object> getExistingRow(
            List<List<Object>> existingRows,
            int sheetRowNumber,
            int columnCount
    ) {
        int listIndex = sheetRowNumber - 1;

        if (listIndex < 0 || listIndex >= existingRows.size()) {
            return blankRow(columnCount);
        }

        return normalizeRowLength(existingRows.get(listIndex), columnCount);
    }

    private static List<Object> normalizeRowLength(List<Object> row, int columnCount) {
        List<Object> normalized = new ArrayList<>();

        for (int i = 0; i < columnCount; i++) {
            if (row != null && i < row.size() && row.get(i) != null) {
                normalized.add(row.get(i));
            } else {
                normalized.add("");
            }
        }

        return normalized;
    }

    private static List<Object> blankRow(int columnCount) {
        List<Object> row = new ArrayList<>();

        for (int i = 0; i < columnCount; i++) {
            row.add("");
        }

        return row;
    }

    private static boolean fillBlanksOnly(List<Object> existingRow, List<Object> csvRow) {
        boolean changed = false;

        for (int i = 0; i < existingRow.size() && i < csvRow.size(); i++) {
            if (isBlank(existingRow.get(i)) && !isBlank(csvRow.get(i))) {
                existingRow.set(i, csvRow.get(i));
                changed = true;
            }
        }

        return changed;
    }

    private static boolean isBlank(Object value) {
        return value == null || value.toString().trim().isEmpty();
    }

    private static String normalizeKey(Object value) {
        if (value == null) {
            return "";
        }

        return value.toString()
                .replace("\"", "")
                .trim()
                .toLowerCase();
    }

    private static String columnLetter(int columnNumber) {
        StringBuilder result = new StringBuilder();

        while (columnNumber > 0) {
            columnNumber--;
            result.insert(0, (char) ('A' + (columnNumber % 26)));
            columnNumber /= 26;
        }

        return result.toString();
    }
}