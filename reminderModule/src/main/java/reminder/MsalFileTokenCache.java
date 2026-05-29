package reminder;

import com.microsoft.aad.msal4j.ITokenCacheAccessAspect;
import com.microsoft.aad.msal4j.ITokenCacheAccessContext;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

public class MsalFileTokenCache implements ITokenCacheAccessAspect {

    private final Path cachePath;

    public MsalFileTokenCache(String cacheFilePath) {
        this.cachePath = Path.of(cacheFilePath);
    }

    @Override
    public void beforeCacheAccess(ITokenCacheAccessContext context) {
        try {
            if (Files.exists(cachePath)) {
                String cacheData = Files.readString(cachePath);

                if (!cacheData.isBlank()) {
                    context.tokenCache().deserialize(cacheData);
                }
            }
        } catch (IOException e) {
            throw new RuntimeException("Could not read MSAL token cache: " + cachePath, e);
        }
    }

    @Override
    public void afterCacheAccess(ITokenCacheAccessContext context) {
        if (!context.hasCacheChanged()) {
            return;
        }

        try {
            if (cachePath.getParent() != null) {
                Files.createDirectories(cachePath.getParent());
            }

            Files.writeString(cachePath, context.tokenCache().serialize());

        } catch (IOException e) {
            throw new RuntimeException("Could not write MSAL token cache: " + cachePath, e);
        }
    }
}