using System.Collections.Generic;
using System;

using UnityEngine;
using UnityEngine.Windows;

using AlbiruniML;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;

public static class StyleTransfer
{
    public static TransformNet tnet;

    public static void Initialize(string path, string styleName)
    {
        tnet = new TransformNet(path, styleName);
        imageTensor = null;
        predictTensor = null;
    }

    public static void Reset() => tnet = null;
    private static Texture2D ResizeImage(Texture2D image, int width, int height)
    {
        image.Resize(width, height);
        image.Apply();

        return image;
    }

    public static bool isWorking = false;
    public static Tensor imageTensor;
    static Texture2D srcImage;
    public static Tensor predictTensor;

    public static Texture2D SrcImage { get { return srcImage; } }

    public static int Progress { get { 
        return tnet != null ? tnet.progress : 0; } }

    private static Tensor LoadImage(Texture2D image)
    {
        if (image == null)
        {
            UnityEngine.Debug.Log($"image {image} is null");
            return null;
        }
        srcImage = Texture2D.Instantiate(image);
        var x = AlbiruniML.Ops.buffer(new int[] { srcImage.height, srcImage.width, 3 });
        //Texture2D bmp = ResizeImage(srcImage, srcImage.width, srcImage.height);
        Texture2D bmp = image;
        for (int i = 0; i < bmp.height; i++)
        {
            for (int j = 0; j < bmp.width; j++)
            {
                Color clr = bmp.GetPixel(i, j);
                float red = clr.r;
                float green = clr.g;
                float blue = clr.b;

                x.Set(red, i, j, 0);
                x.Set(green, i, j, 1);
                x.Set(blue, i, j, 2);
            }
        }
        return x.toTensor();
    }
    public static Texture2D LoadTensor(object dataobject)
    {
        var data = dataobject as Tensor;
        Texture2D bmp = new Texture2D(data.Shape[1], data.Shape[0]);
        var colors = new Color[bmp.height * bmp.width];
        for (int i = 0; i < bmp.height; i++)
        {
            for (int j = 0; j < bmp.width; j++)
            {
                colors[(j * bmp.width) + i] = new Color(data.Get(i, j, 0),
                                    data.Get(i, j, 1),
                                    data.Get(i, j, 2));
            }
        }

        bmp.SetPixels(colors);
        bmp.Apply();

        return bmp;
    }

    public async static Task Transfer(Texture2D image)
    {
        if (image == null)
            UnityEngine.Debug.Log("{image} is null in Transfer()");

        if (tnet == null)
            UnityEngine.Debug.Log("styled models not found");

        imageTensor = LoadImage(image);
        if (imageTensor == null)
            UnityEngine.Debug.Log("imageTensor not found");

        imageTensor.keep();
        Tensor result = null;
        cts = new CancellationTokenSource();
        try {
            isWorking = true;
            await Task.Run(() => result = tnet.Predict(StyleTransfer.imageTensor), cts.Token);
        } catch (OperationCanceledException) {
            UnityEngine.Debug.Log("Running task cancelled");
            isWorking = false;
            UnityEditor.EditorUtility.ClearProgressBar();
        } catch {
            UnityEngine.Debug.Log("Failed to run task");
            isWorking = false;
        }
        cts = null;
        if(result != null)
            OpenImage.SaveImagePNG(LoadTensor(result));
    }

    private static CancellationTokenSource cts;
    public static CancellationToken ct => cts.Token;


    internal static void Cancel()
    {
        if (cts != null)
            cts.Cancel();

        isWorking = false;
    }
}

public class TransformNet
{
    Dictionary<string, Tensor> variables = new Dictionary<string, Tensor>();
    public Dictionary<string, Dictionary<string, Tensor>> variableDictionary = new Dictionary<string, Dictionary<string, Tensor>>();
    static Tensor timesScalar;
    static Tensor plusScalar;
    static Tensor epsilonScalar;

