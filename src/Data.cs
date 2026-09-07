using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace DesktopTodo
{
    public class TodoItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class Settings
    {
        public bool Topmost { get; set; }
        public bool DesktopEmbed { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public bool HasPos { get; set; }
    }

    public class AppDataFile
    {
        public List<TodoItem> Items { get; set; }
        public Settings Settings { get; set; }
    }

    public static class Store
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopTodo");
        private static readonly string FilePath = Path.Combine(Dir, "todos.json");
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static AppDataFile Data;

        public static void Load()
        {
            Data = new AppDataFile();
            Data.Items = new List<TodoItem>();
            Settings s = new Settings();
            s.Topmost = true;
            Data.Settings = s;

            try
            {
                if (File.Exists(FilePath))
                {
                    AppDataFile loaded = Json.Deserialize<AppDataFile>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        if (loaded.Items != null)
                        {
                            List<TodoItem> clean = new List<TodoItem>();
                            foreach (TodoItem it in loaded.Items)
                            {
                                if (it != null && !string.IsNullOrEmpty(it.Text)) clean.Add(it);
                            }
                            Data.Items = clean;
                        }
                        if (loaded.Settings != null) Data.Settings = loaded.Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Store.Load failed: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, Json.Serialize(Data));
            }
            catch (Exception ex)
            {
                Logger.Log("Store.Save failed: " + ex.Message);
            }
        }

        // Active items: with due date first (earliest due on top, same date newest first),
        // then items without due date keep default order (newest on top) at the bottom.
        public static List<TodoItem> GetActiveSorted()
        {
            List<TodoItem> list = Data.Items.FindAll(delegate(TodoItem i) { return !i.Completed; });
            list.Sort(CompareActive);
            return list;
        }

        private static int CompareActive(TodoItem a, TodoItem b)
        {
            bool aHas = a.DueDate.HasValue;
            bool bHas = b.DueDate.HasValue;
            if (aHas && bHas)
            {
                int c = DateTime.Compare(a.DueDate.Value.Date, b.DueDate.Value.Date);
                if (c != 0) return c;
                return DateTime.Compare(b.CreatedAt, a.CreatedAt);
            }
            if (aHas) return -1;
            if (bHas) return 1;
            return DateTime.Compare(b.CreatedAt, a.CreatedAt);
        }

        // Completed items: most recently struck-off first.
        public static List<TodoItem> GetDoneSorted()
        {
            List<TodoItem> list = Data.Items.FindAll(delegate(TodoItem i) { return i.Completed; });
            list.Sort(delegate(TodoItem a, TodoItem b)
            {
                DateTime av = a.CompletedAt.HasValue ? a.CompletedAt.Value : DateTime.MinValue;
                DateTime bv = b.CompletedAt.HasValue ? b.CompletedAt.Value : DateTime.MinValue;
                return DateTime.Compare(bv, av);
            });
            return list;
        }
    }

    public static class Logger
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopTodo", "error.log");

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(FilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
