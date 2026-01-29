using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace AppAmbit;

internal static class MultipartFormDataContentExtensions
{
    private const string _dateTimeFormatISO8601ForFile = "yyyy-MM-ddTHH_mm_ss_fffZ";
    private const string _dateFormatStringApi = "yyyy-MM-dd HH:mm:ss";

    public static void AddObjectToMultipartFormDataContent(this MultipartFormDataContent formData, object obj, string prefix = "", bool useSquareBrakets = false)
    {
        try 
        {
            if (obj is null)
                return;

            if (obj is IDictionary dict)
            {
                formData.AddDictionaryToMultipartFormDataContent(dict, prefix);
                return;
            }

            if (obj.IsList())
            {
                var list = obj.ToObjectList();
                formData.AddListToMultipartFormDataContent(list, prefix);
                return;
            }

            if (obj is DateTime dateTime)
            {
                formData.Add(new StringContent(dateTime.ToString(_dateFormatStringApi)), prefix);
                return;
            }

            if (IsSimpleType(obj))
            {
                formData.Add(new StringContent(obj.ToString() ?? ""), prefix);
                return;
            }

            var objectProperties = obj.GetType().GetProperties();
            foreach (var property in objectProperties)
            {
                var propName = property.Name;
                var propValue = property.GetValue(obj);

                if (propValue == null)
                    continue;

                var jsonIgnoreAttr = property.GetCustomAttribute<JsonIgnoreAttribute>();
                if (jsonIgnoreAttr != null)
                    continue;

                var jsonPropAttr = property.GetCustomAttribute<JsonPropertyAttribute>();
                propName = jsonPropAttr?.PropertyName ?? property.Name;

                string multipartKey = useSquareBrakets ? $"{prefix}[{propName}]" : $"{prefix}{propName}";
                var type = property.PropertyType;
                var isNullable = Nullable.GetUnderlyingType(type) != null;
                var actualType = isNullable ? Nullable.GetUnderlyingType(type) : type;

                if (actualType != null && actualType.IsEnum && propValue != null)
                {
                    var enumVal = Enum.Parse(actualType, propValue.ToString());
                    var enumMember = actualType.GetMember(enumVal.ToString()).FirstOrDefault();
                    var enumAttr = enumMember?.GetCustomAttribute<EnumMemberAttribute>();
                    var enumStr = enumAttr?.Value ?? enumVal.ToString();
                    formData.Add(new StringContent(enumStr), multipartKey);
                    continue;
                }

                var multipartFormDataFileAttribute = property.GetCustomAttribute<MultipartFormDataFileAttribute>();
                if (multipartFormDataFileAttribute != null)
                {
                    var fileName = $"log-{DateTime.UtcNow.ToString(_dateTimeFormatISO8601ForFile)}.txt";
                    var filePath = Path.Combine(AppPaths.AppDataDir, fileName);

                    Debug.WriteLine($"Debug FilePath: {filePath}");
                    var encodedBytes = Encoding.ASCII.GetBytes(propValue as string ?? "");
                    var fileContent = new ByteArrayContent(encodedBytes);

                    string formNameFile = useSquareBrakets ? "[file]" : "file";
                    formData.Add(fileContent, prefix + formNameFile, Path.GetFileName(filePath));
                    continue;
                }

                string newPrefix = useSquareBrakets ? $"{prefix}[{propName}]" : $"{prefix}{propName}";
                formData.AddObjectToMultipartFormDataContent(propValue, newPrefix, true);
            }
        }catch (Exception ex)
        {
            Debug.WriteLine($"Error in AddObjectToMultipartFormDataContent: {ex}");
        }
    }

    public static bool IsSimpleType(object obj)
    {
        if (obj == null) return false;

        var type = obj.GetType();
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(Guid);
    }

    public static void AddDictionaryToMultipartFormDataContent(this MultipartFormDataContent formData, IDictionary dict, string prefix = "")
    {
        if (dict == null)
            return;

        foreach (DictionaryEntry kvp in dict)
        {
            var key = kvp.Key?.ToString() ?? string.Empty;
            var value = kvp.Value ?? string.Empty;
            var newPrefix = $"{prefix}[{key}]";
            formData.AddObjectToMultipartFormDataContent(value, newPrefix, true);
        }
    }

    public static void AddListToMultipartFormDataContent(this MultipartFormDataContent formData, List<object> list, string prefix = "")
    {
        if (list is not List<object>)
            return;

        Dictionary<string, object> dict = list
            .Select((item, index) => new KeyValuePair<string, object>(index.ToString(), item))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        formData.AddDictionaryToMultipartFormDataContent(dict, prefix);
    }

    [Conditional("DEBUG")]
    public static async void DebugMultipartFormDataContent(this MultipartFormDataContent formData)
    {
        foreach (var content in formData)
        {
            Debug.WriteLine("Headers:");
            foreach (var header in content.Headers)
            {
                if (header.Key == "file")
                {
                    Debug.WriteLine($"{header.Key}: FILE");
                    continue;
                }

                Debug.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            var body = await content.ReadAsStringAsync();
            Debug.WriteLine("Body:");
            Debug.WriteLine("----------------------------");
            Debug.WriteLine(body);
            Debug.WriteLine("----------------------------");
        }
    }
}
