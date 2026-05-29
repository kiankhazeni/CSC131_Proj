package AHAautomation;

import java.time.*;

import org.openqa.selenium.By;
import org.openqa.selenium.Keys;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;
import org.openqa.selenium.firefox.FirefoxDriver;
import org.openqa.selenium.support.ui.ExpectedConditions;
import org.openqa.selenium.support.ui.FluentWait;
import org.openqa.selenium.support.ui.Wait;
import org.openqa.selenium.support.ui.WebDriverWait;
import org.openqa.selenium.interactions.Actions;
import org.openqa.selenium.JavascriptExecutor;
import io.github.bonigarcia.wdm.WebDriverManager;
import java.time.temporal.ChronoUnit;
import java.util.List;

// For headless mode
import org.openqa.selenium.Dimension;

// These ones are just to hide irrelevant logs
import org.openqa.selenium.firefox.GeckoDriverService;
import org.openqa.selenium.firefox.FirefoxOptions;
import org.openqa.selenium.TimeoutException;
import java.io.File;

public class Main {

    public static void run(AppConfig config) {
        // Hide safe-to-ignore error/warning messages
        System.setProperty("org.slf4j.simpleLogger.defaultLogLevel", "error");
        System.setProperty("wdm.quiet", "true");
        java.util.logging.Logger.getLogger("org.openqa.selenium").setLevel(java.util.logging.Level.SEVERE);
        java.util.logging.Logger.getLogger("org.openqa.selenium.remote").setLevel(java.util.logging.Level.SEVERE);

        WebDriverManager.firefoxdriver().setup();

        GeckoDriverService service = new GeckoDriverService.Builder()
                .withLogFile(new File("NUL"))
                .build();

        FirefoxOptions options = new FirefoxOptions();

        boolean headlessMode = config.getRequiredBoolean("aha.headlessMode");

        if (headlessMode) {
            options.addArguments("-headless");
        }

        WebDriver driver = new FirefoxDriver(service, options);

        try {
            Actions actions = new Actions(driver);

            driver.manage().timeouts().implicitlyWait(Duration.ofSeconds(30));
            // driver.manage().window().maximize();//Maximizeing the screen
            configureBrowserWindow(driver, headlessMode);

            driver.get("https://atlas.heart.org/find-class");
           // sleepQuietly(5000);

           Wait<WebDriver> fluentWait = new FluentWait<>(driver)
                  .withTimeout(Duration.ofSeconds(10))
                   .pollingEvery(Duration.ofMillis(100))
                    .ignoring(Exception.class);

            WebElement sign_In_AND_sign_Up_Btn = fluentWait.until(
                    d -> d.findElement(By.xpath("//button[@data-testid='login-logout-button1']"))//Wait Time
            );
            sign_In_AND_sign_Up_Btn.click();

            WebDriverWait wait = new WebDriverWait(driver, Duration.ofSeconds(30));

            WebElement emailArea = wait.until(
                    ExpectedConditions.presenceOfElementLocated(By.xpath("//input[@id='Email']"))
            );
            emailArea.clear();
            emailArea.sendKeys(config.getRequired("aha.email"));//Email Area, User could change it to any desired Email

            WebElement passwordArea = wait.until(
                    ExpectedConditions.presenceOfElementLocated(By.xpath("//input[@id='Password']"))//Xpath for the Password
            );
            passwordArea.clear();
            passwordArea.sendKeys(config.getRequired("aha.password"));//Password for the email

           // sleepQuietly(5000);

            WebElement BtnForPassword_Toggle = driver.findElement(By.xpath("//button[@id='btnToggleMask']"));
            BtnForPassword_Toggle.click();

           // sleepQuietly(5000);

            WebElement Btn_Singing = wait.until(
                    ExpectedConditions.elementToBeClickable(By.xpath("//button[@id='btnSignIn']"))//Xpath
            );
            Btn_Singing.click();

            WebElement classes = driver.findElement(By.id("Classes"));
            actions.moveToElement(classes).perform();

            driver.findElement(By.xpath("//*[contains(text(),'Training Site Classes')]")).click();//Xpath

            sleepQuietly(5000);

            WebElement orgInput = driver.findElement(
                    By.xpath("//input[@role='combobox' and @aria-label='Organization']")//Xpath
            );
            orgInput.sendKeys(config.getRequired("aha.organization"));
            orgInput.sendKeys(Keys.ENTER);

            sleepQuietly(3000);

            String instructorName = config.getRequired("aha.instructor");

            if (!instructorName.equalsIgnoreCase("all")) {
                WebElement instructorField = wait.until(
                        ExpectedConditions.elementToBeClickable(By.xpath("//input[@name='search_name_input']"))//Xpath
                );
                instructorField.click();

                sleepQuietly(3000);// Wait Time

                WebElement instructorButton = wait.until(
                        ExpectedConditions.elementToBeClickable(
                                By.xpath("//ul[contains(@class,'optionContainer')]//span[contains(text(),'" + instructorName + "')]")
                        )
                );
                instructorButton.click();

                sleepQuietly(5000);// Wait Time
            }

            WebElement datePicker = wait.until(ExpectedConditions.elementToBeClickable(
                    By.xpath("//span[contains(@class, 'customReactCalendarPicker_placeholderStyle') and text()='Choose a Date Range']")
            ));
            datePicker.click();

            wait.until(ExpectedConditions.visibilityOfElementLocated(
                    By.xpath("//div[contains(@class, 'react-datepicker__day')]")
            ));

            int startOffsetDays = config.getRequiredInt("aha.startOffsetDays");
            int endOffsetDays = config.getRequiredInt("aha.endOffsetDays");

            LocalDate startDate = LocalDate.now().plusDays(startOffsetDays);
            LocalDate endDate = LocalDate.now().plusDays(endOffsetDays);

            // Debug
            // System.out.println("Start Date: " + startDate);
            // System.out.println("End Date: " + endDate);

            YearMonth visibleMonth = YearMonth.now();

            visibleMonth = selectDate(wait, startDate, visibleMonth);
            sleepQuietly(1000);
            selectDate(wait, endDate, visibleMonth);

            sleepQuietly(2000);// Wait Time

            ((JavascriptExecutor) driver).executeScript("window.scrollBy(0, 500)");// Scrolling
            sleepQuietly(2000);// Wait Time

            processAllResults(driver, wait, config);

        } catch (Exception e) {
            e.printStackTrace();
        } finally {
            if (driver != null) {
                driver.quit(); // Quite after searching is done or all students have been accpeted
            }
        }
    }

