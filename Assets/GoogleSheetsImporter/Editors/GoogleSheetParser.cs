using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DataImporter
{
    /// <summary>
    /// Helps to parse data from Google sheets document.
    /// </summary>
    public class GoogleSheetParser
    {
        private static readonly string[] SupportedTypes = { "string", "int", "float", "bool", "Vector3" };
        
        // NOTE: Added strings are complete list of C# keywords provided by MSDN
        // Ref: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/?redirectedfrom=MSDN
        private static readonly HashSet<string> CsharpKeywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };
        
        /// <summary> Downloads CSV data from given url. </summary>
        /// <param name="url">Google sheet's url to download</param>
        /// <returns>Downloaded CSV data</returns>
        /// <exception cref="WebException">When the Google sheets is set to private (401 unauthorized)</exception>
        /// <exception cref="Exception">When download fails</exception>
        public string DownloadSheet(string url)
        {
            try
            {
                var csvUrl = ConvertToCsvExportUrl(url);
                
                using var client = new WebClient();
                client.Encoding = Encoding.UTF8;

                return client.DownloadString(csvUrl);
            }
            catch (WebException webException)
            {
                // Check for 401 Unauthorized error
                if (webException.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var errorMsg = "Failed to access spreadsheet (401 Unauthorized).\n\nTry change share option of Spreadsheet to:\n- 'Anyone with the link' can view\n- Or make it public";
                    Debug.LogError($"[GoogleSheetParser] {errorMsg}");
                    throw new Exception(errorMsg);
                }
                
                Debug.LogError($"[GoogleSheetParser] Download failed: {webException.Message}");
                throw new Exception($"Download failed: {webException.Message}");
            }
            catch (Exception commonException)
            {
                Debug.LogError($"[GoogleSheetParser] Download failed: {commonException.Message}");
                throw;
            }
        }
        
        /// <summary>Converts given google sheets url to csv export url. </summary>
        /// <param name="url">Google sheets url to convert</param>
        /// <returns>Csv export url of the Google sheets</returns>
        private string ConvertToCsvExportUrl(string url)
        {
            // Extract spreadsheet ID and gid from URL
            var match = Regex.Match(url, @"spreadsheets/d/([a-zA-Z0-9-_]+)");
            if (!match.Success)
                throw new Exception("Invalid Google Sheets URL format.");
            
            var spreadsheetId = match.Groups[1].Value;
            
            // Extract gid (sheet ID) if present
            var gid = "0"; // default first sheet
            var gidMatch = Regex.Match(url, @"[#&]gid=([0-9]+)");
            if (gidMatch.Success)
                gid = gidMatch.Groups[1].Value;
            
            return $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={gid}";
        }
        
        /// <summary>Parse csv metadata including sheet name and data information with validation</summary>
        /// <param name="csvData">Csv data to parse</param>
        /// <param name="sheetName">Name of sheet (1st row of csv represents sheet's name)</param>
        /// <param name="fieldNames">Names of fields defined in sheet</param>
        /// <param name="fieldTypes">Types of fields defined in sheet</param>
        /// <param name="errorMessage">error message caused failure</param>
        /// <returns>Returns TRUE if validation and parsing completed successfully</returns>
        public bool TryValidateAndParseCsvData(
            string csvData,
            out string sheetName, out string[] fieldNames, out string[] fieldTypes, out string errorMessage)
        {
            sheetName = "";
            fieldNames = null;
            fieldTypes = null;
            errorMessage = "";
            
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 4)
            {
                errorMessage = "CSV must have at least 4 rows (sheet name, field names, field types, and data).";
                return false;
            }
            
            // Parse first row (sheet name)
            var firstRow = SplitCsvLine(lines[0]);
            sheetName = GenerateClassName(firstRow[0]);
            if (string.IsNullOrEmpty(sheetName))
            {
                errorMessage = "Sheet name (first row, first cell) is empty or invalid.";
                return false;
            }
            
            // Parse second row (field names)
            fieldNames = SplitCsvLine(lines[1]);
            
            // Parse third row (field types)
            fieldTypes = SplitCsvLine(lines[2]);
            
            if (fieldNames.Length != fieldTypes.Length)
            {
                errorMessage = "Field names and field types count mismatch.";
                return false;
            }
            
            // Validate field names
            if (!TryValidateFieldNames(fieldNames, out errorMessage))
                return false;
            
            // Validate field types
            if (!TryValidateFieldTypes(fieldTypes, out errorMessage))
                return false;
            
            return true;
        }
        
        /// <summary>Check if the given field names are valid</summary>
        /// <param name="fieldNames">Field names to validate</param>
        /// <param name="errorMessage">Error message causes failure</param>
        /// <returns>TRUE if all field names are valid</returns>
        private bool TryValidateFieldNames(string[] fieldNames, out string errorMessage)
        {
            errorMessage = "";
            var invalidFields = new List<string>();
            
            for (var i = 0; i < fieldNames.Length; i++)
            {
                var fieldName = fieldNames[i].Trim();
                
                if (string.IsNullOrEmpty(fieldName))
                {
                    invalidFields.Add($"Column {i + 1}: Empty field name");
                    continue;
                }
                
                // Check if starts with underscore
                if (fieldName.StartsWith("_"))
                {
                    invalidFields.Add($"'{fieldName}': Cannot start with underscore");
                    continue;
                }
                
                // Check if ends with underscore
                if (fieldName.EndsWith("_"))
                {
                    invalidFields.Add($"'{fieldName}': Cannot end with underscore");
                    continue;
                }
                
                // Check if starts with digit
                if (char.IsDigit(fieldName[0]))
                {
                    invalidFields.Add($"'{fieldName}': Cannot start with digit");
                    continue;
                }
                
                // Check for invalid characters (only alphanumeric and underscore allowed)
                if (!Regex.IsMatch(fieldName, @"^[a-zA-Z0-9_]+$"))
                {
                    invalidFields.Add($"'{fieldName}': Contains invalid characters (only letters, numbers, and _ allowed)");
                    continue;
                }
                
                // Check if C# keyword
                if (CsharpKeywords.Contains(fieldName))
                {
                    invalidFields.Add($"'{fieldName}': C# keyword cannot be used as field name");
                    continue;
                }
                
                // Capitalize first letter
                fieldNames[i] = CapitalizeFirstLetter(fieldName);
            }
            
            if (invalidFields.Count > 0)
            {
                errorMessage = "Invalid field names detected:\n" + string.Join("\n", invalidFields);
                return false;
            }
            
            return true;
        }
        
        /// <summary>Check if the given field types are valid</summary>
        /// <param name="fieldTypes">Field types to validate</param>
        /// <param name="errorMessage">Error message causes failure</param>
        /// <returns>TRUE if all field types are valid</returns>
        private bool TryValidateFieldTypes(string[] fieldTypes, out string errorMessage)
        {
            errorMessage = "";
            var invalidTypes = new List<string>();
            
            for (var i = 0; i < fieldTypes.Length; i++)
            {
                var fieldType = fieldTypes[i].Trim();
                
                if (!SupportedTypes.Contains(fieldType))
                    invalidTypes.Add($"Column {i + 1}: '{fieldType}' (supported types: {string.Join(", ", SupportedTypes)})");
            }
            
            if (invalidTypes.Count > 0)
            {
                errorMessage = "Invalid field types detected:\n" + string.Join("\n", invalidTypes);
                return false;
            }
            
            return true;
        }
        
        /// <summary>Get data only csv</summary>
        /// <param name="csvData">Csv data to modify</param>
        /// <returns>Csv string that only contains data</returns>
        public string GetDataOnlyCsv(string csvData)
        {
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var dataLines = new List<string>();
            
            // Skip first three rows (sheet name, field names, types)
            for (var i = 2; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                // Parse and remove first column
                var cells = SplitCsvLine(line);
                if (cells.Length <= 1) continue;
                
                var dataCells = new string[cells.Length - 1];
                Array.Copy(cells, 1, dataCells, 0, dataCells.Length);
                    
                // Reconstruct CSV line
                dataLines.Add(string.Join(",", dataCells));
            }
            
            return string.Join("\n", dataLines);
        }
        
        /// <summary>Splits given csv line into separated field</summary>
        /// <param name="line">Csv line to split</param>
        /// <returns>Split csv line into separated field</returns>
        private string[] SplitCsvLine(string line)
        {
            var values = new List<string>();
            var inQuotes = false;
            var currentValue = new StringBuilder();
            
            // Since strings containing commas may exist within CSV lines, we check them one character at a time.
            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }
            
            values.Add(currentValue.ToString().Trim());
            
            return values.ToArray();
        }
        
        private string GenerateClassName(string fieldName)
        {
            // Remove invalid characters and capitalize
            var className = Regex.Replace(fieldName, @"[^a-zA-Z0-9_]", "");
            return CapitalizeFirstLetter(className);
        }
        
        private static string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            return char.ToUpper(str[0]) + str.Substring(1);
        }
    }
}