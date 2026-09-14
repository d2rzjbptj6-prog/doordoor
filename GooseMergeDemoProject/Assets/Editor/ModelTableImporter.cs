using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using UnityEditor;
using UnityEngine;

public static class ModelTableImporter
{
    private const string OutputPath = "Assets/Resources/Config/model_config.json";

    [Serializable]
    private sealed class ModelConfigData
    {
        public ActorConfigData[] actors;
        public BuffConfigData[] buffs;
        public MonsterConfigData[] monsters;
    }

    [Serializable]
    private sealed class ActorConfigData
    {
        public int id;
        public string name;
        public int buffId;
        public int modelId;
        public float breakValue;
        public StatConfigData[] stats;
    }

    [Serializable]
    private sealed class StatConfigData
    {
        public string key;
        public float value;
    }

    [Serializable]
    private sealed class BuffConfigData
    {
        public int id;
        public string name;
        public int effectId;
        public int priority;
        public int priorityGroup;
        public int frontBuffId;
        public float bulletDistance;
        public float bulletSpeed;
        public float breakValue;
        public float battleValue;
    }

    [Serializable]
    private sealed class MonsterConfigData
    {
        public int id;
        public int stageId;
        public int actorId;
        public string note;
        public int doorGroup;
        public int count;
        public float x;
        public float y;
        public string range;
        public string blood;
    }

    private sealed class SheetData
    {
        public readonly List<Dictionary<int, string>> Rows = new List<Dictionary<int, string>>();
    }

    [MenuItem("Tools/Demo/Import Model Table")]
    public static void ImportDefaultModelTableMenu()
    {
        string xlsxPath = FindDefaultModelPath();
        if (!File.Exists(xlsxPath))
        {
            xlsxPath = EditorUtility.OpenFilePanel("Select model.xlsx", Application.dataPath, "xlsx");
        }
        if (string.IsNullOrEmpty(xlsxPath))
        {
            return;
        }
        ImportModelTable(xlsxPath, true);
    }

    public static void ImportDefaultModelTableBatch()
    {
        ImportDefaultModelTable(false);
    }

    public static bool ImportDefaultModelTable(bool showDialog)
    {
        string xlsxPath = FindDefaultModelPath();
        if (!File.Exists(xlsxPath))
        {
            Debug.LogWarning("model.xlsx not found: " + xlsxPath);
            return false;
        }
        return ImportModelTable(xlsxPath, showDialog);
    }