    private static void configureBrowserWindow(WebDriver driver, boolean headlessMode) {
        try {
            if (headlessMode) {
                driver.manage().window().setSize(new Dimension(1280, 900));
                return;
            }

            // Do not maximize (too disruptive)
            driver.manage().window().setSize(new Dimension(1280, 900));
        } catch (Exception e) {
            System.out.println("Could not configure browser window: " + e.getMessage());
        }
    }

    private static void processAllResults(
            WebDriver driver,
            WebDriverWait wait,
            AppConfig config
    ) throws Exception {
        int classIndex = 0;

        while (true) {
            // count number of listings
            List<WebElement> optionIcons = driver.findElements(
                    By.xpath("//i[contains(@class, 'aha-icon-meat-balls')]")
            );

            if (optionIcons.isEmpty()) {
                System.out.println("No students/classes found for the date range. Exiting.");
                return;
            }

            if (classIndex >= optionIcons.size()) {
                System.out.println("Finished processing all visible class results.");
                return;
            }

            System.out.println("Processing class result " + (classIndex + 1) + " of " + optionIcons.size());

            try {
                openClassByIndex(driver, wait, classIndex);

                sleepQuietly(2000);

                AhaScraper.scrapeAndUpdateCsv(driver, config);

                acceptAllStudents(wait);

                classIndex++;

                driver.navigate().back();

                wait.until(ExpectedConditions.presenceOfElementLocated(
                        By.xpath("//i[contains(@class, 'aha-icon-meat-balls')]")
                ));

                sleepQuietly(2000);
            } catch (TimeoutException e) {
                System.out.println("Timed out opening class result " + (classIndex + 1) + ". Skipping.");

                classIndex++;

                if (!driver.getCurrentUrl().contains("class-listing")) {
                    driver.navigate().back();
                    sleepQuietly(2000);
                }
            }
        }
    }

