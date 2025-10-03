using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace DataImporter
{
    public class GoogleSheetImporter : EditorWindow
    {
        private const string PrefsKeyURL = "GoogleSheetImporter_LastURL";
        private const string PrefsKeySaveLocation = "GoogleSheetImporter_SaveLocation";
        
        private string m_SheetUrl = "";
        private SaveLocation m_SaveLocation = SaveLocation.Resources;
        private bool m_IsProcessing = false;
        private string m_SuccessMessage = "";
        private double m_SuccessMessageTime = 0;
        
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
            m_SheetUrl = EditorPrefs.GetString(PrefsKeyURL, "");
            m_SaveLocation = (SaveLocation)EditorPrefs.GetInt(PrefsKeySaveLocation, 0);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Google Sheet Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // URL Input
            EditorGUILayout.LabelField("Google Sheet URL", EditorStyles.label);
            var newUrl = EditorGUILayout.TextField(m_SheetUrl);
            if (newUrl != m_SheetUrl)
            {
                m_SheetUrl = newUrl;
                EditorPrefs.SetString(PrefsKeyURL, m_SheetUrl);
            }
            
            EditorGUILayout.Space(10);
            
            // Save Location
            EditorGUILayout.LabelField("Save Location", EditorStyles.label);
            var newLocation = (SaveLocation)EditorGUILayout.EnumPopup(m_SaveLocation);
            if (newLocation != m_SaveLocation)
            {
                m_SaveLocation = newLocation;
                EditorPrefs.SetInt(PrefsKeySaveLocation, (int)m_SaveLocation);
            }
            
            EditorGUILayout.Space(10);
            
            // Display save path info
            var savePath = GetSaveFolder();
            EditorGUILayout.HelpBox($"Files will be saved to: {savePath}", MessageType.Info);
            EditorGUILayout.Space(10);
            GUI.enabled = !m_IsProcessing && !string.IsNullOrEmpty(m_SheetUrl);
            
            // Import CSV Button
            if (GUILayout.Button("Import CSV", GUILayout.Height(30)))
                ImportCsv();
            
            EditorGUILayout.Space(5);
            
            // Generate Data Class Button
            if (GUILayout.Button("Generate Data Class", GUILayout.Height(30)))
                GenerateDataClass();
            
            GUI.enabled = true;
            
            if (m_IsProcessing)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Processing...", EditorStyles.centeredGreyMiniLabel);
            }
            
            // Display success message
            if (!string.IsNullOrEmpty(m_SuccessMessage) && EditorApplication.timeSinceStartup - m_SuccessMessageTime < 5.0)
            {
                EditorGUILayout.Space(10);
                
                var successStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.2f, 0.8f, 0.2f) },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                EditorGUILayout.LabelField(m_SuccessMessage, successStyle);
                Repaint();
            }
        }
        
        private string GetSaveFolder()
        {
            var baseFolder = m_SaveLocation == SaveLocation.Resources ? "Assets/Resources" : "Assets/StreamingAssets";
            return $"{baseFolder}/Database";
        }
        
        private void ImportCsv()
        {
            m_IsProcessing = true;
            
            try
            {
                // Parse sheet URL and download
                var parser = new GoogleSheetParser();
                var csvData = parser.DownloadSheet(m_SheetUrl);
                
                if (string.IsNullOrEmpty(csvData))
                {
                    DisplayErrorMessage("Failed to download sheet data.");
                    return;
                }
                
                // Validate and parse
                if (!parser.TryValidateAndParseCsvData(csvData, out string sheetName, out string[] fieldNames, out string[] fieldTypes, out string error))
                {
                    DisplayErrorMessage(error);
                    return;
                }
                
                // Prepare save path
                var saveFolder = GetSaveFolder();
                if (!Directory.Exists(saveFolder))
                    Directory.CreateDirectory(saveFolder);
                
                var fileName = $"{sheetName}.csv";
                var fullPath = Path.Combine(saveFolder, fileName);
                
                // Check if file exists
                if (File.Exists(fullPath) &&
                    !EditorUtility.DisplayDialog("Overwrite File?", $"File '{fileName}' already exists. Do you want to overwrite it?", "Yes", "No"))
                    return;
                
                // Save CSV without header rows (only data rows)
                var dataOnlyCsv = parser.GetDataOnlyCsv(csvData);
                File.WriteAllText(fullPath, dataOnlyCsv);
                
                AssetDatabase.Refresh();
                
                // Show success message in editor
                m_SuccessMessage = $"Successfully Created {sheetName} CSV File!";
                m_SuccessMessageTime = EditorApplication.timeSinceStartup;
                
                Debug.Log($"[GoogleSheetImporter] CSV imported: {fullPath}");
            }
            catch (Exception ex)
            {
                DisplayErrorMessage($"Unexpected error: {ex.Message}");
                Debug.LogError($"[GoogleSheetImporter] Error: {ex}");
            }
            finally
            {
                m_IsProcessing = false;
                Repaint();
            }
        }
        
        private void GenerateDataClass()
        {
            m_IsProcessing = true;
            
            try
            {
                // Parse sheet URL and download
                var parser = new GoogleSheetParser();
                var csvData = parser.DownloadSheet(m_SheetUrl);
                
                if (string.IsNullOrEmpty(csvData))
                {
                    DisplayErrorMessage("Failed to download sheet data.");
                    return;
                }
                
                // Validate and parse
                if (!parser.TryValidateAndParseCsvData(csvData, out string sheetName, out string[] fieldNames, out string[] fieldTypes, out string error))
                {
                    DisplayErrorMessage(error);
                    return;
                }
                
                // Generate class files
                var generator = new DataClassGenerator();
                if (!generator.TryGenerateClass(sheetName, fieldNames, fieldTypes, out string classPath, out string collectionPath, out error))
                {
                    DisplayErrorMessage(error);
                    return;
                }
                
                AssetDatabase.Refresh();
                
                // Show success message in editor
                m_SuccessMessage = $"Successfully Created {sheetName} Data Class!";
                m_SuccessMessageTime = EditorApplication.timeSinceStartup;
                
                Debug.Log($"[GoogleSheetImporter] Classes generated:\n- {classPath}\n- {collectionPath}");
            }
            catch (Exception ex)
            {
                DisplayErrorMessage($"Unexpected error: {ex.Message}");
                Debug.LogError($"[GoogleSheetImporter] Error: {ex}");
            }
            finally
            {
                m_IsProcessing = false;
                Repaint();
            }
        }
        
        private void DisplayErrorMessage(string message)
        {
            EditorUtility.DisplayDialog("Error", message, "OK");
            Debug.LogError($"[GoogleSheetImporter] {message}");
        }
    }
}