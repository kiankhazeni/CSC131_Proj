using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using UserInterface.Models;

namespace UserInterface.Services
{
    public class CsvStudentService
    {
        public List<StudentRecord> LoadStudents(string preprodPath, string ahaPath)
        {
            var preprodRows = LoadPreprodRows(preprodPath);
            var ahaRows = LoadAhaRows(ahaPath);

            var ahaByEmail = ahaRows
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .GroupBy(x => Normalize(x.Email))
                .ToDictionary(g => g.Key, g => g.First());

            var results = new List<StudentRecord>();

            foreach (var preprod in preprodRows)
            {
                ahaByEmail.TryGetValue(Normalize(preprod.Email), out var aha);

                results.Add(new StudentRecord
                {
                    Email = Pick(preprod.Email, aha?.Email),
                    FirstName = Pick(preprod.FirstName, aha?.FirstName),
                    MiddleName = Pick(preprod.MiddleName, aha?.MiddleName),
                    LastName = Pick(preprod.LastName, aha?.LastName),

                    Phone = aha?.Phone ?? string.Empty,
                    Course = aha?.Course ?? string.Empty,
                    Date = aha?.Date ?? string.Empty,
                    AcuityRegistration = aha?.AcuityRegistration ?? string.Empty,
                    AhaRegistration = aha?.AhaRegistration ?? string.Empty,
                    ReminderEmailSent = aha?.ReminderEmailSent ?? string.Empty,

                    LocationName = preprod.LocationName,
                    Status = preprod.Status,
                    Group = preprod.Group
                });
            }

            return results
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ToList();
        }

        private List<PreprodRow> LoadPreprodRows(string filePath)
        {
            var rows = new List<PreprodRow>();

            if (!File.Exists(filePath))
                return rows;

            using var parser = CreateParser(filePath);

            if (parser.EndOfData)
                return rows;

            parser.ReadFields();

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null)
                    continue;

                rows.Add(new PreprodRow
                {
                    LocationName = GetField(fields, 1),
                    FirstName = GetField(fields, 3),
                    MiddleName = GetField(fields, 4),
                    LastName = GetField(fields, 5),
                    Email = GetField(fields, 6),
                    Status = GetField(fields, 10),
                    Group = GetField(fields, 16)
                });
            }

            return rows;
        }

        private List<AhaRow> LoadAhaRows(string filePath)
        {
            var rows = new List<AhaRow>();

            if (!File.Exists(filePath))
                return rows;

            using var parser = CreateParser(filePath);

            if (parser.EndOfData)
                return rows;

            parser.ReadFields();

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null)
                    continue;

                rows.Add(new AhaRow
                {
                    Email = GetField(fields, 0),
                    FirstName = GetField(fields, 1),
                    MiddleName = GetField(fields, 2),
                    LastName = GetField(fields, 3),
                    Phone = GetField(fields, 4),
                    Course = GetField(fields, 5),
                    Date = GetField(fields, 6),
                    AcuityRegistration = GetField(fields, 7),
                    AhaRegistration = GetField(fields, 8),
                    ReminderEmailSent = GetField(fields, 9)
                });
            }

            return rows;
        }

        private TextFieldParser CreateParser(string filePath)
        {
            var parser = new TextFieldParser(filePath);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = false;
            return parser;
        }

        private static string GetField(string[] fields, int index)
        {
            if (index < 0 || index >= fields.Length)
                return string.Empty;

            return fields[index]?.Trim() ?? string.Empty;
        }

        private static string Normalize(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string Pick(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : (second ?? string.Empty);
        }

        private class PreprodRow
        {
            public string LocationName { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Group { get; set; } = string.Empty;
        }

        private class AhaRow
        {
            public string Email { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Course { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
            public string AcuityRegistration { get; set; } = string.Empty;
            public string AhaRegistration { get; set; } = string.Empty;
            public string ReminderEmailSent { get; set; } = string.Empty;
        }
    }
}