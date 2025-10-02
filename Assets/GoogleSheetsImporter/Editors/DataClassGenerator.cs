using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DataImporter
{
    public class DataClassGenerator
    {
        private const string CLASS_FOLDER = "Assets/Scripts/Database";
        private const string NAMESPACE = "Data";
        
        public bool GenerateClass(string className, string[] fieldNames, string[] fieldTypes, 
            out string classPath, out string collectionPath, out string error)
        {
            classPath = "";
            collectionPath = "";
            error = "";
            
            // Ensure folder exists
            if (!Directory.Exists(CLASS_FOLDER))
            {
                Directory.CreateDirectory(CLASS_FOLDER);
            }
            
            // Capitalize class name
            className = CapitalizeFirstLetter(className);
            
            // Generate paths
            classPath = Path.Combine(CLASS_FOLDER, $"{className}.cs");
            collectionPath = Path.Combine(CLASS_FOLDER, $"{className}Collection.cs");
            
            // Check if files exist
            if (File.Exists(classPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite Class?", 
                    $"Class '{className}.cs' already exists. Do you want to overwrite it?", 
                    "Yes", "No"))
                {
                    error = "Class generation cancelled by user.";
                    return false;
                }
            }
            
            if (File.Exists(collectionPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite Collection Class?", 
                    $"Class '{className}Collection.cs' already exists. Do you want to overwrite it?", 
                    "Yes", "No"))
                {
                    error = "Collection class generation cancelled by user.";
                    return false;
                }
            }
            
            try
            {
                // Generate data class
                string classContent = GenerateDataClassContent(className, fieldNames, fieldTypes);
                File.WriteAllText(classPath, classContent);
                
                // Generate collection class
                string collectionContent = GenerateCollectionClassContent(className);
                File.WriteAllText(collectionPath, collectionContent);
                
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to generate classes: {ex.Message}";
                
                // Rollback - delete created files
                try
                {
                    if (File.Exists(classPath))
                        File.Delete(classPath);
                    if (File.Exists(collectionPath))
                        File.Delete(collectionPath);
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
            sb.AppendLine($"namespace {NAMESPACE}");
            sb.AppendLine("{");
            sb.AppendLine($"    [System.Serializable]");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");
            
            // Fields
            for (int i = 0; i < fieldNames.Length; i++)
            {
                string fieldName = fieldNames[i];
                string fieldType = fieldTypes[i];
                
                // Make first letter lowercase for field names
                string privateFieldName = MakeFirstLetterLowercase(fieldName);
                
                sb.AppendLine($"        public {fieldType} {privateFieldName};");
            }
            
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
            sb.AppendLine($"namespace {NAMESPACE}");
            sb.AppendLine("{");
            sb.AppendLine($"    [System.Serializable]");
            sb.AppendLine($"    public class {className}Collection");
            sb.AppendLine("    {");
            sb.AppendLine($"        public List<{className}> items = new List<{className}>();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            return char.ToUpper(str[0]) + str.Substring(1);
        }
        
        private string MakeFirstLetterLowercase(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            return char.ToLower(str[0]) + str.Substring(1);
        }
    }
}