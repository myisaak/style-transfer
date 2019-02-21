using System.IO;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;

public class OpenImage : EditorWindow {

    private static string path = "";
    private static Task t;

    [MenuItem("Window/Style Transfer/Wave")]
    static void Waves() => ApplyAsync("wave");

    [MenuItem("Window/Style Transfer/La Muse")]
    static void LaMuse() => ApplyAsync("la_muse");

    [MenuItem("Window/Style Transfer/Rain Princess")]
    static void RainPrincess() => ApplyAsync("rain_princess");

    [MenuItem("Window/Style Transfer/Scream")]
    static void Scream() => ApplyAsync("scream");

    [MenuItem("Window/Style Transfer/Udnie")]
    static void Udnie() => ApplyAsync("udnie");

    [MenuItem("Window/Style Transfer/Wreck")]
    static void Wreck() => ApplyAsync("wreck");

    async static void ApplyAsync(string styleName) {
        var sw = new Stopwatch();
        sw.Start();
        StyleTransfer.Initialize("styles", styleName);
        sw.Stop();
        UnityEngine.Debug.Log($"Initialization time elapsed: {sw.Elapsed.Seconds} secs");
        
        Texture2D texture = Selection.activeObject as Texture2D;
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Select Texture", "You must select a texture first!", "OK");
            return;
        }

        path = AssetDatabase.GetAssetPath(texture);
        //path = EditorUtility.OpenFilePanel("Overwrite with png", "", "png");

        if (path.Length != 0)
        {
            // var fileContent = File.ReadAllBytes(path);
            // texture.LoadImage(fileContent);
            path = EditorUtility.SaveFilePanel("Save styled image", $"{Application.dataPath}/{path}", "", "png");
            if (path.Length != 0)
            {
                sw.Restart();
                await (t = StyleTransfer.Transfer(texture));
                sw.Stop();
                UnityEngine.Debug.Log($"Running time elapsed: {sw.Elapsed.Seconds} secs");
                AssetDatabase.Refresh();
            }
        } else {
            UnityEngine.Debug.Log("Texture not found");
        }

        EditorUtility.ClearProgressBar();
    }

    [MenuItem("Window/Style Transfer/Editor")]
    static void Init() {
        var window = GetWindow(typeof(OpenImage));
    }

    Rect rect;

    void OnGUI() {
        rect = new Rect(3, 3, position.width - 3, 20);
        if (GUI.Button(rect, !StyleTransfer.isWorking ? "Start" : "Stop")) {
            if (!StyleTransfer.isWorking) {
                ApplyAsync("rain_princess");
            }
            else {
                StyleTransfer.Reset();
                StyleTransfer.Cancel();
            }
        }

        if (StyleTransfer.Progress >= 0 && StyleTransfer.Progress <= 120) {
            rect = new Rect(3, 23, position.width - 3, 20);
            float progress = StyleTransfer.Progress / 120f;

            EditorGUI.ProgressBar(rect, progress,"Progress");
        }
    }

    public static void SaveImagePNG(Texture2D image) {
        if (image == null) {
            UnityEngine.Debug.Log($"SaveImagePNG(image: {nameof(image)} is null)");
        }

        if (path.Length != 0) {
            File.WriteAllBytes(path, image.EncodeToPNG());
        } else {
            UnityEngine.Debug.Log("Please pick a file location");
        }
    }
}