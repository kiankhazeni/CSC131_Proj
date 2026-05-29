package AHAautomation;

import org.openqa.selenium.By;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;

import java.util.*;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

class AhaScraper {

    static class StudentInfo {
        String firstName;
        String lastName;
        String email;
        String phone;
        String date;

        StudentInfo(String firstName, String lastName, String email, String phone, String date) {
            this.firstName = clean(firstName);
            this.lastName = clean(lastName);
            this.email = clean(email);
            this.phone = normalizePhone(phone);
            this.date = clean(date);
        }

        private static String clean(String s) {
            return s == null ? "" : s.trim();
        }

        @Override
        public String toString() {
            return "First Name: " + firstName +
                    "\nLast Name: " + lastName +
                    "\nEmail: " + email +
                    "\nPhone: " + phone +
                    "\nDate: " + date;
        }

        public List<Object> toSheetRow() {
            return Arrays.asList(
                    email,        // A
                    firstName,    // B
                    "",           // C
                    lastName,     // D
                    phone,        // E
                    "",           // F
                    date,         // G
                    "",           // H
                    "YES",        // I
                    ""            // J
            );
        }

        private static String normalizePhone(String s) {
            if (s == null) {
                return "";
            }

            return s.replaceAll("\\D", "");
        }
    }

    public static void scrapeAndUpdateCsv(WebDriver driver, AppConfig config) throws Exception {
        String ahaCsvFile = config.getRequired("file.ahaCsv");

        String classDate = scrapeClassDate(driver);
        List<StudentInfo> students = scrapeStudentsFromTable(driver, classDate);

        System.out.println("\n===== SCRAPED STUDENTS =====");
        System.out.println("Class date: " + (classDate.isBlank() ? "(not found)" : classDate));
        for (int i = 0; i < students.size(); i++) {
            System.out.println("\nStudent " + (i + 1));
            System.out.println(students.get(i));
        }

        System.out.println("\n===== UPDATING AHA CSV =====");
        AhaCsvWriter.insertStudents(ahaCsvFile, students);

        System.out.println("\nAHA sheet update complete.");
    }

    static List<StudentInfo> scrapeStudentsFromTable(WebDriver driver, String classDate) {
        List<StudentInfo> results = new ArrayList<>();
        List<WebElement> tables = driver.findElements(By.tagName("table"));

        for (WebElement table : tables) {
            List<String> headers = getHeaders(table);
            results.addAll(extractStudentsFromTable(table, headers, classDate));
        }

        return results;
    }

    private static List<String> getHeaders(WebElement table) {
        List<String> headers = new ArrayList<>();

        List<WebElement> ths = table.findElements(By.xpath(".//tr[1]//th"));
        if (ths.isEmpty()) {
            ths = table.findElements(By.xpath(".//thead//th"));
        }

        for (WebElement th : ths) {
            String text = th.getText().trim();
            if (!text.isEmpty()) {
                headers.add(text);
            }
        }

        return headers;
    }

    private static List<String> getRowTexts(WebElement row) {
        List<String> cells = new ArrayList<>();
        List<WebElement> tds = row.findElements(By.xpath(".//td"));

        for (WebElement td : tds) {
            cells.add(td.getText().trim());
        }

        return cells;
    }

    private static List<StudentInfo> extractStudentsFromTable(WebElement table, List<String> headers, String classDate) {
        List<StudentInfo> students = new ArrayList<>();
        List<WebElement> rows = table.findElements(By.xpath(".//tr[td]"));

        int nameCol = findHeaderIndex(headers, "name");
        int emailCol = findHeaderIndex(headers, "email");
        int phoneCol = findHeaderIndex(headers, "phone");

        for (WebElement row : rows) {
            List<String> cells = getRowTexts(row);
            if (cells.isEmpty()) {
                continue;
            }

            String rowText = String.join(" | ", cells);
            String email = findEmail(rowText);
            String phone = findPhone(rowText);

            String nameText;
            if (nameCol >= 0 && nameCol < cells.size()) {
                nameText = cells.get(nameCol);
            } else {
                nameText = guessNameCell(cells);
            }

            if (emailCol >= 0 && emailCol < cells.size()) {
                String emailFromCell = findEmail(cells.get(emailCol));
                if (!emailFromCell.isEmpty()) {
                    email = emailFromCell;
                }
            }

            if (phoneCol >= 0 && phoneCol < cells.size()) {
                String phoneFromCell = findPhone(cells.get(phoneCol));
                if (!phoneFromCell.isEmpty()) {
                    phone = phoneFromCell;
                }
            }

            if (!email.isEmpty() || !phone.isEmpty() || looksLikePersonName(nameText)) {
                String[] nameParts = splitName(nameText);
                StudentInfo info = new StudentInfo(nameParts[0], nameParts[1], email, phone, classDate);

                if (isUseful(info) && !info.email.equalsIgnoreCase("Name/Phone Number")) {
                    students.add(info);
                }
            }
        }

        return students;
    }

