using UnityEditor;
using UnityEngine;

namespace UnityTemplateProjects.TerrainGeneration.Editor {

public class TerrainGraphWindow : EditorWindow {
    
    [UnityEditor.Callbacks.OnOpenAsset(0)]
    public static bool ShowEditor (int instanceID, int line) {
        var targetObject = EditorUtility.InstanceIDToObject(instanceID);

        return false;
    }

    [MenuItem("TerrainGeneration/TerrainGraphWindow")]
    private static void ShowWindow () {
        var window = GetWindow<TerrainGraphWindow>();
        window.titleContent = new GUIContent("TerrainGraphWindow");
        window.Show();
    }

    private void OnGUI () {
        // current = this;
        //
        // if (graph == null) {
        //     return;
        // }
        //
        // if (graphUI.undo == null) {
        //     graphUI.undo = new Den.Tools.GUI.Undo {undoObject = graph, undoName = "MapMagic Graph Change"};
        //     graphUI.undo.undoAction = GraphWindow.RefreshMapMagic;
        // }
        //
        // graphUI.undo.undoObject = graph;

        BeginWindows();
        EndWindows();
    }

}

}