    private static void openClassByIndex(
            WebDriver driver,
            WebDriverWait wait,
            int classIndex
    ) {
        List<WebElement> optionIcons = driver.findElements(
                By.xpath("//i[contains(@class, 'aha-icon-meat-balls')]")
        );

        if (classIndex >= optionIcons.size()) {
            throw new TimeoutException("Class index no longer exists: " + classIndex);
        }

        WebElement optionIcon = optionIcons.get(classIndex);

        ((JavascriptExecutor) driver).executeScript(
                "arguments[0].scrollIntoView({block: 'center'});",
                optionIcon
        );

        sleepQuietly(500);

        try {
            wait.until(ExpectedConditions.elementToBeClickable(optionIcon)).click();
        } catch (Exception e) {
            ((JavascriptExecutor) driver).executeScript("arguments[0].click();", optionIcon);
        }

        sleepQuietly(500);

        String viewButtonTestId = "action-menus-0-" + classIndex;

        WebElement viewButton = wait.until(ExpectedConditions.elementToBeClickable(
                By.xpath("//button[@data-testid='" + viewButtonTestId + "' " +
                        "and contains(normalize-space(.), 'View')]")
        ));

        try {
            viewButton.click();
        } catch (Exception e) {
            ((JavascriptExecutor) driver).executeScript("arguments[0].click();", viewButton);
        }
    }

    private static void acceptAllStudents(WebDriverWait wait) {
        while (true) {
            try {
                sleepQuietly(2000);// Wait Time

                WebElement Accept_Btn = wait.until(ExpectedConditions.elementToBeClickable(
                        By.xpath("//button[@data-testid='acceptbutton' and contains(text(), 'Accept')]")
                ));
                Accept_Btn.click();

                sleepQuietly(2000);// Wait Time

                WebElement Confriming_To_Accept_Btn = wait.until(ExpectedConditions.elementToBeClickable(
                        By.xpath("//button[@data-testid='acceptBtn' and @aria-label='Accept']") //Xpath
                ));
                Confriming_To_Accept_Btn.click();

                sleepQuietly(3000);// Wait Time

            } catch (Exception e) {
                System.out.println("All Students have been Accepted. Moving on.");
                break;
            }
        }
    }

    // helpers for date selector config
    private static YearMonth selectDate(
            WebDriverWait wait,
            LocalDate targetDate,
            YearMonth visibleMonth
    ) {
        YearMonth targetMonth = YearMonth.from(targetDate);

        navigateDatePickerToMonth(wait, visibleMonth, targetMonth);

        String targetDay = String.format("%03d", targetDate.getDayOfMonth());

        WebElement dateElement = wait.until(ExpectedConditions.elementToBeClickable(
                By.xpath("//div[contains(@class, 'react-datepicker__day') " +
                        "and contains(@class, 'react-datepicker__day--" + targetDay + "') " +
                        "and not(contains(@class, 'react-datepicker__day--outside-month'))]")
        ));

        dateElement.click();

        return targetMonth;
    }

    private static YearMonth navigateDatePickerToMonth(
            WebDriverWait wait,
            YearMonth visibleMonth,
            YearMonth targetMonth
    ) {
        long monthDifference = ChronoUnit.MONTHS.between(visibleMonth, targetMonth);

        while (monthDifference > 0) {
            clickNextMonth(wait);
            visibleMonth = visibleMonth.plusMonths(1);
            monthDifference--;
            sleepQuietly(300);
        }

        while (monthDifference < 0) {
            clickPreviousMonth(wait);
            visibleMonth = visibleMonth.minusMonths(1);
            monthDifference++;
            sleepQuietly(300);
        }

        return visibleMonth;
    }

    private static void clickNextMonth(WebDriverWait wait) {
        WebElement nextButton = wait.until(ExpectedConditions.elementToBeClickable(
                By.xpath("//button[contains(@class, 'reactDatePicker_next_month_btn')]")
        ));

        nextButton.click();
    }

    private static void clickPreviousMonth(WebDriverWait wait) {
        WebElement previousButton = wait.until(ExpectedConditions.elementToBeClickable(
                By.xpath("//button[contains(@class, 'reactDatePicker_previous_month_btn')]")
        ));

        previousButton.click();
    }

    private static void sleepQuietly(long millis) {
        try {
            Thread.sleep(millis);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }

    // main

    public static void main(String[] args) {
        AppConfig config = new AppConfig();
        run(config);
    }
}
