package com.emailreader;

import com.microsoft.aad.msal4j.*;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.util.*;
import java.util.regex.*;

public class EmailReader {

    private static final String CLIENT_ID      = "af4d8fa0-07f1-4324-bb95-dff2c4ceb433";
    private static final Set<String> SCOPE     = Collections.singleton("Mail.Read");
    private static final String TARGET_SUBJECT = "Notification from Atlas: Incoming Enrollment Request";
    private static final int    POLL_SECONDS   = 30;

    // Tracks emails we've already processed so we don't handle duplicates
    private static final Set<String> seenIds = new HashSet<>();

    // Holds the current auth result so we can refresh silently
    private static IAuthenticationResult authResult;

    public static void main(String[] args) throws Exception {

 
        PublicClientApplication pca = PublicClientApplication
                .builder(CLIENT_ID)
           
                .authority("https://login.microsoftonline.com/common")
                .build();

        
        DeviceCodeFlowParameters deviceCodeParams = DeviceCodeFlowParameters
                .builder(SCOPE, deviceCode -> System.out.println(deviceCode.message()))
                .build();

        authResult = pca.acquireToken(deviceCodeParams).join();
        System.out.println("Access token acquired. Starting email polling...\n");

        HttpClient httpClient = HttpClient.newHttpClient();

        while (true) {
            // Silently refresh the token before each poll to avoid expir
            authResult = refreshTokenSilently(pca);

            readInbox(httpClient, authResult.accessToken());

            System.out.printf("Waiting %d seconds before next check...%n", POLL_SECONDS);
            Thread.sleep(POLL_SECONDS * 1000L);
        }
    }


    private static IAuthenticationResult refreshTokenSilently(PublicClientApplication pca) {
        try {
            return pca.acquireTokenSilently(
                    SilentParameters.builder(SCOPE, authResult.account()).build()
            ).join();
        } catch (Exception e) {
            System.out.println("Silent token refresh failed – using existing token.");
            return authResult;
        }
    }

    
    private static void readInbox(HttpClient httpClient, String accessToken) throws Exception {

        String url = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages"
                + "?$top=25"
                + "&$orderby=receivedDateTime%20desc"
                + "&$select=id,subject,body";

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .header("Authorization", "Bearer " + accessToken)
                .header("Accept", "application/json")
                .GET()
                .build();

        HttpResponse<String> response =
                httpClient.send(request, HttpResponse.BodyHandlers.ofString());

        if (response.statusCode() != 200) {
            System.out.println("Error fetching emails – HTTP " + response.statusCode());
            System.out.println(response.body());
            return;
        }

        JSONObject json     = new JSONObject(response.body());
        JSONArray  messages = json.getJSONArray("value");

        if (messages.isEmpty()) {
            System.out.println("📭 No emails found.");
            return;
        }

        boolean foundNew = false;

        for (int i = 0; i < messages.length(); i++) {
            JSONObject msg     = messages.getJSONObject(i);
            String     emailId = msg.optString("id", "");
            String     subject = msg.optString("subject", "");

            // Skip wrong subjects or ones we've already processed
            if (!TARGET_SUBJECT.equals(subject) || seenIds.contains(emailId)) {
                continue;
            }

            seenIds.add(emailId);
            foundNew = true;


            // Strip HTML tags from the body
            String rawBody  = msg.getJSONObject("body").optString("content", "");
            String bodyText = rawBody.replaceAll("<[^>]*>", " ")
                    .replaceAll("\\s+", " ")
                    .trim();

            System.out.println("📧 New enrollment email detected!");

            parseEnrollmentDetails(bodyText);
            System.out.println("================================");
        }

        if (!foundNew) {
            System.out.println("📬 No new enrollment emails.");
        }
    }

    private static void parseEnrollmentDetails(String bodyText) {

        Pattern namePattern = Pattern.compile("Dear\\s+([A-Za-z]+)");
        Matcher nameMatcher = namePattern.matcher(bodyText);

        if (nameMatcher.find()) {
            String name = nameMatcher.group(1);
            System.out.println("   Name      : " + name);
        }


        Pattern pattern = Pattern.compile(
                "(\\S+)\\s+(\\S+)\\s+Course\\s+on\\s+(\\S+)",
                Pattern.CASE_INSENSITIVE
        );
        Matcher matcher = pattern.matcher(bodyText);

        if (matcher.find()) {
            String courseType = matcher.group(1) + " " + matcher.group(2);
            String date       = matcher.group(3).replaceAll("[^0-9/\\-]", "");

            System.out.println("   Course    : " + courseType + " Course");
            System.out.println("   Date      : " + date);
        } else {
            System.out.println("   Could not parse enrollment details from email body.");
            System.out.println("   Raw body preview: "
                    + bodyText.substring(0, Math.min(200, bodyText.length())) + "...");
        }
    }
}
