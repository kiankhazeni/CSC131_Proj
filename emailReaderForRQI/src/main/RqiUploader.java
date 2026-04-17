package main;

import com.jcraft.jsch.ChannelSftp;
import com.jcraft.jsch.JSch;
import com.jcraft.jsch.Session;

import java.io.File;

public class RqiUploader {

    private static final String HOST        = "rqi1stop-sftp-preprod.rqi1stop.com";
    private static final int PORT           = 6239;

    private static final String USERNAME    = "116286";
    private static final String PASSWORD    = "bEtR0X6@O$";

    private static final String REMOTE_DIR  = "/uploads/116286";
    private static final String FILENAME    = "preprod_cl.csv";

    public static void uploadRQIFile(String localFilePath) {

        Session session = null;
        ChannelSftp sftp = null;

        try {
            File file = new File(localFilePath);

            // Check filename
            if (!file.getName().equals(FILENAME)) {
                throw new RuntimeException(
                        "Filename must match: " + FILENAME
                );
            }

            JSch jsch = new JSch();
            session = jsch.getSession(USERNAME, HOST, PORT);
            session.setPassword(PASSWORD);

            session.setConfig("StrictHostKeyChecking", "no");
            session.connect();

            sftp = (ChannelSftp) session.openChannel("sftp");
            sftp.connect();

            System.out.println("Connected to RQI SFTP...");

            // Navigate to correct directory
            sftp.cd(REMOTE_DIR);

            // Upload file (overwrite mode)
            sftp.put(file.getAbsolutePath(), FILENAME);

            System.out.println("RQI upload successful...");

        } catch (Exception e) {
            System.out.println("RQI upload failed...");
            e.printStackTrace();
        } finally {
            if (sftp != null) sftp.disconnect();
            if (session != null) session.disconnect();
        }
    }
}