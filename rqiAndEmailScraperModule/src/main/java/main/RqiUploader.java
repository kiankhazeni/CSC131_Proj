package main;

import com.jcraft.jsch.ChannelSftp;
import com.jcraft.jsch.JSch;
import com.jcraft.jsch.Session;

import java.io.File;

public class RqiUploader {

    public static void uploadRQIFile(AppConfig config) {

        String csvFilePath  = config.getRequired("file.rqiCsv");

        String host         = config.getRequired("rqi.host");
        int port            = config.getRequiredInt("rqi.port");

        String username     = config.getRequired("rqi.username");
        String password     = config.getRequired("rqi.password");

        String remoteDir    = config.getRequired("rqi.remoteDir");
        String filename     = config.getRequired("rqi.filename");

        Session session = null;
        ChannelSftp sftp = null;

        try {
            File file = new File(csvFilePath);

            // Check filename
            if (!file.getName().equals(filename)) {
                throw new RuntimeException(
                        "Filename must match: " + filename
                );
            }

            JSch jsch = new JSch();
            session = jsch.getSession(username, host, port);
            session.setPassword(password);

            session.setConfig("StrictHostKeyChecking", "no");
            session.connect();

            sftp = (ChannelSftp) session.openChannel("sftp");
            sftp.connect();

            System.out.println("Connected to RQI SFTP...");

            // Navigate to correct directory
            sftp.cd(remoteDir);

            // Upload file (overwrite mode)
            sftp.put(file.getAbsolutePath(), filename);

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