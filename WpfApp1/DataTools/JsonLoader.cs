using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PixArtConverter.DataTools
{
    public static class JsonLoader
    {
        //private static readonly string _resourcePath  = "PixArtConverter.Resources.DefaultTiles.json";
        private static string[]? _cachedData;

        public static async Task<string[]> LoadDataAsync(string _resourcePath)
        {
            if (_cachedData != null) return _cachedData;

            Stream? stream = null;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                stream = assembly.GetManifestResourceStream(_resourcePath);

                if (stream == null)
                    throw new FileNotFoundException("Ресурс не найден", _resourcePath);

                var options = new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var data = await JsonSerializer.DeserializeAsync<string[]>(stream, options);

                return _cachedData = data switch
                {
                    null => throw new JsonException("Неверный формат: ожидался массив строк"),
                    { Length: 0 } => throw new InvalidOperationException("Массив не должен быть пустым"),
                    _ => data
                };
            }
            catch (FileNotFoundException ex)
            {
                LogError($"Файл не найден: {ex.FileName}");
                throw new CriticalLoadingException("Ошибка конфигурации", ex);
            }
            catch (JsonException ex)
            {
                LogError($"Ошибка формата JSON: {ex.Message}");
                throw new InvalidDataException("Некорректные данные в файле", ex);
            }
            catch (IOException ex)
            {
                LogError($"Ошибка ввода-вывода: {ex.Message}");
                throw new LoadingRetryableException("Ошибка чтения файла", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Нет прав доступа: {ex.Message}");
                throw new SecurityException("Доступ запрещен", ex);
            }
            finally
            {
                stream?.Dispose();

                // Для больших файлов сразу освобождаем память
                if (_cachedData == null)
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            }
        }

        private static void LogError(string message)
        {
            // Реализация логирования (NLog, Serilog, и т.д.)
            Console.WriteLine($"[ERROR] {DateTime.UtcNow:O} {message}");

            // Для UI-приложений:
            // Application.Current.Dispatcher.Invoke(() => 
            //     ShowErrorPopup(message));
        }
    }

    // Кастомные исключения для разных сценариев
    public class CriticalLoadingException : Exception
    {
        public CriticalLoadingException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class LoadingRetryableException : Exception
    {
        public LoadingRetryableException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class SecurityException : Exception
    {
        public SecurityException(string message, Exception inner)
            : base(message, inner) { }
    }
}

