using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Eriksson.StyleTransfer
{
    public class StyleTransferEditor : EditorWindow
    {
        public enum Style { wave, la_muse, rain_princess, scream, udnie, wreck }

        private static string path = "";
        private static Task t;
        private static Rect rect;
        private static Style style;
        private static MonoScript ms;
        private static StyleTransferEditor instance;

        [MenuItem("Window/Style Transfer/Editor")]
        public static void Init()
        {
            var window = GetWindow(typeof(StyleTransferEditor));
        }

        private void OnGUI()
        {
            if (instance == null) {
                instance = this;
            }
            rect = new Rect(3, 3, position.width - 3, 20);
            style = (Style)EditorGUI.EnumPopup(rect, style);

            rect = new Rect(3, 23, position.width - 3, 20);
            if (GUI.Button(rect, !Main.IsWorking ? "Start" : "Stop"))
            {
                if (!Main.IsWorking)
                {
                    ApplyAsync(style.ToString());
                }
                else
                {
                    Main.Cancel();
                }
            }

            if (Main.Progress >= 0 && Main.Progress <= 120)
            {
                rect = new Rect(3, 43, position.width - 3, 20);
                float progress = Main.Progress / 120f;

                EditorGUI.ProgressBar(rect, progress, "Progress");
            }
        }

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
            try {
                var sw = new Stopwatch();
                sw.Start();

                var ms = MonoScript.FromScriptableObject(instance);
                string path = AssetDatabase.GetAssetPath(ms)+"/../../Styles";

                UnityEngine.Debug.Log(path);
                Main.Initialize(path, styleName);
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
                        await (t = Main.StartTransfer(texture, path));
                        sw.Stop();
                        UnityEngine.Debug.Log($"Running time elapsed: {sw.Elapsed.Seconds} secs");
                        AssetDatabase.Refresh();
                    }
                }

                Main.Cancel();
            } catch (UnityException e){
                UnityEngine.Debug.Log("An error has occured");
                UnityEngine.Debug.Log(e);
            }
        }
    }
}