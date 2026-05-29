package reminder;

public class Main {

    public static void main(String[] args) {
        try {
            AppConfig config = new AppConfig();

            boolean runContinuously = config.getRequiredBoolean("reminder.runContinuously");
            int runInterval = config.getRequiredInt("reminder.runInterval");

            if (runInterval <= 0) {
                throw new IllegalArgumentException("reminder.runInterval must be greater than 0.");
            }

            RegistrationReminder reminder = new RegistrationReminder(config);

            while (true) {
                reminder.run();

                if (!runContinuously) {
                    System.out.println("Single-run mode complete. Exiting.");
                    break;
                }

                System.out.println("Next reminder check in: " + runInterval + " seconds.");
                Thread.sleep(runInterval * 1000L);
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            System.out.println("Reminder program interrupted. Exiting.");
        } catch (Exception e) {
            System.out.println("Registration reminder failed.");
            e.printStackTrace();
        }
    }
}