    private static bool ImportModelTable(string xlsxPath, bool showDialog)
    {
        try
        {
            Dictionary<string, SheetData> workbook = ReadWorkbook(xlsxPath);
            ModelConfigData config = new ModelConfigData
            {
                actors = ParseActors(GetSheet(workbook, "actor")).ToArray(),
                buffs = ParseBuffs(GetSheet(workbook, "buff")).ToArray(),
                monsters = ParseMonsters(GetSheet(workbook, "monster")).ToArray(),
            };
            ValidateConfig(config);

            string outputFullPath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath));
            File.WriteAllText(outputFullPath, JsonUtility.ToJson(config, true));
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            string message = $"Imported model table: {config.actors.Length} actors, {config.buffs.Length} buffs, {config.monsters.Length} monsters";
            Debug.Log(message);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Model table imported", message, "OK");
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Model table import failed: " + ex);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Model table import failed", ex.Message, "OK");
            }
            return false;
        }
    }

    private static string FindDefaultModelPath()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectPath, "..", "..", "model.xlsx"));
    }

    private static SheetData GetSheet(Dictionary<string, SheetData> workbook, string name)
    {
        SheetData sheet;
        if (!workbook.TryGetValue(name, out sheet))
        {
            throw new InvalidDataException("Missing sheet: " + name);
        }
        return sheet;
    }

    private static List<ActorConfigData> ParseActors(SheetData sheet)
    {
        List<ActorConfigData> actors = new List<ActorConfigData>();
        for (int i = 4; i < sheet.Rows.Count; i++)
        {
            Dictionary<int, string> row = sheet.Rows[i];
            int id = ReadInt(row, 2, 0);
            if (id <= 0)
            {
                continue;
            }

            ActorConfigData actor = new ActorConfigData
            {
                id = id,
                name = ReadString(row, 3),
                buffId = ReadInt(row, 4, 0),
                modelId = ReadInt(row, 5, 0),
                breakValue = ReadFloat(row, 6, 0f),
                stats = ReadStats(row, 7).ToArray(),
            };
            actors.Add(actor);
        }
        return actors;
    }

    private static List<BuffConfigData> ParseBuffs(SheetData sheet)
    {
        List<BuffConfigData> buffs = new List<BuffConfigData>();
        for (int i = 4; i < sheet.Rows.Count; i++)
        {
            Dictionary<int, string> row = sheet.Rows[i];
            int id = ReadInt(row, 2, 0);
            if (id <= 0)
            {
                continue;
            }

            BuffConfigData buff = new BuffConfigData
            {
                id = id,
                name = ReadString(row, 3),
                effectId = ReadInt(row, 4, 0),
                priority = ReadInt(row, 5, 0),
                priorityGroup = ReadInt(row, 6, 0),
                frontBuffId = ReadInt(row, 7, 0),
                bulletDistance = ReadFloat(row, 8, 0f),
                bulletSpeed = ReadFloat(row, 9, 0f),
                breakValue = ReadFloat(row, 10, 0f),
                battleValue = ReadFloat(row, 11, 0f),
            };
            buffs.Add(buff);
        }
        return buffs;
    }

    private static List<MonsterConfigData> ParseMonsters(SheetData sheet)
    {
        List<MonsterConfigData> monsters = new List<MonsterConfigData>();
        int fallbackId = 1;
        for (int i = 4; i < sheet.Rows.Count; i++)
        {
            Dictionary<int, string> row = sheet.Rows[i];
            int actorId = ReadInt(row, 4, 0);
            if (actorId <= 0)
            {
                continue;
            }

            int id = ReadInt(row, 2, 0);
            MonsterConfigData monster = new MonsterConfigData
            {
                id = id > 0 ? id : fallbackId,
                stageId = ReadInt(row, 3, 0),
                actorId = actorId,
                note = ReadString(row, 5),
                doorGroup = ReadInt(row, 6, 0),
                count = Mathf.Max(1, ReadInt(row, 7, 1)),
                x = ReadFloat(row, 8, 0f),
                y = ReadFloat(row, 9, 0f),
                range = ReadString(row, 10),
                blood = ReadString(row, 11),
            };
            monsters.Add(monster);
            fallbackId++;
        }
        return monsters;
    }

    private static void ValidateConfig(ModelConfigData config)
    {
        HashSet<int> actorIds = new HashSet<int>();
        HashSet<int> buffIds = new HashSet<int>();
        for (int i = 0; i < config.actors.Length; i++)
        {
            actorIds.Add(config.actors[i].id);
        }
        for (int i = 0; i < config.buffs.Length; i++)
        {
            buffIds.Add(config.buffs[i].id);
        }

        for (int i = 0; i < config.buffs.Length; i++)
        {
            BuffConfigData buff = config.buffs[i];
            if (buff.frontBuffId > 0 && !buffIds.Contains(buff.frontBuffId))
            {
                Debug.LogWarning($"Buff {buff.id} references missing front buff {buff.frontBuffId}");
            }
        }

        for (int i = 0; i < config.monsters.Length; i++)
        {
            MonsterConfigData monster = config.monsters[i];
            if (!actorIds.Contains(monster.actorId))
            {
                Debug.LogWarning($"Monster {monster.id} references missing actor {monster.actorId}");
            }
        }
    }

    private static List<StatConfigData> ReadStats(Dictionary<int, string> row, int startColumn)
    {
        List<StatConfigData> stats = new List<StatConfigData>();
        for (int column = startColumn; column < startColumn + 32; column += 2)
        {
            string key = ReadString(row, column);
            if (string.IsNullOrEmpty(key) || key.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            stats.Add(new StatConfigData
            {
                key = key,
                value = ReadFloat(row, column + 1, 0f),
            });
        }
        return stats;
    }

    private static Dictionary<string, SheetData> ReadWorkbook(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            List<string> sharedStrings = ReadSharedStrings(archive);
            Dictionary<string, string> relationshipTargets = ReadWorkbookRelationships(archive);
            Dictionary<string, string> sheetTargets = ReadWorkbookSheets(archive, relationshipTargets);
            Dictionary<string, SheetData> sheets = new Dictionary<string, SheetData>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> sheet in sheetTargets)
            {
                ZipArchiveEntry entry = archive.GetEntry(sheet.Value);
                if (entry == null)
                {
                    continue;
                }
                sheets[sheet.Key] = ReadSheet(entry, sharedStrings);
            }

            return sheets;
        }
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        List<string> values = new List<string>();
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return values;
        }

        XmlDocument document = LoadXml(entry);
        XmlNamespaceManager namespaces = CreateNamespaceManager(document);
        foreach (XmlNode node in document.SelectNodes("//x:si", namespaces))
        {
            values.Add(node.InnerText);
        }
        return values;
    }

    private static Dictionary<string, string> ReadWorkbookRelationships(ZipArchive archive)
    {
        Dictionary<string, string> relationships = new Dictionary<string, string>();
        ZipArchiveEntry entry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (entry == null)
        {
            return relationships;
        }

        XmlDocument document = LoadXml(entry);
        foreach (XmlNode node in document.DocumentElement.ChildNodes)
        {
            XmlAttribute id = node.Attributes["Id"];
            XmlAttribute target = node.Attributes["Target"];
            if (id != null && target != null)
            {
                relationships[id.Value] = "xl/" + target.Value.TrimStart('/');
            }
        }
        return relationships;
    }

    private static Dictionary<string, string> ReadWorkbookSheets(ZipArchive archive, Dictionary<string, string> relationshipTargets)
    {
        Dictionary<string, string> sheets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XmlDocument document = LoadXml(archive.GetEntry("xl/workbook.xml"));
        XmlNamespaceManager namespaces = CreateNamespaceManager(document);
        XmlNamespaceManager relationshipNamespaces = new XmlNamespaceManager(document.NameTable);
        relationshipNamespaces.AddNamespace("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        foreach (XmlNode node in document.SelectNodes("//x:sheets/x:sheet", namespaces))
        {
            XmlAttribute name = node.Attributes["name"];
            XmlAttribute relationshipId = node.Attributes["id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"];
            if (name == null || relationshipId == null)
            {
                continue;
            }
            string target;
            if (relationshipTargets.TryGetValue(relationshipId.Value, out target))
            {
                sheets[name.Value] = target;
            }
        }
        return sheets;
    }

    private static SheetData ReadSheet(ZipArchiveEntry entry, List<string> sharedStrings)
    {
        SheetData sheet = new SheetData();
        XmlDocument document = LoadXml(entry);
        XmlNamespaceManager namespaces = CreateNamespaceManager(document);

        foreach (XmlNode rowNode in document.SelectNodes("//x:sheetData/x:row", namespaces))
        {
            Dictionary<int, string> row = new Dictionary<int, string>();
            foreach (XmlNode cellNode in rowNode.SelectNodes("x:c", namespaces))
            {
                XmlAttribute reference = cellNode.Attributes["r"];
                int column = reference != null ? ColumnIndexFromCellReference(reference.Value) : row.Count + 1;
                row[column] = ReadCellValue(cellNode, namespaces, sharedStrings);
            }
            sheet.Rows.Add(row);
        }

        return sheet;
    }

    private static string ReadCellValue(XmlNode cellNode, XmlNamespaceManager namespaces, List<string> sharedStrings)
    {
        XmlAttribute type = cellNode.Attributes["t"];
        string typeValue = type != null ? type.Value : string.Empty;
        if (typeValue == "inlineStr")
        {
            XmlNode inline = cellNode.SelectSingleNode("x:is", namespaces);
            return inline != null ? inline.InnerText : string.Empty;
        }

        XmlNode valueNode = cellNode.SelectSingleNode("x:v", namespaces);
        if (valueNode == null)
        {
            return string.Empty;
        }

        if (typeValue == "s")
        {
            int index;
            if (int.TryParse(valueNode.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) && index >= 0 && index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }
            return string.Empty;
        }

        return valueNode.InnerText;
    }

    private static XmlDocument LoadXml(ZipArchiveEntry entry)
    {
        if (entry == null)
        {
            throw new InvalidDataException("Missing xml entry");
        }
        XmlDocument document = new XmlDocument();
        using (Stream stream = entry.Open())
        {
            document.Load(stream);
        }
        return document;
    }

    private static XmlNamespaceManager CreateNamespaceManager(XmlDocument document)
    {
        XmlNamespaceManager namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        return namespaces;
    }

    private static int ColumnIndexFromCellReference(string reference)
    {
        int column = 0;
        for (int i = 0; i < reference.Length; i++)
        {
            char c = reference[i];
            if (c < 'A' || c > 'Z')
            {
                break;
            }
            column = column * 26 + (c - 'A' + 1);
        }
        return Mathf.Max(1, column);
    }

    private static string ReadString(Dictionary<int, string> row, int column)
    {
        string value;
        return row.TryGetValue(column, out value) ? value : string.Empty;
    }

    private static int ReadInt(Dictionary<int, string> row, int column, int fallback)
    {
        float value = ReadFloat(row, column, fallback);
        return Mathf.RoundToInt(value);
    }

    private static float ReadFloat(Dictionary<int, string> row, int column, float fallback)
    {
        string text = ReadString(row, column);
        if (string.IsNullOrEmpty(text))
        {
            return fallback;
        }

        float value;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }
}