    private static String scrapeClassDate(WebDriver driver) {
        try {
            WebElement dateElement = driver.findElement(By.xpath(
                    "//label[normalize-space()='Date | Time']" +
                            "/ancestor::div[contains(@class, 'col-')][1]//span"
            ));

            String raw = dateElement.getAttribute("title");

            if (raw == null || raw.isBlank()) {
                raw = dateElement.getText();
            }

            return formatClassDate(raw);

        } catch (Exception e) {
            System.out.println("Could not find class date on page.");
            return "";
        }
    }

    private static String formatClassDate(String raw) {
        if (raw == null || raw.isBlank()) {
            return "";
        }

        Matcher matcher = Pattern.compile("(\\d{1,2})-(\\d{1,2})-(\\d{4})").matcher(raw);

        if (!matcher.find()) {
            return "";
        }

        int month = Integer.parseInt(matcher.group(1));
        int day = Integer.parseInt(matcher.group(2));
        int year = Integer.parseInt(matcher.group(3));

        return month + "/" + day + "/" + year;
    }

    private static int findHeaderIndex(List<String> headers, String keyword) {
        for (int i = 0; i < headers.size(); i++) {
            if (headers.get(i).toLowerCase().contains(keyword)) {
                return i;
            }
        }
        return -1;
    }

    private static String guessNameCell(List<String> cells) {
        for (String cell : cells) {
            if (looksLikePersonName(cell)) {
                return cell;
            }
        }

        for (String cell : cells) {
            String stripped = cell
                    .replace(findEmail(cell), "")
                    .replace(findPhone(cell), "")
                    .trim();

            if (looksLikePersonName(stripped)) {
                return stripped;
            }
        }

        return "";
    }

    private static boolean looksLikePersonName(String text) {
        if (text == null) return false;

        String s = text.trim();
        if (s.isEmpty()) return false;
        if (s.length() < 3) return false;
        if (s.toLowerCase().contains("name/phone")) return false;
        if (s.toLowerCase().contains("email")) return false;
        if (!findEmail(s).isEmpty()) return false;

        String[] parts = s.split("\\s+|,\\s*");
        int wordCount = 0;

        for (String p : parts) {
            if (p.matches("[A-Za-z][A-Za-z'\\-]+")) {
                wordCount++;
            }
        }

        return wordCount >= 2;
    }

    private static String findEmail(String text) {
        if (text == null) return "";

        Matcher m = Pattern.compile("[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}").matcher(text);
        return m.find() ? m.group() : "";
    }

    private static String findPhone(String text) {
        if (text == null) return "";

        Matcher m = Pattern.compile("(\\+?1[-.\\s]?)?(\\(?\\d{3}\\)?[-.\\s]?\\d{3}[-.\\s]?\\d{4})").matcher(text);
        return m.find() ? m.group().trim() : "";
    }

    private static String[] splitName(String fullName) {
        String cleaned = fullName == null ? "" : fullName.trim();

        cleaned = cleaned.replaceAll("\\s+", " ");
        cleaned = cleaned.replace(findEmail(cleaned), "").trim();
        cleaned = cleaned.replace(findPhone(cleaned), "").trim();

        if (cleaned.contains(",")) {
            String[] parts = cleaned.split(",", 2);
            String last = parts[0].trim();
            String first = parts.length > 1 ? parts[1].trim() : "";
            return new String[]{first, last};
        }

        String[] parts = cleaned.split(" ");
        if (parts.length >= 2) {
            return new String[]{parts[0], parts[parts.length - 1]};
        }

        return new String[]{cleaned, ""};
    }

    private static boolean isUseful(StudentInfo info) {
        return !(info.firstName.isEmpty()
                && info.lastName.isEmpty()
                && info.email.isEmpty()
                && info.phone.isEmpty());
    }
}