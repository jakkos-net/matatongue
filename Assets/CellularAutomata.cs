using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.UI;

[RequireComponent(typeof(VisualEffect))]
public class CellularAutomata : MonoBehaviour
{
    [SerializeField] uint axisSize;
    [SerializeField] float timeBetweenUpdates;
    [SerializeField] ComputeShader shader;
    uint birthRule;
    uint surviveRule;
    uint states;

    float timeSinceLastUpdate = 0;
    RenderTexture oldTex;
    RenderTexture newTex;
    VisualEffect visualizer;
    

    void Start()
    {
        visualizer = GetComponent<VisualEffect>();


        var desc = new RenderTextureDescriptor((int)axisSize, (int)axisSize);
        desc.colorFormat = RenderTextureFormat.ARGBFloat;
        desc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        desc.volumeDepth = (int) axisSize;
        desc.enableRandomWrite = true;
        oldTex = new RenderTexture(desc);
        newTex = new RenderTexture(desc);
        oldTex.Create();
        newTex.Create();
        ParseRuleText();
        RandomiseTexture(oldTex);   
    }

    void ParseRuleText()
    {
        string text = GameObject.Find("Rule Input Field").GetComponent<InputField>().text;
        text = Regex.Replace(text, @"\s+", "");
        var splits = text.Split('/');

        surviveRule = RangesToRule(splits[0]);
        birthRule = RangesToRule(splits[1]);
        states = uint.Parse(splits[2]);
    }

    uint RangesToRule(string rangesText)
    {
        uint rule = 0;
        var splitRangesText = rangesText.Split(',');
        foreach (var rangeText in splitRangesText)
        {
            var range = rangeText.Split('-');

            if (range.Length == 1)
            {
                if (range[0] != "")
                {
                    var num = int.Parse(range[0]);
                    rule += RangeToRule(num, num);
                }
            }
            else if (range.Length == 2)
            {
                rule += RangeToRule(int.Parse(range[0]), int.Parse(range[1]));
            }
        }

        return rule;
    }

    public void Restart()
    {
        OnDestroy();
        Start();
    }

    uint RangeToRule(int start, int end)
    {
        uint result = 0;

        for(int i = start; i <= end; i++)
        {
            result += (uint) Mathf.RoundToInt(Mathf.Pow(2,i));
        }

        return result;
    }

    enum RandomType
    {
        Blob
    }

    void RandomiseTexture(RenderTexture tex, RandomType type = RandomType.Blob)
    {
        Color[] pixelData = new Color[axisSize * axisSize * axisSize];
        int i = 0;
        UnityEngine.Random.InitState(Time.time.GetHashCode());
        var coord = new Vector3(UnityEngine.Random.Range(axisSize / 3f, 2 * axisSize / 3f), UnityEngine.Random.Range(axisSize / 3f, 2 * axisSize / 3f), UnityEngine.Random.Range(axisSize / 3f, 2 * axisSize / 3f));
        var radius = UnityEngine.Random.Range(axisSize / 10f, axisSize);
        var density = UnityEngine.Random.Range(0.2f, 0.5f);
        for (int x = 0; x < tex.width; x++)
        {
            for(int y =0; y < tex.height; y++)
            {
                for(int z = 0; z < tex.volumeDepth; z++)
                {
                    
                    pixelData[i] = new Color(
                        Vector3.Distance(new Vector3(x,y,z), coord) < radius && UnityEngine.Random.Range(0f, 1f) < density ? states-1 : 0,
                        0,
                        0,
                        0
                        );
                    i += 1;
                }
            }
        }

        var initTex = new Texture3D((int)axisSize, (int)axisSize, (int)axisSize, TextureFormat.RGBAFloat, false);
        initTex.SetPixels(pixelData);
        initTex.Apply();
        Graphics.CopyTexture(initTex, tex);
    }

    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= timeBetweenUpdates)
        {
            timeSinceLastUpdate = 0;
            SimulationStep();
        }
    }

    void SimulationStep()
    {
        int kernel = shader.FindKernel("CSMain");

        shader.SetInt("_BirthRule", (int) birthRule);
        shader.SetInt("_SurviveRule", (int) surviveRule);
        shader.SetInt("_Size", (int) axisSize);
        shader.SetInt("_States", (int)states - 1);
        shader.SetTexture(kernel, "Input", oldTex);
        shader.SetTexture(kernel, "Output", newTex);
        shader.Dispatch(kernel, ((int)axisSize / 8) + 1, ((int)axisSize / 8) + 1, ((int)axisSize / 8) + 1);

        visualizer.SetTexture("_MainTex", newTex);
        visualizer.SetUInt("_Size", axisSize * axisSize * axisSize);
        visualizer.SetUInt("_AxisSize", axisSize);
        visualizer.SetUInt("_SqrAxisSize", axisSize * axisSize);
        visualizer.SetUInt("_States", states - 1);
        visualizer.SetFloat("_TexelSize", 1.0f/axisSize);

        SwapTextures();
    }

    private void OnDestroy()
    {
        oldTex.Release();
        newTex.Release();
    }

    void SwapTextures()
    {
        var temp = oldTex;
        oldTex = newTex;
        newTex = temp;
    }
}
