using UnityEngine;

namespace DataImporter
{
    /// <summary>
    /// Utility class for parsing string value into generic value.
    /// </summary>
    public static class DataParsingUtility
    {
        /// <summary>Try parses `UnityEngine.Vector3` from given string.</summary>
        /// <param name="value">String to parse</param>
        /// <param name="result">Parsed Vector3 value</param>
        /// <returns>TRUE if parsing succeed</returns>
        public static bool TryParseVector3(string value, out Vector3 result)
        {
            result = Vector3.zero;
            
            if (string.IsNullOrWhiteSpace(value))
                return false;
            
            // Remove parentheses and split by comma
            var cleaned = value.Trim().Trim('(', ')');
            var parts = cleaned.Split(',');
            
            try
            {
                result.x = parts.Length > 0 ? float.Parse(parts[0].Trim()) : 0f;
                result.y = parts.Length > 1 ? float.Parse(parts[1].Trim()) : 0f;
                result.z = parts.Length > 2 ? float.Parse(parts[2].Trim()) : 0f;

                return true;
            }
            catch
            {
                Debug.LogError($"[GoogleSheetParser] Failed to parse Vector3: '{value}'");
                return false;
            }
        }
    }
}