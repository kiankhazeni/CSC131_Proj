package com.enrollment;

import com.microsoft.aad.msal4j.*;

import java.util.Arrays;
import java.util.LinkedHashSet;
import java.util.Set;
import java.util.concurrent.CompletionException;

public class OutlookOAuth {

    public static String getAccessToken(AppConfig config) throws Exception {

        String clientId = config.getRequired("outlook.clientId");
        String tenantId = config.getRequired("outlook.tenantId");
        String cacheFile = config.getRequired("calendar.tokenCacheFile");

        String authority = "https://login.microsoftonline.com/" + tenantId;
        Set<String> scopes = parseScopes(config.getRequired("outlook.graphScopes"));

        PublicClientApplication app = PublicClientApplication
                .builder(clientId)
                .authority(authority)
                .setTokenCacheAccessAspect(new MsalFileTokenCache(cacheFile))
                .build();

        IAuthenticationResult result = acquireTokenSilently(app, scopes);

        if (result != null) {
            return result.accessToken();
        }

        DeviceCodeFlowParameters parameters = DeviceCodeFlowParameters
                .builder(scopes, deviceCode -> System.out.println(deviceCode.message()))
                .build();

        result = app.acquireToken(parameters).get();

        return result.accessToken();
    }

    private static IAuthenticationResult acquireTokenSilently(
            PublicClientApplication app,
            Set<String> scopes
    ) {
        try {
            Set<IAccount> accounts = app.getAccounts().join();

            if (accounts.isEmpty()) {
                return null;
            }

            IAccount account = accounts.iterator().next();

            SilentParameters parameters = SilentParameters
                    .builder(scopes, account)
                    .build();

            return app.acquireTokenSilently(parameters).join();

        } catch (CompletionException e) {
            return null;
        } catch (Exception e) {
            return null;
        }
    }

    private static Set<String> parseScopes(String scopesText) {
        Set<String> scopes = new LinkedHashSet<>();

        Arrays.stream(scopesText.split(","))
                .map(String::trim)
                .filter(scope -> !scope.isBlank())
                .forEach(scopes::add);

        return scopes;
    }
}