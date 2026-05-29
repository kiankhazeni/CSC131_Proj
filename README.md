# Vitals | CPR Lifeline Automation App

## Project Overview

`Vitals Automation` is a Windows desktop app built with `C#`/`.NET 8 WPF` and `Java`. It provides a central user interface for running and monitoring several CPR Lifeline automation modules, including RQI CSV upload/email scraping, AHA student acceptance automation and scraping, Outlook calendar event creation, and reminder email functionalities. The app is intended to reduce repetitive administrative work by combining module controls, CSV generation, configuration editing, and logs into a single user-friendly tool.

The desktop app launches packaged Java modules from the `modules/` folder and uses shared configuration files from the `config/` folder. The user interface includes a [**Dashboard**] for starting/stopping automation, a [**Students**] view for viewing/loading/exporting CSV data, a [**Reminders**] view, a Logs view with summarized events, and a [**Settings**] view for reviewing module and app configuration.

> [!NOTE]
> The folders in the repository will **NOT** run by themselves because sensitive fields have been redacted. Download the `AES-256`-encrypted `.zip` files and extract them using the passcode to access the working program and modules.


<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->


## Installation & Setup

### Option 1: Run the packaged client build

| [**Required Tool**] | [**Link**] | [**Reason**] |
| --- | --- | --- |
| Windows operating system | N/A | WPF apps do not work outside of Windows operating systems |
| `Java 21` | https://www.oracle.com/java/technologies/downloads/#jdk21-windows | This is needed to run the `.jar` files for the automation modules |
| Archive tool | https://www.7-zip.org/download.html | An archive tool that supports encryption is necessary to extract the `.zip` files, which are password-encrypted. The app contains keys that will be disabled if exposed in a public repository |

This is the recommended option for the client because it does not require Visual Studio, `.NET SDK`, or `.NET runtime`. This option is self-contained and includes the necessary runtime files, minus `Java 21`.

