package main;

import com.azure.identity.DeviceCodeCredential;
import com.azure.identity.DeviceCodeCredentialBuilder;
import com.azure.identity.TokenCachePersistenceOptions;
import com.microsoft.graph.serviceclient.GraphServiceClient;
import com.azure.core.credential.TokenRequestContext;
import com.azure.identity.AuthenticationRecord;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;


public class OutlookOAuth {

    public static GraphServiceClient createGraphClient(AppConfig config) throws Exception {

        String clientId = config.getRequired("outlook.clientId");
        String tenantId = config.getRequired("outlook.tenantId");

        String cacheName = config.getRequired("outlook.tokenCacheName");
        String cacheFile = config.getRequired("outlook.tokenCacheFile");

        String[] scopes = parseScopes(config.getRequired("outlook.graphScopes"));

        TokenCachePersistenceOptions cacheOptions =
                new TokenCachePersistenceOptions().setName(cacheName);

        AuthenticationRecord authRecord = loadAuthenticationRecord(cacheFile);

        DeviceCodeCredentialBuilder builder = new DeviceCodeCredentialBuilder()
                .clientId(clientId)
                .tenantId(tenantId)
                .challengeConsumer(challenge -> System.out.println(challenge.getMessage()))
                .tokenCachePersistenceOptions(cacheOptions);

        if (authRecord != null) {
            builder.authenticationRecord(authRecord);
        }

        DeviceCodeCredential credential = builder.build();

        if (authRecord == null) {
            System.out.println("No Microsoft authentication record found. Sign-in required.");

            TokenRequestContext requestContext = new TokenRequestContext()
                    .addScopes(scopes);

            AuthenticationRecord newRecord = credential.authenticate(requestContext).block();

            if (newRecord != null) {
                saveAuthenticationRecord(cacheFile, newRecord);
                System.out.println("Saved Microsoft authentication record.");
            }
        }

        return new GraphServiceClient(credential, scopes);
    }

    private static AuthenticationRecord loadAuthenticationRecord(String filePath) {
        File file = new File(filePath);

        if (!file.exists()) {
            return null;
        }

        try (FileInputStream input = new FileInputStream(file)) {
            return AuthenticationRecord.deserialize(input);
        } catch (Exception e) {
            System.out.println("Could not load Microsoft authentication record. Sign-in will be required.");
            return null;
        }
    }

    private static void saveAuthenticationRecord(
            String filePath,
            AuthenticationRecord authRecord
    ) throws Exception {

        File file = new File(filePath);
        File parent = file.getParentFile();

        if (parent != null) {
            parent.mkdirs();
        }

        try (FileOutputStream output = new FileOutputStream(file)) {
            authRecord.serialize(output);
        }
    }

    private static String[] parseScopes(String scopesText) {
        String[] scopes = scopesText.split(",");

        for (int i = 0; i < scopes.length; i++) {
            scopes[i] = scopes[i].trim();
        }

        return scopes;
    }
}