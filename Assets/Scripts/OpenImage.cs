using System.IO;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;

public class OpenImage : Editor
{
    private static string path = "";
    private static bool executing = false;
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
    async static void ApplyAsync(string styleName)
    {
        EditorApplication.update += Update;

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
        }

        EditorApplication.update -= Update;
        EditorUtility.ClearProgressBar();
    }

    static void Update() {
        if (StyleTransfer.Progress >= 0 && StyleTransfer.Progress <= 120)
        {
            bool cancel = UnityEditor.EditorUtility.DisplayCancelableProgressBar("Loading...", $"{StyleTransfer.Progress}/120", (float)StyleTransfer.Progress / 120f);
            if(cancel) {
                EditorUtility.ClearProgressBar();
                StyleTransfer.Reset();
                EditorApplication.update -= Update;
                StyleTransfer.Cancel();
                return;
            }
            if (StyleTransfer.Progress >= 120)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }

    public static void SaveImagePNG(Texture2D image)
    {
        if (image == null)
        {
            UnityEngine.Debug.Log($"SaveImagePNG(image: {nameof(image)} is null)");
        }

        if (path.Length != 0)
        {
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        else
        {
            UnityEngine.Debug.Log("Please pick a file location");
        }
    }
}