#### Steps:
1. Download the `client_package.zip` from [Releases](https://github.com/kiankhazeni/CSC131_Proj/releases/tag/v1.0)
2. Extract the zip folder in your desired location using passcode `cpr-lifeline`
3. Open the extracted `VitalsAutomation` folder
4. Scroll down and find `VitalsAutomation.exe`. This is the file you will run

> [!NOTE]
> Keep the `config/`, `modules/`, and `resources/` folders beside the `.exe`. Do **NOT** move the `.exe` by itself or get rid of any files. You may want to make a shortcut for easier access instead.

### Option 2: Build from source

This option is intended for the professor, grader, or anyone reviewing the source code.

| [**Required Tool**] |
| --- |
| <ul><li>Windows 10 or Windows 11</li></ul>
| <ul><li>Visual Studio with the `.NET desktop development` workload, or the .NET 8 SDK</li></ul>
| <ul><li>`Java 21` installed and available through `JAVA_HOME`, `PATH`, or a local `tools/jdk/` folder</li></ul>
| <ul><li>Access to the required Microsoft/Google/AHA/RQI credentials and configuration values</li></ul>
| <ul><li>An editor that supports `Java` if you wish to review the individual module code (e.g. IntelliJ)</li></ul>
| <ul><li>An archive tool that can handle encrypted archives (e.g. 7-Zip)</li></ul>

#### Steps
1. Download `modules_with_intact_secrets.zip` from [Releases](https://github.com/kiankhazeni/CSC131_Proj/releases/tag/v1.0-source)
2. Extract using passcode `cpr-lifeline`
3. `UserInterface/` is the folder containing the main program. Access it via `UserInterface.sln` in Visual Studio and press Start
3.  The other 4 folders are individual modules that have been packaged into `.jar` files and placed in `UserInterface/UserInterface/modules/`. Access the modules by opening each module's folder in your `Java`-compatible editor


<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->


## How to Run the App

### Option 1: Using `client_package.zip`
1. Navigate to the folder you extracted and open it. You will see a folder with about 250 files. You can ignore most of these
2. Scroll to the bottom, where you will find <img width="12" height="12" alt="vitals_icon_title" src="https://github.com/user-attachments/assets/62f6cbba-e27a-4ba1-9be7-a3c7a8a8c131" />
`VitalsAutomation.exe`
3. Double-click on `VitalsAutomation.exe` to run it
    - You may make a shortcut for easy access. Do **NOT** move any files out of this folder
4. Check the [**Settings**] tab for configurations, credentials, and spreadsheet IDs you may want to set

### Option 2: Using `modules_with_intact_secrets.zip`
1. Navigate to the folder you extracted. Open `UserInterface/UserInterface.sln` with Visual Studio
2. Build and run
4. Check the [**Settings**] tab for configurations, credentials, and spreadsheet IDs you may want to set

## How to Factory Reset the App
#### For `client_package.zip`:
1. Go to [**Settings**] and click **Module Configuration**:`Restore Defaults`, **Microsoft Sign-in Caches**:`Clear All`, and `Restore App Defaults`
2. Go to [**Logs**] and click `Open Log Folder`
3. Click `Quit` (not the `X` button)
4. In the folder opened by `Open Log Folder`, go back two folders and delete the `VitalsAutomation` folder (you must do this after quitting the app)
5. Go to app folder. Open `resources` folder and delete all files in the current folder
    - Do **NOT** delete anything in `assets/` or `email_templates/`
6. Create new files in `resources/` called `aha.csv` and `preprod_cl.csv` (create a `.txt` file and rename the extension). You can leave these empty
7. Start the app. Everything should be reset

#### For `modules_with_intact_secrets.zip`:
1. Follow Steps 1 - 4 above
2. Go to app folder. Delete `bin/` and `obj/` (close Visual Studio if you cannot delete it)
3. Start the app. Everything should be reset


<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->


## Configuration and Credentials

This project uses configuration files located in the `config/` folder. The app expects the files to be named:

```text
config/app.properties
config/credentials.json
config/default-app.properties
```

Because the GitHub repository is public, sensitive config files are encrypted before upload to prevent the disabling of credentials and keys. The `AES-256` decryption key/password is `cpr-lifeline`.

The `config/auth_cache/` folder is used for Microsoft authentication token cache files and may be generated or updated after sign-in. They are safe to delete, but users will have to sign-in to Microsoft again. Delete these in Settings if you wish to log in with a different account.


<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
<!----------------------------------------------------------------------------------------------------------------------------------------------------------------------------->


## Demo Instructions

Use the following steps to reproduce the live demo. For demonstration purposes, `put_this_in_resources_for_demo.csv` is provided. 


### Preparing the Demo
1. Put `put_this_in_resources_for_demo.csv` in the `resources` folder. This will be used to demonstrate the `Reminders` module
2. Launch the app by double-clicking `VitalsAutomation.exe` from the packaged folder, or run it from Visual Studio

> [!NOTE]
> Remember to place the demo file inside the `resources` folder so we can demonstrate!

### Demo Starts

#### Tabs Walkthrough

1. The app should open on the [**Dashboard**] tab
   - The initial **Automation Status** should show `Stopped`
	- **Automation Modules** shows the 4 modules: `RQI Uploader & Email Scraper`, `AHA Automation`, `Outlook Event Creator`, `Reminders`
   - The module toggles can be used to enable or disable individual modules before starting automation
	- **Recent Logs** shows the 3 most recent log events. Test this by clicking a module toggle
2. Open the [**Students**] tab
   - The table should be empty and show `0 merged student records found`. This is expected since the CSV files start empty, and no modules have been run
	- Click the AHA `Browse` button (not RQI) and open `put_this_in_resources_for_demo.csv`. Alternatively, you can type in the filepath and click `Load CSVs`
	- The table should populate with 4 rows
	- Click `Configure Columns` and check that the checkboxes work. You may also drag columns to reorder and click on headings to sort by the one clicked
	- Once done checking this, click `Reset columns` to revert to the earlier state
3. Open the [**Reminders**] tab
   - Since we loaded `put_this_in_resources_for_demo.csv`, there should now be rows there
   - This table will only show students who have not already paid/registered with Acuity, so it is intentional that it shows 3 of the 4 we saw in [**Students**] 
   - Like the previous table, you can also rearrange and resize columns. However, these changes won't save
4. Open the [**Logs**] tab
   - There should be some logs here since we clicked the toggles in [**Dashboard**] and loaded the AHA CSV file
   - Use the search bar to search any text within the **Source**, **Messsage**, and **Level** columns. For instance, type "merged" and see what happens
   - Filter further using the dropdown arrows by **Source** and **Level**. These filters can work at the same time as search
   - Click `Clear View`. This clears the visible log list, but the logs are safely saved in the log folder
   - Click `Open Log Folder`. This folder holds all of the log entries that show up in the [**Logs**] tab, even if you use `Clear View`
      - The log entries in the [**Logs**] tab are from the application directly or summarized versions of the logs for each module
      - The full-detail logs for each module will be in the folder you see here: `logs/modules/`
5. Open the [**Settings**] tab
   - In **Module Configuration**, use the dropdown to switch between config categories
      - Configs are categorized by module, and each config item has a description that should help you customize
      - Config items with sensitive fields are redacted by default. Click the eye icon to reveal/hide them
      - There is no button to update default values to ensure all changes to default values are strictly intentional. Default configs are stored in `config/default-app.properties`. You can modify this with a text editor, or copy your `app.properties` file and rename it to `default-app.properties`
   - **Module Run Commands** shows the `Java` commands used to run each module. You may edit these commands, but it is heavily advised not to
      - The **Enabled** column is synced to the toggles in [**Dashboard**] 
      - These commands require `Java 21` to be installed
   - **Microsoft Sign-in Caches** allows you to clear your sign-in caches. Your sign-in caches allow the modules to repeat without prompting you for sign-in every run
      - Use `Clear Cache` if you need to change accounts for any module
   - **Log Display** allows you to prevent the app from clearing the [**Logs**] view when the app closes
      - Enabling this will make [**Logs**] show the entire log for the current date. After enabling, you must restart the app for it to take effect
   - Finally, at the bottom, you can set the current configs as default or restore all app configs to the defaults
      - **Module Configuration** items are NOT considered "app configs"
      - "App configs" includes:
         - **Module Run Commands** 
         - **Log Display** 
         - [**Students**] table layout (if you make a mistake here, you can use `Reset Columns` to revert it to the original default layout)
         - [**Students**] **CSV Sources** (AHA and RQI filepaths)
   - Scroll back up and click `Restore Defaults` under **Module Configuration** 
       
#### Modules Walkthrough

`Reminders`
1. Open the [**Students**] tab
   - Make sure AHA says `resources\put_this_in_resources_for_demo.csv`
2. Open the [**Dashboard**] tab
   - Toggle off all modules except `Reminders`
   - Click `Start`
3. `Reminders` module is now running in **Dry Run Mode** 
   - Scroll down to watch **Recent Logs** 
   - Click `Stop` when you see `[DRY RUN] Registration reminder complete: would send 2, sent 0, skipped 2`
   - A "Dry Run" allows you to preview reminder emails without actually sending them
   - This message means, if it were a real run, it would send reminders to 2 of the 4 students it saw (recall that 1 student already paid, 1 already received a reminder, and the remaining 2 have neither paid nor received a reminder)
4. Open the [**Settings**] tab
   - Next, we are going to do a real run with **Dry Run Mode** disabled. The emails in this case are test accounts, so it's fine to do a real run on these
      - If you want to see which students will be sent reminders, check the log at `logs/modules/reminders/todays_date.log` or see [**Reminders**] 
   - In **Mod Configuration**, select the `Reminders` dropdown
   - Disable **Dry Run Mode** 
   - Click `Save Module Config`
5. Open the [**Dashboard**] tab
   - Click `Start`
   - There should be a pop-up. Click `Copy Code` then `Open Link`
   - Sign-in to the email you want to send reminder emails from
   - Return to the app
   - Click `Stop` after you see the log entry `Registration reminder complete: would send 0, sent 2, skipped 2`
      - This means the reminders were sent to 2 students
      - You may check your email for confirmation that it sent
      - Note that **Needs Follow-up** is now zero
6. Open the [**Reminders**] tab
   - You should see the two students who were "Pending" now have their statuses updated

`RQI Uploader & Email Scraper`
1. Open the [**Settings**] tab
   - Click `Restore Defaults`
   - Select `RQI Uploader & Email Scraper` from the dropdown
   - Scroll down and ensure the subcategory `Google Sheets` is accurate
      - Update **Spreadsheet IDs** if applicable
         - The spreadsheet ID is in the sheet's URL between `/d/` and `/edit?` (e.g. https://docs.google.com/spreadsheets/d/THIS_IS_YOUR_SPREADSHEET_ID/edit?gid=794248817#gid=794248817)
      - Either:
         - Share Editor access with `csc-131-service-account@csc-131-cpr-project.iam.gserviceaccount.com`(service account that will sync the CSVs to your sheets)
         - Or open edit access to `Anyone with link` (not recommended)
      - Fill in the ID and tab name fields
      - Click `Save Module Config`
      - If changed from default, you may want to copy these values over to `config/default-app.properties`
2. Open the [**Dashboard**] tab
   - Disable all toggles except `RQI Uploader & Email Scraper`
   - Click `Start`
   - You should get a sign-in popup. Sign-in to the email that you want to scan for Acuity emails
   - Click `Stop` when you see the log entry `Checked inbox: processed x messages, no new appointments`
3. Open the [**Logs**] tab
   - Click the **Source** dropdown and select `RQI Uploader & Email Scraper`
   - Scroll up and read the messages after the `Login Required` message
      - If it says `Attempted to upload to RQI. Upload successful`, you may log in to SFTP to check if it was correctly uploaded
      - Since this module reads Acuity emails, there should be no changes to the [**Reminders**] tab
4. Open the [**Students**] tab
   - If the log says `Updated x row(s) in resources/...`, you should see x new row(s)
   - Try using `Export CSVs`. Choose a folder, and it will save the AHA and RQI sheets there

`AHA Automation`
1. Open the [**Settings**] tab
   - Click `Restore Defaults`
   - Select the `AHA Automation` dropdown
   - Ensure the **Email** and **Password** are correct
      - If changed from default, you may want to copy these values over to `config/default-app.properties`
   - Decide what filters to use
   - Click `Save Module Config`
2. Open the [**Dashboard**] tab
   - Toggle off all modules except `AHA Automation`
   - Click `Start`
   - The module is in **Headless Mode**, meaning it is running a web browser in the background without any visual interface. Keep waiting until you see log messages
      - If there is an error, that's fine. It will try again in 30 seconds
      - If the process stops with no further logs, no classes were found in the date range
   - Click `Stop` after you see `Process stopped. Next AHA Automation run in 30 seconds`
3. Open the [**Students**] tab
   - The rows added by `AHA Automation` are the ones with "YES" for **AHA Reg.** 

`Outlook Event Creator`
1. Open the [**Dashboard**] 
   - Toggle off all modules except `Outlook Event Creator`
   - Click `Start`
   - Sign-in to the email that receives enrollment emails. The module will create events on this email's calendar
   - Click `Stop` when you see `Checked inbox: no new enrollment emails`
   - Any event created is shown in [**Logs**] (type "created" in the search bar)
   - Check your email to verify the event was created successfully

### Final Test
1. Open the [**Settings**] tab
   - Make and save any config changes you want to be permanent
   - Open your app folder
   - Go to `config/`
   - Make a copy of your `app.properties` file
   - Delete `default-app.properties` and rename your copy `default-app.properties`
2. Follow [these directions](#how-to-factory-reset-the-app) to reset your application
3. Click `Start` (all modules should be enabled)
4. Complete sign-ins
5. Let it run for some time and come back later to check [**Logs**] 
6. If everything is running smoothly (no disruptive errors, modules are still running, [**Students**] has been populated), click the `X` button to close the app (the `X` in the top right, NOT the `Quit` button)
7. Open your system tray (the `^` left of your WiFi and Volume icons)
8. There should be a red shield-shaped icon, <img width="12" height="12" alt="vitals_icon_title" src="https://github.com/user-attachments/assets/62f6cbba-e27a-4ba1-9be7-a3c7a8a8c131" /> `Vitals | CPR Lifeline`. Double-click it
9. The app should reopen. Confirm that the modules are still running
10. If all seems well, this concludes the demo
    - You may now close the app and let it run in the background while you use your computer
    - If you need to actually stop the app, click the `Quit` button, or right-click on the tray icon and click `Exit`


## Known Issues / Limitations

### Limitations
- The app is Windows-only because it is built with `WPF`
- `Java 21` is required for the packaged Java automation modules
- The `.exe` should not be moved away from the `config/`, `modules/`, and `resources/` folders
- Microsoft authentication requires device-login approval the first time Outlook/Graph modules run (for a total of up to 3 sign-in prompts on first boot)
- The self-contained publish folder contains many `.dll` and runtime files. It looks like a lot, but the only file users need to interact with is VitalsAutomation.exe
- `RQI Uploader & Email Scraper` only uploads to Google Sheets when a new Acuity email comes in
- `Outlook Event Creator` relies solely on enrollment emails to create calendar events
- `AHA Automation` runs on a configured date range from [**Settings**] (or `app.properties`). If a student comes in outside of the date range, they will not be accepted/recorded
- The modules, working together, will in theory keep everything up-to-date and covered. However, it is difficult to tell what will happen in practice

### Issues
- This repository contains sensitive configuration files that, although encrypted,  have a publicly accessible password
- If there are changes to the HTML of the AHA Atlas website, the `AHA Automation` module may break
- The `AHA Automation` module sometimes throws errors, usually because it cannot find a website element. This is safe to ignore since the module will restart after the set `Run Interval` value
