package main;

import jakarta.mail.*;
import jakarta.mail.internet.MimeMultipart;
import com.sun.mail.imap.IMAPFolder;
import org.jsoup.Jsoup;
import java.io.*;
import java.util.*;
import java.util.regex.*;
import java.text.SimpleDateFormat;
import java.text.ParseException;
import java.util.Calendar;
import java.util.Date;
import jakarta.mail.search.ReceivedDateTerm;
import jakarta.mail.search.SearchTerm;
import jakarta.mail.search.ComparisonTerm;

public class EmailScraper {
    private final String username;
    private final String password;

    private static final String UID_FILE                = "resources/last_uid.txt";
    private static final String OUTPUT_FILE             = "resources/email_dump.txt";
    private static final String OUTPUT_RQI_CSV_FILE     = "resources/preprod_cl.csv";
    private static final String OUTPUT_AHA_CSV_FILE     = "resources/aha.csv";
    private static final String OUTPUT_RQI_TEMP_FILE    = "resources/preprod_cl_temp.csv";
    private static final String OUTPUT_AHA_TEMP_FILE    = "resources/aha_temp.csv";
    private static final String SPREADSHEET_AHA_ID      = "1Mz9m5x8tLihEdpJfmv8NZYdYkxokqzCiGeZ48lB_c2Y";
    private static final String SPREADSHEET_RQI_ID      = "18tVdlycK7cC-KNFBU_GQza7ihqdvyyjV-Y_Dh4cd00k";

    public EmailScraper(String username, String password) {
        this.username = username;
        this.password = password;
    }

    // =================================
    //    Inbox Connection
    // =================================
    public IMAPFolder connectToInbox() throws Exception {
        Properties props = new Properties();
        props.put("mail.store.protocol", "imap");
        props.put("mail.imap.host", "imap.gmail.com");
        props.put("mail.imap.port", "993");
        props.put("mail.imap.ssl.enable", "true");

        Session session = Session.getInstance(props);
        Store store = session.getStore();
        store.connect(username, password);

        IMAPFolder inbox = (IMAPFolder) store.getFolder("INBOX");
        inbox.open(Folder.READ_ONLY);

        return inbox;
    }

    public Message[] fetchRecentMessages(IMAPFolder inbox, int maxMessages, int pastDays) throws Exception {
        Calendar cal = Calendar.getInstance();
        cal.add(Calendar.DAY_OF_MONTH, -pastDays);
        SearchTerm recent = new ReceivedDateTerm(ComparisonTerm.GE, cal.getTime());

        Message[] recentMessages = inbox.search(recent);

        if (recentMessages.length > maxMessages) {
            return Arrays.copyOfRange(recentMessages, recentMessages.length - maxMessages, recentMessages.length);
        } else {
            return recentMessages;
        }
    }

