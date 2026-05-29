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

import com.microsoft.graph.serviceclient.GraphServiceClient;

import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.Set;

public class Main {

    public static void main(String[] args) throws Exception {
        // hiding logs
        System.setProperty("org.slf4j.simpleLogger.defaultLogLevel", "error");
        System.setProperty("org.slf4j.simpleLogger.log.com.microsoft.aad.msal4jextensions", "off");
        System.setProperty("org.slf4j.simpleLogger.log.com.microsoft.aad.msal4jextensions.CrossProcessCacheFileLock", "off");
        System.setProperty("org.slf4j.simpleLogger.log.com.azure", "error");
        System.setProperty("org.slf4j.simpleLogger.defaultLogLevel", "error");
        System.setProperty("org.slf4j.simpleLogger.log.com.azure.core.http.netty.implementation.NettyUtility", "error");

        AppConfig config = new AppConfig();
        int runInterval = config.getRequiredInt("app.runInterval");
        boolean runContinuously = config.getRequiredBoolean("app.runContinuously");
        boolean rqiEnabled = config.getRequiredBoolean("rqi.enabled");

        DateTimeFormatter timestampFormat = DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss");

        if (runInterval <= 0) {
            throw new IllegalArgumentException("app.runInterval must be greater than 0.");
        }

        GraphServiceClient graphClient = OutlookOAuth.createGraphClient(config);

        EmailReader reader = new EmailReader(graphClient);
        EmailScraper scraper = new EmailScraper(config);

        Set<String> processedIds = scraper.readProcessedMessageIds();

        System.out.println("=================================");
        System.out.println("Email parser started");
        System.out.println();
        System.out.println("Run continuously: " + runContinuously);
        System.out.println("Poll interval: " + runInterval + " seconds");
        System.out.println("Upload to RQI: " + rqiEnabled);
        System.out.println();
        System.out.println("Press Ctrl+C to stop");
        System.out.println("=================================");

        while (true) {
            try {
                System.out.println("Checking inbox at " + LocalDateTime.now().format(timestampFormat) + "...");

                // Fetch recent messages from Outlook
                int maxMsgCount = config.getRequiredInt("email.maxCount");
                int maxMsgAge = config.getRequiredInt("email.maxAge");    // Days
                List<OutlookEmailMessage> messages = reader.fetchRecentMessages(maxMsgCount, maxMsgAge);
                System.out.println("Processing " + messages.size() + " messages (max " + maxMsgCount + ", past " + maxMsgAge + " days)");

                EmailScraper.ProcessResult result = scraper.processMessages(messages, processedIds);

                processedIds = result.getUpdatedProcessedIds();
                scraper.writeProcessedMessageIds(processedIds);

                int csvChanges = result.writeChangedRows();

                if (csvChanges > 0) {
                    System.out.println();
                    System.out.println("Found " + csvChanges + " CSV change(s)");
                    System.out.println();

                    scraper.syncCsvsToSheets();

                    if (rqiEnabled) {
                        System.out.println("=================================");
                        System.out.println("Attempting to upload to RQI...");
                        RqiUploader.uploadRQIFile(config);
                        System.out.println("=================================");
                    } else {
                        System.out.println("=================================");
                        System.out.println("RQI Upload is disabled. Skipping.");
                        System.out.println("=================================");
                    }
                } else {
                    System.out.println("\nNo new appointments found. Skipping Sheets/RQI upload.");
                    System.out.println("=================================");
                }

            } catch (Exception e) {
                System.out.println("Error during inbox check:");
                e.printStackTrace();
            }

            if (!runContinuously) {
                System.out.println("Single-run mode complete. Exiting...");
                break;
            }

            try {
                Thread.sleep(runInterval * 1000L);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                System.out.println("Program interrupted. Exiting...");
                break;
            }
        }
    }
}