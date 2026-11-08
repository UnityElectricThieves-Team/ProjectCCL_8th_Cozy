using System;
using System.Collections.Generic;

[Serializable]
public sealed class WindowServiceRegistryData
{
    public List<string> GlobalEnabledBindingKeys = new List<string>();
    public List<WindowSceneServiceEntry> SceneEntries = new List<WindowSceneServiceEntry>();
    public List<WindowEnvironmentServiceEntry> EnvironmentEntries = new List<WindowEnvironmentServiceEntry>();

    // Legacy fields (v1 registry binary) for one-time migration.
    public List<string> GlobalMonoBehaviourTypeNames = new List<string>();
    public List<string> GlobalPlainTypeNames = new List<string>();
}

[Serializable]
public sealed class WindowSceneServiceEntry
{
    public string ScenePath;
    public List<string> EnabledBindingKeys = new List<string>();

    // Legacy fields (v1 registry binary) for one-time migration.
    public List<string> MonoBehaviourTypeNames = new List<string>();
    public List<string> PlainTypeNames = new List<string>();
}

[Serializable]
public sealed class WindowEnvironmentServiceEntry
{
    public string EnvironmentKey;
    public List<string> EnabledBindingKeys = new List<string>();
}