    // =================================
    //    Message Processing
    // =================================
    // Processes messages: writes CSVs and raw text output
    public long processMessages(IMAPFolder inbox, Message[] messages, long lastProcessedUID) throws Exception {

        long highestUID = lastProcessedUID;

        try (
                BufferedWriter writer       = new BufferedWriter(new FileWriter(OUTPUT_FILE, true));
                BufferedWriter csvWriterRQI = new BufferedWriter(new FileWriter(OUTPUT_RQI_TEMP_FILE, false));
                BufferedWriter csvWriterAHA = new BufferedWriter(new FileWriter(OUTPUT_AHA_TEMP_FILE, false))
        ) {
            // CSV headers
            csvWriterRQI.write("LocationID,LocationName,UserID,FirstName,MiddleName,LastName,Email,JobCode,JobName,HireDate,Status,DateOfBirth,Gender,YearsofExperiences,ActiveDate,InactiveDate,Group\n");
            csvWriterAHA.write("EMAIL,First Name,M,Last Name,Phone,Course,Date,Acuity Regist.,AHA Regist.,Reminder email sent\n");

            for (Message msg : messages) {

                // Skip processed messages
                long uid = inbox.getUID(msg);
                if (uid <= lastProcessedUID) continue;

                String from = (msg.getFrom() != null && msg.getFrom().length > 0) ? msg.getFrom()[0].toString() : "[Unknown]";
                String subject = msg.getSubject() != null ? msg.getSubject() : "[No Subject]";
                if (!subject.toLowerCase().contains("appointment")) continue;

                String received = msg.getReceivedDate() != null ? msg.getReceivedDate().toString() : "[No Date]";
                String body     = getTextFromMessage(msg);

                String name         = extractField(body, "Name: ");
                String[] nameParts  = extractName(name);
                String phone        = extractField(body, "Phone: ");
                String email        = extractField(body, "Email: ");
                String date         = extractDate(body);
                String location     = extractLocation(body);
                String group        = extractGroup(body);
                String acuity       = extractAcuity(body);

                // Print console output
                System.out.println("=================================");
                System.out.println("UID: " + uid);
                System.out.println("From: " + from);
                System.out.println("Subject: " + subject);
                System.out.println("Received: " + received);

                // Save raw text
                writer.write("=================================\n");
                writer.write("UID: " + uid + "\n");
                writer.write("From: " + from + "\n");
                writer.write("Subject: " + subject + "\n");
                writer.write("Received: " + received + "\n");
                writer.write("Body:\n" + body + "\n\n");

                // Save CSVs
                writeCSVLine(csvWriterRQI, location, email, nameParts, group);
                writeCSVLineAHA(csvWriterAHA, email, nameParts, phone, date, acuity);

                highestUID = Math.max(highestUID, uid);
            }
        }

        return highestUID;
    }

    // Track UID to avoid duplicates
    public long readLastUID() {
        try (BufferedReader reader = new BufferedReader(new FileReader(UID_FILE))) {
            return Long.parseLong(reader.readLine());
        } catch (Exception e) {
            return 0; // First run
        }
    }