    private static float[] ReadW(string filename)
    {
        var d = File.ReadAllBytes(filename);

        float[] s = new float[d.Length / 4];
        int c = 0;
        for (int i = 0; i < d.Length; i += 4)
        {
            byte[] num = new byte[4];
            num[0] = d[i];
            num[1] = d[i + 1];
            num[2] = d[i + 2];
            num[3] = d[i + 3];
            s[c] = ToFloat(num);
            c++;
        }
        return s;
    }
    static float ToFloat(byte[] input)
    {
        byte[] newArray = new[] { input[0], input[1], input[2], input[3] };
        return BitConverter.ToSingle(newArray, 0);
    }
    public static Dictionary<string, Tensor> LoadFolder(string folderName)
    {
        string folderPath = Application.dataPath + folderName;
        string json;
        using (var reader = new System.IO.StreamReader(folderPath + "/manifest.json"))
        {
            json = reader.ReadToEnd();
        }

        var obj =
            JsonConvert.DeserializeObject<Dictionary<string, modelmetainfo>>(json);
        Dictionary<string, Tensor> res = new Dictionary<string, Tensor>();
        foreach (var item in obj)
        {
            float[] s = ReadW(folderPath + "/" + item.Value.filename);
            int size = item.Value.shape[0];
            for (int i = 1; i < item.Value.shape.Length; i++)
            {
                size *= item.Value.shape[i];
            }
            if (size != s.Length)
            {
                UnityEngine.Debug.Log("asdasD");
            }

            var t = AlbiruniML.Ops.tensor(s, item.Value.shape);

            res.Add(item.Key, t);

        }

        return res;
    }
    public TransformNet(string relativeFolder, string styleName)
    {
        timesScalar = AlbiruniML.Ops.scalar(150);
        plusScalar = AlbiruniML.Ops.scalar(255.0f / 2f);
        epsilonScalar = AlbiruniML.Ops.scalar(1e-3f);

        variableDictionary.Add("wave", LoadFolder($"/{relativeFolder}/wave"));
        variableDictionary.Add("la_muse", LoadFolder($"/{relativeFolder}/la_muse"));
        variableDictionary.Add("rain_princess", LoadFolder($"/{relativeFolder}/rain_princess"));
        variableDictionary.Add("scream", LoadFolder($"/{relativeFolder}/scream"));
        variableDictionary.Add("udnie", LoadFolder($"/{relativeFolder}/udnie"));
        variableDictionary.Add("wreck", LoadFolder($"/{relativeFolder}/wreck"));

        this.variables = variableDictionary[styleName];
        this.progress = 0;
    }
    public void ChangeVariable(string name)
    {
        this.variables = this.variableDictionary[name];
    }
    public static string varName(int varId)
    {
        if (varId == 0)
        {
            return "Variable";
        }
        else
        {
            return "Variable_" + varId.ToString();
        }
    }
    public Tensor instanceNorm(Tensor input, int varId)
    {

        var height = input.Shape[0];
        var width = input.Shape[1];
        var inDepth = input.Shape[2];
        var moments = AlbiruniML.Ops.moments(input, new int[] { 0, 1 });
        var mu = moments.mean;
        var sigmaSq = moments.variance;
        var shift = this.variables[varName(varId)];
        var scale = this.variables[varName(varId + 1)];
        var normalized = AlbiruniML.Ops.div(AlbiruniML.Ops.sub(input, mu),
           AlbiruniML.Ops.sqrt(AlbiruniML.Ops.add(sigmaSq, epsilonScalar)));
        var shifted = AlbiruniML.Ops.add(AlbiruniML.Ops.mul(scale, normalized), shift);

        return shifted.as3D(height, width, inDepth);

    }
    public Tensor convLayer(Tensor input, int strides, bool relu, int varId)
    {
        var y = AlbiruniML.Ops.conv2d(input,
 this.variables[varName(varId)], new int[] { strides, strides }, PadType.same);

        var y2 = this.instanceNorm(y, varId + 1);

        if (relu)
        {
            var rl = AlbiruniML.Ops.relu(y2);
            return rl;
        }
        return y2;
    }

    public Tensor convTransposeLayer(Tensor input, int numFilters, int strides, int varId)
    {
        var height = input.Shape[0];
        var width = input.Shape[1];
        var newRows = height * strides;
        var newCols = width * strides;
        var newShape = new int[] { newRows, newCols, numFilters };
        var y = AlbiruniML.Ops.conv2dTranspose(input,
  this.variables[varName(varId)],
  newShape, new int[] { strides, strides }, PadType.same);
        var y2 = this.instanceNorm(y, varId + 1);
        var y3 = AlbiruniML.Ops.relu(y2);
        return y3;
    }

    public int progress;

    public void InvokeProgressEvent(int progress)
    {
        if (StyleTransfer.ct.IsCancellationRequested)
            StyleTransfer.ct.ThrowIfCancellationRequested();

        this.progress = progress;
        UnityEngine.Debug.Log(progress);
    }

    public Tensor residualBlock(Tensor input, int varId)
    {
        var conv1 = this.convLayer(input, 1, true, varId);
        var conv2 = this.convLayer(conv1, 1, false, varId + 3);
        return AlbiruniML.Ops.addStrict(conv2, input);
    }

    public Tensor Predict(Tensor preprocessedInput)
    {
        InvokeProgressEvent(1);
        var conv1 = this.convLayer(preprocessedInput, 1, true, 0);
        InvokeProgressEvent(10);
        var conv2 = this.convLayer(conv1, 2, true, 3);
        InvokeProgressEvent(20);
        var conv3 = this.convLayer(conv2, 2, true, 6);
        InvokeProgressEvent(30);
        var resid1 = this.residualBlock(conv3, 9);
        InvokeProgressEvent(40);
        var resid2 = this.residualBlock(resid1, 15);
        InvokeProgressEvent(50);
        var resid3 = this.residualBlock(resid2, 21);
        InvokeProgressEvent(60);
        var resid4 = this.residualBlock(resid3, 27);
        InvokeProgressEvent(70);
        var resid5 = this.residualBlock(resid4, 33);

        InvokeProgressEvent(80);
        var convT1 = this.convTransposeLayer(resid5, 64, 2, 39);
        InvokeProgressEvent(90);
        var convT2 = this.convTransposeLayer(convT1, 32, 2, 42);
        InvokeProgressEvent(100);
        var convT3 = this.convLayer(convT2, 1, false, 45);
        InvokeProgressEvent(110);
        var outTanh = AlbiruniML.Ops.tanh(convT3);
        var scaled = AlbiruniML.Ops.mul(timesScalar, outTanh);
        var shifted = AlbiruniML.Ops.add(plusScalar, scaled);
        var clamped = AlbiruniML.Ops.clipByValue(shifted, 0, 255);
        var normalized = AlbiruniML.Ops.div(
            clamped, AlbiruniML.Ops.scalar(255.0f));

        InvokeProgressEvent(120);
        return normalized;


    }
}

[System.Serializable]
public class modelmetainfo
{
    public string filename { get; set; }
    public int[] shape { get; set; }
}


