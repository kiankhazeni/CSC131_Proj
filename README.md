# CSC131_Proj

## Part 1:

## Part 2: 

## Part 3: Email Reader for RQI
This program covers the portion of automation mentioned in Video #3 in the class announcements. The basic procedure is lised below. For more detailed information, please see [the specifications](#specifications)
1. Read last processed UID
2. Fetch recent emails from Gmail
3. Loop through emails
   - Skip if UID >= last processed UID
   - Skip if subject does not contain "appointment"
   - Extract relevant fields
   - Write extracted info to temporary CSVs and raw text file
   - Update highest UID
4. Merge temporary CSVs with running files
5. Upload merged CSVs to Google Sheets

### Specifications
1. Email Access and filtering:
  - Connects to Gmail via IMAP using `jakarta.mail`
  - Reads from Inbox
    - Can change to different folder if necessary (set up rule to route all relevant emails to x folder?)
  - Only reads emails:
    - From past 2 weeks
    - Up to maximum of 50 (can set config)
    - Has "Appointment" (case-insensitive) in subject line
2. Duplicates:
  - Tracked via Gmail's internal UIDs
3. Content Extraction
  - Compatible with plaintext, HTML, and multipart formats (tested with emails from Dropbox)
  - Extracts fields and maps Group and LocationName fields to provided values
  - Splits names into first, middle, last
  - Formats dates as M/d/yyyy
4. CSV Handling
  - 2 main CSVs:
    - RQI CSV: `preprod_cl.csv`
    - AHA CSV: `aha.csv`
  - Uses temporary CSVs to check for duplicates before merging
5. Google Sheets Upload
  - Appends CSV data to Google Sheet (not overwrite)
  - Includes headers if sheet empty
6. Logging & Output
  - Prints email subject, sender, recipient, and date to console
  - Saves raw email data to emails.txt
  - Suppresses warnings for demo purposes (disable this if you want to see more while debugging)
 
### To-Do
[ ] Clean up code
[ ] Config file
  - Max emails to read
  - How recent to read
  - Inbox or other email folder
[ ] Transition to Outlook in possible
  - Currently using workaround (using a rule to forward any emails arriving in inbox to Gmail)
[ ] Run in background
[ ] GUI
