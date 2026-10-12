#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class EditorPlayModePauseShortcut
{
    [Shortcut("Play Mode/Pause Editor", KeyCode.Tab)]
    static void PauseEditor()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        EditorApplication.isPaused = true;
    }
}
#endif