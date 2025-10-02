using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace DataImporter
{
    public class GoogleSheetImporter : EditorWindow
    {
        private const string PREFS_KEY_URL = "GoogleSheetImporter_LastURL";
        private const string PREFS_KEY_SAVE_LOCATION = "GoogleSheetImporter_SaveLocation";
        
        private string sheetUrl = "";
        private SaveLocation saveLocation = SaveLocation.Resources;
        private bool isProcessing = false;
        private string successMessage = "";
        private double successMessageTime = 0;
        
        private enum SaveLocation
        {
            Resources,
            StreamingAssets
        }
        
        [MenuItem("Tools/Google Sheet Importer")]
        public static void ShowWindow()
        {
            GetWindow<GoogleSheetImporter>("Google Sheet Importer");
        }
        
        private void OnEnable()
        {
            // Load last used URL and save location
            sheetUrl = EditorPrefs.GetString(PREFS_KEY_URL, "");
            saveLocation = (SaveLocation)EditorPrefs.GetInt(PREFS_KEY_SAVE_LOCATION, 0);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Google Sheet Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // URL Input
            EditorGUILayout.LabelField("Google Sheet URL", EditorStyles.label);
            string newUrl = EditorGUILayout.TextField(sheetUrl);
            if (newUrl != sheetUrl)
            {
                sheetUrl = newUrl;
                EditorPrefs.SetString(PREFS_KEY_URL, sheetUrl);
            }
            
            EditorGUILayout.Space(10);
            
            // Save Location
            EditorGUILayout.LabelField("Save Location", EditorStyles.label);
            SaveLocation newLocation = (SaveLocation)EditorGUILayout.EnumPopup(saveLocation);
            if (newLocation != saveLocation)
            {
                saveLocation = newLocation;
                EditorPrefs.SetInt(PREFS_KEY_SAVE_LOCATION, (int)saveLocation);
            }
            
            EditorGUILayout.Space(10);
            
            // Display save path info
            string savePath = GetSaveFolder();
            EditorGUILayout.HelpBox($"Files will be saved to: {savePath}", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            GUI.enabled = !isProcessing && !string.IsNullOrEmpty(sheetUrl);
            
            // Import CSV Button
            if (GUILayout.Button("Import CSV", GUILayout.Height(30)))
            {
                ImportCSV();
            }
            
            EditorGUILayout.Space(5);
            
            // Generate Data Class Button
            if (GUILayout.Button("Generate Data Class", GUILayout.Height(30)))
            {
                GenerateDataClass();
            }
            
            GUI.enabled = true;
            
            if (isProcessing)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Processing...", EditorStyles.centeredGreyMiniLabel);
            }
            
            // Display success message
            if (!string.IsNullOrEmpty(successMessage) && EditorApplication.timeSinceStartup - successMessageTime < 5.0)
            {
                EditorGUILayout.Space(10);
                
                GUIStyle successStyle = new GUIStyle(EditorStyles.label);
                successStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                successStyle.fontStyle = FontStyle.Bold;
                successStyle.alignment = TextAnchor.MiddleCenter;
                
                EditorGUILayout.LabelField(successMessage, successStyle);
                Repaint();
            }
        }
        
        private string GetSaveFolder()
        {
            string baseFolder = saveLocation == SaveLocation.Resources ? "Assets/Resources" : "Assets/StreamingAssets";
            return $"{baseFolder}/Database";
        }
        
        private void ImportCSV()
        {
            isProcessing = true;
            
            try
            {
                // Parse sheet URL and download
                var parser = new GoogleSheetParser();
                var csvData = parser.DownloadSheet(sheetUrl);
                
                if (string.IsNullOrEmpty(csvData))
                {
                    ShowError("Failed to download sheet data.");
                    return;
                }
                
                // Validate and parse
                if (!parser.ValidateAndParse(csvData, out string sheetName, out string[] fieldNames, out string[] fieldTypes, out string error))
                {
                    ShowError(error);
                    return;
                }
                
                // Prepare save path
                string saveFolder = GetSaveFolder();
                if (!Directory.Exists(saveFolder))
                {
                    Directory.CreateDirectory(saveFolder);
                }
                
                string fileName = $"{sheetName}.csv";
                string fullPath = Path.Combine(saveFolder, fileName);
                
                // Check if file exists
                if (File.Exists(fullPath))
                {
                    if (!EditorUtility.DisplayDialog("Overwrite File?", 
                        $"File '{fileName}' already exists. Do you want to overwrite it?", 
                        "Yes", "No"))
                    {
                        return;
                    }
                }
                
                // Save CSV without header rows (only data rows)
                string dataOnlyCsv = parser.GetDataOnlyCsv(csvData);
                File.WriteAllText(fullPath, dataOnlyCsv);
                
                AssetDatabase.Refresh();
                
                // Show success message in editor
                successMessage = $"Successfully Created {sheetName} CSV File!";
                successMessageTime = EditorApplication.timeSinceStartup;
                
                Debug.Log($"[GoogleSheetImporter] CSV imported: {fullPath}");
            }
            catch (Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                Debug.LogError($"[GoogleSheetImporter] Error: {ex}");
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        private void GenerateDataClass()
        {
            isProcessing = true;
            
            try
            {
                // Parse sheet URL and download
                var parser = new GoogleSheetParser();
                var csvData = parser.DownloadSheet(sheetUrl);
                
                if (string.IsNullOrEmpty(csvData))
                {
                    ShowError("Failed to download sheet data.");
                    return;
                }
                
                // Validate and parse
                if (!parser.ValidateAndParse(csvData, out string sheetName, out string[] fieldNames, out string[] fieldTypes, out string error))
                {
                    ShowError(error);
                    return;
                }
                
                // Generate class files
                var generator = new DataClassGenerator();
                if (!generator.GenerateClass(sheetName, fieldNames, fieldTypes, out string classPath, out string collectionPath, out error))
                {
                    ShowError(error);
                    return;
                }
                
                AssetDatabase.Refresh();
                
                // Show success message in editor
                successMessage = $"Successfully Created {sheetName} Data Class!";
                successMessageTime = EditorApplication.timeSinceStartup;
                
                Debug.Log($"[GoogleSheetImporter] Classes generated:\n- {classPath}\n- {collectionPath}");
            }
            catch (Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                Debug.LogError($"[GoogleSheetImporter] Error: {ex}");
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        private void ShowError(string message)
        {
            EditorUtility.DisplayDialog("Error", message, "OK");
            Debug.LogError($"[GoogleSheetImporter] {message}");
        }
    }
}