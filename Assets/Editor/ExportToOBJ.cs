using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// Export selected objects to OBJ format for use in external tools
/// Place in Assets/Editor/ folder
/// </summary>
public class ExportToOBJ : EditorWindow
{
    [MenuItem("Tools/Export Selected to OBJ")]
    static void ExportSelectedToOBJ()
    {
        GameObject[] selection = Selection.gameObjects;

        if (selection.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select an object to export!", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Export as OBJ", "", selection[0].name, "obj");

        if (string.IsNullOrEmpty(path))
            return;

        StringBuilder sb = new StringBuilder();
        int vertexOffset = 1;

        foreach (GameObject obj in selection)
        {
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();

            if (meshFilter == null)
            {
                Debug.LogWarning($"No mesh found on {obj.name}");
                continue;
            }

            Mesh mesh = meshFilter.sharedMesh;

            if (mesh == null)
            {
                Debug.LogWarning($"No shared mesh on {obj.name}");
                continue;
            }

            sb.AppendLine($"# {obj.name}");
            sb.AppendLine($"g {obj.name}");

            // Write vertices
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 worldV = obj.transform.TransformPoint(v);
                sb.AppendLine($"v {worldV.x} {worldV.y} {worldV.z}");
            }

            // Write normals
            foreach (Vector3 n in mesh.normals)
            {
                Vector3 worldN = obj.transform.TransformDirection(n);
                sb.AppendLine($"vn {worldN.x} {worldN.y} {worldN.z}");
            }

            // Write UVs
            foreach (Vector2 uv in mesh.uv)
            {
                sb.AppendLine($"vt {uv.x} {uv.y}");
            }

            // Write faces
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                int i1 = mesh.triangles[i] + vertexOffset;
                int i2 = mesh.triangles[i + 1] + vertexOffset;
                int i3 = mesh.triangles[i + 2] + vertexOffset;

                sb.AppendLine($"f {i1}/{i1}/{i1} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
            }

            vertexOffset += mesh.vertices.Length;
        }

        File.WriteAllText(path, sb.ToString());

        Debug.Log($"✅ Exported to: {path}");
        EditorUtility.DisplayDialog("Export Complete", $"Exported successfully to:\n{path}", "OK");
    }

    [MenuItem("Tools/Export Selected to OBJ", true)]
    static bool ValidateExport()
    {
        return Selection.gameObjects.Length > 0;
    }
}