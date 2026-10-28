using ConcertHype.Model;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ConcertHype
{
    /// <summary>
    /// Reads concert_hype.json from the mod's folder. Each key is a <see cref="Config"/> field in camelCase. A missing
    /// file, an unknown key or a value that can't be read keeps the default, with a warning.
    /// </summary>
    public static class ConfigFile
    {
        public const string FileName = "concert_hype.json";

        public static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

        private static IEnumerable<FieldInfo> Fields() =>
            typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Instance);

        public static Config Load(string path, Action<string> warn)
        {
            Config config = new Config();
            if (!File.Exists(path))
            {
                warn($"{FileName} not found; using the default numbers");
                return config;
            }
            return Parse(File.ReadAllText(path), warn);
        }

        public static Config Parse(string json, Action<string> warn)
        {
            Config config = new Config();
            JSONClass root;
            try
            {
                root = JSON.Parse(json) as JSONClass;
            }
            catch (Exception ex)
            {
                warn($"{FileName} couldn't be read ({ex.Message}); using the default numbers");
                return config;
            }
            if (root == null)
            {
                warn($"{FileName} isn't a JSON object; using the default numbers");
                return config;
            }

            Dictionary<string, FieldInfo> fields = Fields().ToDictionary(f => CamelCase(f.Name));
            foreach (KeyValuePair<string, JSONNode> item in root)
            {
                if (!fields.TryGetValue(item.Key, out FieldInfo field))
                    warn($"{FileName}: unknown setting '{item.Key}' ignored");
                else if (!TrySet(config, field, item.Value))
                    warn($"{FileName}: '{item.Key}' can't be read; keeping {Format(field.GetValue(config))}");
            }
            return config;
        }

        private static bool TrySet(Config config, FieldInfo field, JSONNode node)
        {
            if (field.FieldType == typeof(double))
            {
                if (!TryNumber(node, out double value))
                    return false;
                field.SetValue(config, value);
                return true;
            }
            if (field.FieldType == typeof(int))
            {
                if (!TryNumber(node, out double value) || value != Math.Floor(value) || Math.Abs(value) > int.MaxValue)
                    return false;
                field.SetValue(config, (int)value);
                return true;
            }
            if (field.FieldType == typeof(double[]))
            {
                if (!(node is JSONArray array) || array.Count == 0)
                    return false;
                double[] values = new double[array.Count];
                for (int i = 0; i < array.Count; i++)
                {
                    if (!TryNumber(array[i], out values[i]))
                        return false;
                }
                field.SetValue(config, values);
                return true;
            }
            return false;
        }

        private static bool TryNumber(JSONNode node, out double value)
        {
            value = 0;
            return !(node is null) && !(node is JSONClass) && !(node is JSONArray)
                && double.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(object value) =>
            value is double[] list ? "[" + string.Join(", ", list.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]"
            : value is double d ? d.ToString("R", CultureInfo.InvariantCulture)
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        /// <summary>
        /// The defaults as the file's text.
        /// </summary>
        public static string Defaults()
        {
            Config config = new Config();
            StringBuilder text = new StringBuilder("{\n");
            List<FieldInfo> fields = Fields().ToList();
            for (int i = 0; i < fields.Count; i++)
                text.Append($"\t\"{CamelCase(fields[i].Name)}\": {Format(fields[i].GetValue(config))}{(i < fields.Count - 1 ? "," : "")}\n");
            return text.Append("}\n").ToString();
        }
    }
}
