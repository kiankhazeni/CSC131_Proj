package reminder;

import java.io.FileInputStream;
import java.io.IOException;
import java.util.Properties;

public class AppConfig {

    private static final String CONFIG_FILE = "config/app.properties";

    private final Properties properties = new Properties();

    public AppConfig() {
        String configPath = System.getProperty("app.config", CONFIG_FILE);

        try (FileInputStream input = new FileInputStream(configPath)) {
            properties.load(input);
        } catch (IOException e) {
            throw new RuntimeException("Could not load config file: " + configPath, e);
        }
    }

    public String getRequired(String key) {
        String value = properties.getProperty(key);

        if (value == null || value.isBlank()) {
            throw new IllegalStateException("Missing config value: " + key);
        }

        return value.trim();
    }

    public int getRequiredInt(String key) {
        return Integer.parseInt(getRequired(key));
    }

    public boolean getRequiredBoolean(String key) {
        String value = properties.getProperty(key);

        return Boolean.parseBoolean(value.trim());
    }
}