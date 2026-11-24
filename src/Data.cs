using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace DesktopTodo
{
    public class SubTask
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Done { get; set; }
    }

    public class TodoGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class TodoItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<SubTask> Subs { get; set; }

        // Group this item belongs to. Older data files have no GroupId at all,
        // and null/empty both mean "未分组".
        public string GroupId { get; set; }

        // Older data files have no Subs field at all, so reads must never assume non-null.
        public List<SubTask> SubsList()
        {
            if (Subs == null) Subs = new List<SubTask>();
            return Subs;
        }

        public int SubDoneCount()
        {
            int n = 0;
            if (Subs != null)
            {
                foreach (SubTask s in Subs) if (s != null && s.Done) n++;
            }
            return n;
        }

        // Drops null/empty subentries, collapses an empty list back to null (keeps the JSON clean),
        // and returns the clean list.
        public List<SubTask> CleanSubs()
        {
            List<SubTask> clean = new List<SubTask>();
            if (Subs != null)
            {
                foreach (SubTask s in Subs)
                {
                    if (s != null && !string.IsNullOrEmpty(s.Text)) clean.Add(s);
                }
            }
            Subs = clean.Count > 0 ? clean : null;
            return clean;
        }
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
        public List<TodoGroup> Groups { get; set; }
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
            Data.Groups = new List<TodoGroup>();
            Settings s = new Settings();
            s.Topmost = true;
            Data.Settings = s;

            try
            {
                if (File.Exists(FilePath))
                {
                    string raw = File.ReadAllText(FilePath);
                    // JavaScriptSerializer only recognises the escaped form \/Date(ms)\/ inside
                    // string values; a file rewritten by an external tool may carry plain
                    // /Date(ms)/, which makes Deserialize throw and the whole list read as empty.
                    // Normalising is value-preserving ("\/" decodes back to "/"), so it is safe.
                    raw = System.Text.RegularExpressions.Regex.Replace(raw, @"(?<!\\)/Date\(", @"\\/Date(");
                    AppDataFile loaded = Json.Deserialize<AppDataFile>(raw);
                    if (loaded != null)
                    {
                        if (loaded.Items != null)
                        {
                            List<TodoItem> clean = new List<TodoItem>();
                            foreach (TodoItem it in loaded.Items)
                            {
                                if (it != null && !string.IsNullOrEmpty(it.Text))
                                {
                                    it.CleanSubs();
                                    clean.Add(it);
                                }
                            }
                            Data.Items = clean;
                        }
                        LoadGroups(loaded.Groups);
                        if (loaded.Settings != null) Data.Settings = loaded.Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Store.Load failed: " + ex.Message);
                BackupBrokenFile();
            }
        }

        // A parse failure must never end with the user's list silently replaced by an
        // empty one on the next save: keep a copy of whatever we could not read.
        private static void BackupBrokenFile()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                string dst = Path.Combine(Dir, "todos.broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                File.Copy(FilePath, dst, true);
                Logger.Log("Store.Load: unreadable data copied to " + dst);
            }
            catch (Exception ex)
            {
                Logger.Log("BackupBrokenFile failed: " + ex.Message);
            }
        }

        // Keeps only well-formed groups (non-empty name, unique id), and drops any item
        // reference to a group that no longer exists so the UI can trust GroupId.
        private static void LoadGroups(List<TodoGroup> raw)
        {
            List<TodoGroup> clean = new List<TodoGroup>();
            if (raw != null)
            {
                foreach (TodoGroup g in raw)
                {
                    if (g == null || string.IsNullOrEmpty(g.Name)) continue;
                    g.Name = g.Name.Trim();
                    if (g.Name.Length == 0) continue;
                    if (string.IsNullOrEmpty(g.Id)) g.Id = Guid.NewGuid().ToString("N");
                    if (FindGroup(clean, g.Id) != null) continue;
                    clean.Add(g);
                }
            }
            Data.Groups = clean;

            foreach (TodoItem it in Data.Items)
            {
                if (!string.IsNullOrEmpty(it.GroupId) && FindGroup(clean, it.GroupId) == null)
                    it.GroupId = null;
            }
        }

        private static TodoGroup FindGroup(List<TodoGroup> list, string id)
        {
            if (list == null || id == null) return null;
            foreach (TodoGroup g in list)
            {
                if (g != null && g.Id == id) return g;
            }
            return null;
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

        // Same ordering as GetActiveSorted, exposed for per-group sublists.
        public static void SortActive(List<TodoItem> list)
        {
            list.Sort(CompareActive);
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

        // Permanently removes a single item by id (e.g. one added by mistake).
        public static bool DeleteItem(string id)
        {
            TodoItem it = Data.Items.Find(delegate(TodoItem i) { return i.Id == id; });
            if (it == null) return false;
            Data.Items.Remove(it);
            return true;
        }

        // ---------- groups ----------

        // Never null; older data files simply start with an empty group list.
        public static List<TodoGroup> Groups()
        {
            if (Data.Groups == null) Data.Groups = new List<TodoGroup>();
            return Data.Groups;
        }

        public static TodoGroup GetGroup(string id)
        {
            return FindGroup(Groups(), id);
        }

        // Display name for an item's group; ungrouped items read as "未分组".
        public static string GroupName(string id)
        {
            TodoGroup g = GetGroup(id);
            return g == null ? "未分组" : g.Name;
        }

        // Returns the existing group when the name is already taken (ignoring case),
        // so typing the same name twice never creates duplicates.
        public static TodoGroup AddGroup(string name)
        {
            if (name == null) return null;
            string t = name.Trim();
            if (t.Length == 0) return null;
            foreach (TodoGroup g in Groups())
            {
                if (string.Equals(g.Name, t, StringComparison.CurrentCultureIgnoreCase)) return g;
            }
            TodoGroup ng = new TodoGroup();
            ng.Id = Guid.NewGuid().ToString("N");
            ng.Name = t;
            Groups().Add(ng);
            return ng;
        }

        public static bool RenameGroup(string id, string name)
        {
            TodoGroup g = GetGroup(id);
            if (g == null || name == null) return false;
            string t = name.Trim();
            if (t.Length == 0) return false;
            foreach (TodoGroup other in Groups())
            {
                if (other != g && string.Equals(other.Name, t, StringComparison.CurrentCultureIgnoreCase))
                    return false;
            }
            g.Name = t;
            return true;
        }

        // Removing a group never removes todos: its items fall back to 未分组.
        public static void DeleteGroup(string id)
        {
            TodoGroup g = GetGroup(id);
            if (g == null) return;
            foreach (TodoItem it in Data.Items)
            {
                if (it.GroupId == id) it.GroupId = null;
            }
            Groups().Remove(g);
        }

        public static void MoveItem(string itemId, string groupId)
        {
            TodoItem it = Data.Items.Find(delegate(TodoItem i) { return i.Id == itemId; });
            if (it == null) return;
            it.GroupId = string.IsNullOrEmpty(groupId) ? null : groupId;
        }

        // groupId == null / "" counts the ungrouped bucket.
        public static bool InGroup(TodoItem it, string groupId)
        {
            string gid = string.IsNullOrEmpty(it.GroupId) ? null : it.GroupId;
            string want = string.IsNullOrEmpty(groupId) ? null : groupId;
            return gid == want;
        }

        public static int CountInGroup(List<TodoItem> items, string groupId)
        {
            int n = 0;
            foreach (TodoItem it in items) if (InGroup(it, groupId)) n++;
            return n;
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
