/*
    String username = "mockstudentcsc131@gmail.com";
    String password = "bsve zzlg qlnm bqck";

    SFTP credentials
    Host name: sftp://rqi1stop-sftp-preprod.rqi1stop.com
    Username: 116286
    Port: 6239
    File Path: /uploads/116286
    Password: bEtR0X6@O$
    File Type: Delta
    File Name: preprod_cl.csv (must use this file name or the drop will fail)
*/

package main;

import jakarta.mail.*;
import com.sun.mail.imap.IMAPFolder;

public class Main {

    public static void main(String[] args) throws Exception {

        // Gmail credentials
        String username = "mockstudentcsc131@gmail.com";
        String password = "bsve zzlg qlnm bqck";

        // Initialize the EmailScraper
        EmailScraper scraper = new EmailScraper(username, password);

        // Connect to inbox
        IMAPFolder inbox = scraper.connectToInbox();

        // Fetch recent messages (max 50, past 14 days)
        Message[] messages = scraper.fetchRecentMessages(inbox, 50, 140);

        System.out.println("Processing " + messages.length + " messages (max 50, past 14 days)");

        // Process messages
        long lastProcessedUID = scraper.readLastUID();
        long highestUID = scraper.processMessages(inbox, messages, lastProcessedUID);

        // Update last processed UID
        scraper.writeLastUID(highestUID);

        // Merge CSV temp files with main files and upload to sheets
        scraper.mergeAndUploadSheets();

        // Upload to RQI
        scraper.uploadToRQI();

        // Close mailbox
        inbox.close(false);
        inbox.getStore().close();
    }
}