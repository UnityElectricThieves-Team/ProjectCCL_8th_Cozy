using UnityEditor;

/// <summary>
/// <see cref="BackgroundStrip"/> 인스펙터에 높이가 어디서 오는지 안내를 띄운다.
/// 높이는 이 컴포넌트의 필드가 아니라 <see cref="BackgroundSystem"/>의 인스펙터 값이라서,
/// 안내가 없으면 프리팹을 열어 본 사람이 "높이를 어디서 바꾸지?" 하고 헤매게 된다.
/// </summary>
[CustomEditor(typeof(BackgroundStrip))]
public sealed class BackgroundStripEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            $"띠 높이는 씬의 {nameof(BackgroundSystem)} 인스펙터에 있는 Height Base Px 값을 참조합니다. 여기서는 바꾸지 않습니다.",
            MessageType.Info);
    }
}
