using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DataImporter
{
    public class GoogleSheetParser
    {
        private static readonly string[] SUPPORTED_TYPES = { "string", "int", "float", "bool", "Vector3" };
        private static readonly HashSet<string> CSHARP_KEYWORDS = new HashSet<string>
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
        
        public string DownloadSheet(string url)
        {
            try
            {
                // Convert Google Sheets URL to CSV export URL
                string csvUrl = ConvertToCsvExportUrl(url);
                
                using (WebClient client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    return client.DownloadString(csvUrl);
                }
            }
            catch (WebException webEx)
            {
                // Check for 401 Unauthorized error
                if (webEx.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    string errorMsg = "Failed to access spreadsheet (401 Unauthorized).\n\nTry change share option of Spreadsheet to:\n- 'Anyone with the link' can view\n- Or make it public";
                    Debug.LogError($"[GoogleSheetParser] {errorMsg}");
                    throw new Exception(errorMsg);
                }
                
                Debug.LogError($"[GoogleSheetParser] Download failed: {webEx.Message}");
                throw new Exception($"Download failed: {webEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GoogleSheetParser] Download failed: {ex.Message}");
                throw;
            }
        }
        
        private string ConvertToCsvExportUrl(string url)
        {
            // Extract spreadsheet ID and gid from URL
            var match = Regex.Match(url, @"spreadsheets/d/([a-zA-Z0-9-_]+)");
            if (!match.Success)
            {
                throw new Exception("Invalid Google Sheets URL format.");
            }
            
            string spreadsheetId = match.Groups[1].Value;
            
            // Extract gid (sheet ID) if present
            string gid = "0"; // default first sheet
            var gidMatch = Regex.Match(url, @"[#&]gid=([0-9]+)");
            if (gidMatch.Success)
            {
                gid = gidMatch.Groups[1].Value;
            }
            
            return $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={gid}";
        }
        
        public bool ValidateAndParse(string csvData, out string sheetName, out string[] fieldNames, out string[] fieldTypes, out string error)
        {
            sheetName = "";
            fieldNames = null;
            fieldTypes = null;
            error = "";
            
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length < 4)
            {
                error = "CSV must have at least 4 rows (sheet name, field names, field types, and data).";
                return false;
            }
            
            // Parse first row (sheet name)
            string[] firstRow = ParseCsvLine(lines[0]);
            sheetName = GenerateClassName(firstRow[0]);
            if (string.IsNullOrEmpty(sheetName))
            {
                error = "Sheet name (first row, first cell) is empty or invalid.";
                return false;
            }
            
            // Parse second row (field names)
            fieldNames = ParseCsvLine(lines[1]);
            
            // Parse third row (field types)
            fieldTypes = ParseCsvLine(lines[2]);
            
            if (fieldNames.Length != fieldTypes.Length)
            {
                error = "Field names and field types count mismatch.";
                return false;
            }
            
            // Validate field names
            if (!ValidateFieldNames(fieldNames, out error))
            {
                return false;
            }
            
            // Validate field types
            if (!ValidateFieldTypes(fieldTypes, out error))
            {
                return false;
            }
            
            return true;
        }
        
        private bool ValidateFieldNames(string[] fieldNames, out string error)
        {
            error = "";
            var invalidFields = new List<string>();
            
            for (int i = 0; i < fieldNames.Length; i++)
            {
                string fieldName = fieldNames[i].Trim();
                
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
                if (CSHARP_KEYWORDS.Contains(fieldName.ToLower()))
                {
                    invalidFields.Add($"'{fieldName}': C# keyword cannot be used as field name");
                    continue;
                }
                
                // Capitalize first letter
                fieldNames[i] = CapitalizeFirstLetter(fieldName);
            }
            
            if (invalidFields.Count > 0)
            {
                error = "Invalid field names detected:\n" + string.Join("\n", invalidFields);
                return false;
            }
            
            return true;
        }
        
        private bool ValidateFieldTypes(string[] fieldTypes, out string error)
        {
            error = "";
            var invalidTypes = new List<string>();
            
            for (int i = 0; i < fieldTypes.Length; i++)
            {
                string fieldType = fieldTypes[i].Trim();
                
                if (!SUPPORTED_TYPES.Contains(fieldType))
                {
                    invalidTypes.Add($"Column {i + 1}: '{fieldType}' (supported types: {string.Join(", ", SUPPORTED_TYPES)})");
                }
            }
            
            if (invalidTypes.Count > 0)
            {
                error = "Invalid field types detected:\n" + string.Join("\n", invalidTypes);
                return false;
            }
            
            return true;
        }
        
        public string GetDataOnlyCsv(string csvData)
        {
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Skip first two rows (sheet name/field names and types)
            // Also remove first column (sheet name column) from data rows
            var dataLines = new List<string>();
            
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                // Parse and remove first column
                string[] cells = ParseCsvLine(line);
                if (cells.Length > 1)
                {
                    string[] dataCells = new string[cells.Length - 1];
                    Array.Copy(cells, 1, dataCells, 0, dataCells.Length);
                    
                    // Reconstruct CSV line
                    dataLines.Add(string.Join(",", dataCells));
                }
            }
            
            return string.Join("\n", dataLines);
        }
        
        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            bool inQuotes = false;
            var currentValue = new StringBuilder();
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
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
            string className = Regex.Replace(fieldName, @"[^a-zA-Z0-9_]", "");
            return CapitalizeFirstLetter(className);
        }
        
        private string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            return char.ToUpper(str[0]) + str.Substring(1);
        }
        
        public static Vector3 ParseVector3(string value, out bool success)
        {
            success = false;
            
            if (string.IsNullOrWhiteSpace(value))
            {
                return Vector3.zero;
            }
            
            // Remove parentheses and split by comma
            string cleaned = value.Trim().Trim('(', ')');
            string[] parts = cleaned.Split(',');
            
            if (parts.Length != 3)
            {
                Debug.LogError($"[GoogleSheetParser] Invalid Vector3 format: '{value}'. Expected 3 values separated by commas.");
                return Vector3.zero;
            }
            
            try
            {
                float x = float.Parse(parts[0].Trim());
                float y = float.Parse(parts[1].Trim());
                float z = float.Parse(parts[2].Trim());
                
                success = true;
                return new Vector3(x, y, z);
            }
            catch
            {
                Debug.LogError($"[GoogleSheetParser] Failed to parse Vector3: '{value}'");
                return Vector3.zero;
            }
        }
    }
}