package AHAautomation;

import java.time.Duration;
import java.time.LocalDate;
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

public class Main {

    public static void run() {
        WebDriverManager.firefoxdriver().setup();
        WebDriver driver = new FirefoxDriver();

        try {
            Actions actions = new Actions(driver);

            driver.manage().timeouts().implicitlyWait(Duration.ofSeconds(30));
            driver.manage().window().maximize();//Maximizeing the screen

            driver.get("https://atlas.heart.org/find-class");
           // Thread.sleep(5000);

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
            emailArea.sendKeys("Sacstatecpr@outlook.com");//Email Area, User could change it to any desired Email

            WebElement passwordArea = wait.until(
                    ExpectedConditions.presenceOfElementLocated(By.xpath("//input[@id='Password']"))//Xpath for the Password
            );
            passwordArea.clear();
            passwordArea.sendKeys("ssCPRLL123*");//Password for the email

           // Thread.sleep(5000);

            WebElement BtnForPassword_Toggle = driver.findElement(By.xpath("//button[@id='btnToggleMask']"));
            BtnForPassword_Toggle.click();

           // Thread.sleep(5000);

            WebElement Btn_Singing = wait.until(
                    ExpectedConditions.elementToBeClickable(By.xpath("//button[@id='btnSignIn']"))//Xpath
            );
            Btn_Singing.click();

            WebElement classes = driver.findElement(By.id("Classes"));
            actions.moveToElement(classes).perform();

            driver.findElement(By.xpath("//*[contains(text(),'Training Site Classes')]")).click();//Xpath

            Thread.sleep(5000);

            WebElement orgInput = driver.findElement(
                    By.xpath("//input[@role='combobox' and @aria-label='Organization']")//Xpath
            );
            orgInput.sendKeys("Sac State");
            orgInput.sendKeys(Keys.ENTER);

            Thread.sleep(3000);

            WebElement instructorField = wait.until(
                    ExpectedConditions.elementToBeClickable(By.xpath("//input[@name='search_name_input']"))//Xpath
            );
            instructorField.click();

            Thread.sleep(3000);// Wait Time

            WebElement sacStateInstructor = wait.until(
                    ExpectedConditions.elementToBeClickable(
                            By.xpath("//ul[contains(@class,'optionContainer')]//span[contains(text(),'Sac State')]")
                    )
            );
            sacStateInstructor.click();

            Thread.sleep(5000);// Wait Time

            WebElement datePicker = wait.until(ExpectedConditions.elementToBeClickable(
                    By.xpath("//span[contains(@class, 'customReactCalendarPicker_placeholderStyle') and text()='Choose a Date Range']")
            ));
            datePicker.click();

            Thread.sleep(2000);// Wait Time

            LocalDate currentDate = LocalDate.now();
            String today = String.format("%03d", currentDate.getDayOfMonth());


            WebElement start_Date = wait.until(ExpectedConditions.elementToBeClickable(
                    By.xpath("//div[contains(@class, 'react-datepicker__day') and contains(@class, 'react-datepicker__day--" + today + "')]")//Xpath
            ));
            start_Date.click();

            Thread.sleep(1000);

            WebElement end_Date = wait.until(ExpectedConditions.elementToBeClickable(
                    By.xpath("//div[contains(@class, 'react-datepicker__day') and contains(@class, 'react-datepicker__day--" + today + "')]")//Xpath
            ));
            end_Date.click();

            Thread.sleep(2000);// Wait Time

            ((JavascriptExecutor) driver).executeScript("window.scrollBy(0, 500)");// Scrolling
            Thread.sleep(2000);// Wait Time

            WebElement Option_ICON = wait.until(ExpectedConditions.elementToBeClickable(
                    By.xpath("//i[contains(@class, 'aha-icon-meat-balls')]")//Xpath
            ));
            Option_ICON.click();

            Thread.sleep(2000);

            WebElement TheBtnForTheView = wait.until(ExpectedConditions.elementToBeClickable(
                    By.xpath("//button[@data-testid='action-menus-0-0' and contains(text(), 'View')]")
            ));
            TheBtnForTheView.click();

            Thread.sleep(2000);//Wait Time
            while (true) {
                try {

                    Thread.sleep(2000);// Wait Time

                    WebElement Accept_Btn = wait.until(ExpectedConditions.elementToBeClickable(
                            By.xpath("//button[@data-testid='acceptbutton' and contains(text(), 'Accept')]")
                    ));
                    Accept_Btn.click();

                    Thread.sleep(2000);// Wait Time

                    WebElement Confriming_To_Accept_Btn = wait.until(ExpectedConditions.elementToBeClickable(
                            By.xpath("//button[@data-testid='acceptBtn' and @aria-label='Accept']") //Xpath
                    ));
                    Confriming_To_Accept_Btn.click();

                    Thread.sleep(3000);// Wait Time

                } catch (Exception e) {

                    System.out.println("All Students have been Accepted / Ther are no more Students to Accept.");
                    break;
                }
            }


        } catch (Exception e) {
            e.printStackTrace();
        } finally {
            driver.quit();// Quite after searching is done or all students have been accpeted
        }
    }

    public static void main(String[] args) {
        run();
    }
}
