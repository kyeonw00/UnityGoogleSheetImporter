using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DataImporter
{
    public class DataClassGenerator
    {
        private const string ClassFolder = "Assets/Scripts/Database";
        private const string Namespace = "Data";
        
        public bool TryGenerateClass(string className, string[] fieldNames, string[] fieldTypes, 
            out string classPath, out string collectionPath, out string error)
        {
            classPath = "";
            collectionPath = "";
            error = "";
            
            // Ensure folder exists
            if (!Directory.Exists(ClassFolder))
                Directory.CreateDirectory(ClassFolder);
            
            // Capitalize class name
            className = CapitalizeFirstLetter(className);
            
            // Generate paths
            classPath = Path.Combine(ClassFolder, $"{className}.cs");
            collectionPath = Path.Combine(ClassFolder, $"{className}Collection.cs");
            
            // Check if files exist
            if (File.Exists(classPath) &&
                !EditorUtility.DisplayDialog("Overwrite Class?", $"Class '{className}.cs' already exists. Do you want to overwrite it?", "Yes", "No"))
            {
                error = "Class generation cancelled by user.";
                return false;
            }
            
            // Also check for collection wrapper
            if (File.Exists(collectionPath) &&
                !EditorUtility.DisplayDialog("Overwrite Collection Class?", $"Class '{className}Collection.cs' already exists. Do you want to overwrite it?", "Yes", "No"))
            {
                error = "Collection class generation cancelled by user.";
                return false;
            }
            
            try
            {
                // Generate data class
                var classContent = GenerateDataClassContent(className, fieldNames, fieldTypes);
                File.WriteAllText(classPath, classContent);
                
                // Generate collection class
                var collectionContent = GenerateCollectionClassContent(className);
                File.WriteAllText(collectionPath, collectionContent);
                
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to generate classes: {ex.Message}";
                
                // Rollback - delete created files
                try
                {
                    if (File.Exists(classPath)) File.Delete(classPath);
                    if (File.Exists(collectionPath)) File.Delete(collectionPath);
                }
                catch (Exception rollbackEx)
                {
                    Debug.LogError($"[DataClassGenerator] Rollback failed: {rollbackEx.Message}");
                }
                
                return false;
            }
        }
        
        private string GenerateDataClassContent(string className, string[] fieldNames, string[] fieldTypes)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    [System.Serializable]");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");
            
            // Fields
            for (var i = 0; i < fieldNames.Length; i++)
            {
                var fieldName = fieldNames[i];
                var fieldType = fieldTypes[i];
                
                // Make first letter lowercase for field names
                var privateFieldName = MakeFirstLetterLowercase(fieldName);
                
                sb.AppendLine($"        public {fieldType} {privateFieldName};");
            }
            
            // Close scopes
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private string GenerateCollectionClassContent(string className)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine($"namespace {Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    [System.Serializable]");
            sb.AppendLine($"    public class {className}Collection");
            sb.AppendLine("    {");
            sb.AppendLine($"        public List<{className}> items = new List<{className}>();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
        
        private static string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return char.ToUpper(str[0]) + str.Substring(1);
        }
        
        private static string MakeFirstLetterLowercase(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return char.ToLower(str[0]) + str.Substring(1);
        }
    }
}