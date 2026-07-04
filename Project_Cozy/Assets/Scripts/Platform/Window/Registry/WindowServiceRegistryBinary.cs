using System;
using System.IO;

public static class WindowServiceRegistryBinary
{
    private const int Version = 2;

    public static byte[] Serialize(WindowServiceRegistryData data)
    {
        if (data == null) data = new WindowServiceRegistryData();

        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(Version);

            WriteStringList(writer, data.GlobalEnabledBindingKeys);

            writer.Write(data.SceneEntries != null ? data.SceneEntries.Count : 0);
            if (data.SceneEntries != null)
            {
                for (int i = 0; i < data.SceneEntries.Count; i++)
                {
                    var entry = data.SceneEntries[i] ?? new WindowSceneServiceEntry();
                    writer.Write(entry.ScenePath ?? string.Empty);
                    WriteStringList(writer, entry.EnabledBindingKeys);
                }
            }

            writer.Write(data.EnvironmentEntries != null ? data.EnvironmentEntries.Count : 0);
            if (data.EnvironmentEntries != null)
            {
                for (int i = 0; i < data.EnvironmentEntries.Count; i++)
                {
                    var entry = data.EnvironmentEntries[i] ?? new WindowEnvironmentServiceEntry();
                    writer.Write(entry.EnvironmentKey ?? string.Empty);
                    WriteStringList(writer, entry.EnabledBindingKeys);
                }
            }

            writer.Flush();
            return ms.ToArray();
        }
    }

    public static WindowServiceRegistryData Deserialize(byte[] bytes)
    {
        var data = new WindowServiceRegistryData();
        if (bytes == null || bytes.Length == 0)
            return data;

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            int version = reader.ReadInt32();
            if (version == 1)
            {
                data.GlobalMonoBehaviourTypeNames = ReadStringList(reader);
                data.GlobalPlainTypeNames = ReadStringList(reader);

                int sceneCountV1 = reader.ReadInt32();
                for (int i = 0; i < sceneCountV1; i++)
                {
                    var legacyEntry = new WindowSceneServiceEntry
                    {
                        ScenePath = reader.ReadString(),
                        MonoBehaviourTypeNames = ReadStringList(reader),
                        PlainTypeNames = ReadStringList(reader),
                    };
                    data.SceneEntries.Add(legacyEntry);
                }

                return data;
            }

            if (version != Version)
                throw new InvalidOperationException($"Unsupported WindowServiceRegistry binary version: {version}");

            data.GlobalEnabledBindingKeys = ReadStringList(reader);

            int sceneCount = reader.ReadInt32();
            for (int i = 0; i < sceneCount; i++)
            {
                var entry = new WindowSceneServiceEntry
                {
                    ScenePath = reader.ReadString(),
                    EnabledBindingKeys = ReadStringList(reader),
                };
                data.SceneEntries.Add(entry);
            }

            int environmentCount = reader.ReadInt32();
            for (int i = 0; i < environmentCount; i++)
            {
                var entry = new WindowEnvironmentServiceEntry
                {
                    EnvironmentKey = reader.ReadString(),
                    EnabledBindingKeys = ReadStringList(reader),
                };
                data.EnvironmentEntries.Add(entry);
            }
        }

        return data;
    }

    private static void WriteStringList(BinaryWriter writer, System.Collections.Generic.List<string> list)
    {
        writer.Write(list != null ? list.Count : 0);
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
            writer.Write(list[i] ?? string.Empty);
    }

    private static System.Collections.Generic.List<string> ReadStringList(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var list = new System.Collections.Generic.List<string>(count);
        for (int i = 0; i < count; i++)
            list.Add(reader.ReadString());

        return list;
    }
}
