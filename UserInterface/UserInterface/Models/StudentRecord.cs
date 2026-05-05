namespace UserInterface.Models
{
    public class StudentRecord
    {
        // Shared / merged identity
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // From aha.csv
        public string Phone { get; set; } = string.Empty;
        public string Course { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string AcuityRegistration { get; set; } = string.Empty;
        public string AhaRegistration { get; set; } = string.Empty;
        public string ReminderEmailSent { get; set; } = string.Empty;

        // From preprod_cl.csv
        public string LocationName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;

        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}