using System;
using System.IO;
using Newtonsoft.Json;

namespace FormulaGame
{
    // PC/Editor file boundary. Nintendo keeps its existing committed storage API.
    public static class FormulaSaveStorage
    {
        public static void Write(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(value));
            if (File.Exists(path))
            {
                // After recovery, do not replace the valid backup with the corrupt primary.
                bool primaryValid;
                try { primaryValid = JsonConvert.DeserializeObject(File.ReadAllText(path), value.GetType()) != null; }
                catch (JsonException) { primaryValid = false; }
                File.Replace(temporary, path, primaryValid ? path + ".bak" : null);
            }
            else File.Move(temporary, path);
        }
        public static object Read(string path, Type type)
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            Exception last = null;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    object value = JsonConvert.DeserializeObject(File.ReadAllText(candidate), type);
                    if (value != null) return value;
                    last = new InvalidDataException("Save is empty.");
                }
                catch (Exception e) { last = e; }
            }
            throw new InvalidDataException("Save and backup could not be recovered.", last);
        }
    }
}
