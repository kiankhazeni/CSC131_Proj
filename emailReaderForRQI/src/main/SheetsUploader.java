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

    private static final String CREDENTIAL_FILE = "src/main/resources/credentials.json";

    public static Sheets getSheetsService() throws Exception {

        GoogleCredentials credentials = GoogleCredentials
                .fromStream(new FileInputStream(CREDENTIAL_FILE))
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

    public static void uploadCSV(String spreadsheetId, String csvFilePath) throws Exception {
        Sheets service = getSheetsService();

        boolean emptySheet = isSheetEmpty(service, spreadsheetId);
        // Include header if empty sheet
        List<List<Object>> rows = readCSV(csvFilePath, emptySheet);

        if (rows.isEmpty()) return;

        ValueRange body = new ValueRange().setValues(rows);

        service.spreadsheets().values()
                .append(spreadsheetId, "Sheet1!A1", body)
                .setValueInputOption("USER_ENTERED")
                .execute();
    }

    // Check if the spreadsheet is empty
    private static boolean isSheetEmpty(Sheets service, String spreadsheetId) throws Exception {
        ValueRange response = service.spreadsheets().values().get(spreadsheetId, "Sheet1!A1:Z1").execute();
        List<List<Object>> values = response.getValues();
        return values == null || values.isEmpty();
    }

}