    public void writeLastUID(long uid) {
        try (BufferedWriter writer = new BufferedWriter(new FileWriter(UID_FILE))) {
            writer.write(String.valueOf(uid));
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    // =================================
    //    Email Parsing
    // =================================
    // Checks content type and extracts text; returns "[Unknown Content Type]" if type not accounted for
    private String getTextFromMessage(Message message) throws Exception {

        if (message.isMimeType("text/plain")) {
            return message.getContent().toString();
        }

        if (message.isMimeType("text/html")) {
            String html = message.getContent().toString();
            return Jsoup.parse(html).text();
        }

        if (message.isMimeType("multipart/*")) {
            MimeMultipart mimeMultipart = (MimeMultipart) message.getContent();
            return getTextFromMimeMultipart(mimeMultipart);
        }

        return "[Unknown Content Type]";
    }

    private String getTextFromMimeMultipart(Multipart multipart) throws Exception {

        StringBuilder result = new StringBuilder();

        for (int i = 0; i < multipart.getCount(); i++) {

            BodyPart bodyPart = multipart.getBodyPart(i);

            if (bodyPart.isMimeType("text/plain")) {
                result.append(bodyPart.getContent().toString());
            }

            else if (bodyPart.isMimeType("text/html")) {
                String html = bodyPart.getContent().toString();
                result.append(Jsoup.parse(html).text());
            }

            else if (bodyPart.getContent() instanceof Multipart) {
                result.append(getTextFromMimeMultipart((Multipart) bodyPart.getContent()));
            }
        }

        return result.toString();
    }

    // Extract fields
    private String extractField(String text, String label) {
        Pattern pattern = Pattern.compile(label + "\\s*(.*)");
        Matcher matcher = pattern.matcher(text);

        return matcher.find() ? matcher.group(1).trim() : "";
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
    private void writeCSVLine(BufferedWriter writer, String location, String email, String[] nameParts, String group) throws IOException {
        writer.write(
                escapeCSV(null) + "," +
                        escapeCSV(location) + "," +
                        escapeCSV(email) + "," +
                        escapeCSV(nameParts[0]) + "," +
                        escapeCSV(nameParts[1]) + "," +
                        escapeCSV(nameParts[2]) + "," +
                        escapeCSV(email) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV("Active") + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(group) + "\n"
        );
    }

    private void writeCSVLineAHA(BufferedWriter writer, String email, String[] nameParts, String phone, String date, String acuity) throws IOException {
        writer.write(
                escapeCSV(email) + "," +
                        escapeCSV(nameParts[0]) + "," +
                        escapeCSV(nameParts[1]) + "," +
                        escapeCSV(nameParts[2]) + "," +
                        escapeCSV(phone) + "," +
                        escapeCSV("BLS") + "," +
                        escapeCSV(date) + "," +
                        escapeCSV(acuity) + "," +
                        escapeCSV(null) + "," +
                        escapeCSV(null) + "\n"
        );
    }

    private String escapeCSV(Object value) {
        if (value == null) {
            return "\"\"";
        }
        String text = value.toString().replace("\"", "\"\"");

        return "\"" + text + "\"";
    }

    // Merge temp with running file
    private void mergeCSV(String runningFile, String tempFile, int keyColumnIndex) throws IOException {
        Set<String> seenKeys = new HashSet<>();
        List<String> mergedLines = new ArrayList<>();
        boolean runningExists = new File(runningFile).exists();

        // Read running file if exists
        if (runningExists) {
            try (BufferedReader br = new BufferedReader(new FileReader(runningFile))) {
                String header = br.readLine();
                mergedLines.add(header); // keep header
                String line;
                while ((line = br.readLine()) != null) {
                    String[] cols = line.split(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", -1);
                    if (cols.length > keyColumnIndex) {
                        seenKeys.add(cols[keyColumnIndex].replaceAll("\"", ""));
                        mergedLines.add(line);
                    }
                }
            }
        }

        // Read temp file (always include header if running file doesn’t exist)
        try (BufferedReader br = new BufferedReader(new FileReader(tempFile))) {
            String tempHeader = br.readLine(); // read temp header
            if (!runningExists) {
                mergedLines.add(tempHeader); // copy header to mergedLines
            }

            String line;
            while ((line = br.readLine()) != null) {
                String[] cols = line.split(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", -1);
                if (cols.length > keyColumnIndex) {
                    String key = cols[keyColumnIndex].replaceAll("\"", "");
                    if (!seenKeys.contains(key)) {
                        mergedLines.add(line);
                        seenKeys.add(key);
                    }
                }
            }
        }

        // Write merged data to running file
        try (BufferedWriter bw = new BufferedWriter(new FileWriter(runningFile, false))) {
            for (String l : mergedLines) {
                bw.write(l + "\n");
            }
        }
    }

    // =================================
    //    Sheets/RQI Upload
    // =================================
    // Merge CSV temp files and upload to Google Sheets
    public void mergeAndUploadSheets() throws Exception {
        mergeCSV(OUTPUT_RQI_CSV_FILE, OUTPUT_RQI_TEMP_FILE, 2);
        mergeCSV(OUTPUT_AHA_CSV_FILE, OUTPUT_AHA_TEMP_FILE, 0);

        SheetsUploader.uploadCSV(SPREADSHEET_RQI_ID, OUTPUT_RQI_CSV_FILE);
        SheetsUploader.uploadCSV(SPREADSHEET_AHA_ID, OUTPUT_AHA_CSV_FILE);
    }

    public void uploadToRQI() {
        System.out.println("=================================");
        System.out.println("Attempting to upload to RQI...");
        RqiUploader.uploadRQIFile(OUTPUT_RQI_CSV_FILE);
        System.out.println("=================================");
        System.out.println("Finished successfully. Terminating program...");
    }

}
