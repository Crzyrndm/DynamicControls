using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    public class AeroPair
    {
        public float Q;
        public float deflection;

        public AeroPair(float q, float deflect)
        {
            Q = q;
            deflection = deflect;
        }
    }

    public class ModuleDynamicDeflection : PartModule
    {
        public List<AeroPair> deflectionAtPressure;

        public IModuleInterface aeroModule;

        public static ConfigNode defaults;

        /// <summary>
        /// The current max deflection angle
        /// </summary>
        public float currentDeflection;
        bool loaded = false;

        public override void OnLoad(ConfigNode node) // dynamic deflection node with actions and events subnodes
        {
            try
            {
                deflectionAtPressure = deflectionAtPressure ?? new List<AeroPair>();
                LoadConfig(node);
                loaded = true;
            }
            catch (Exception ex)
            {
                Log("OnLoad failed");
                Log(ex.InnerException);
                Log(ex.StackTrace);
            }
        }

        public void LoadConfig(ConfigNode node, bool loadingDefaults = false)
        {
            deflectionAtPressure.Clear();
            foreach (string s in node.GetValues("key"))
            {
                string[] kvp = s.Split(',');
                deflectionAtPressure.Add(new AeroPair(Mathf.Abs(float.Parse(kvp[0].Trim())), Mathf.Abs(float.Parse(kvp[1].Trim()))));
            }
            if (deflectionAtPressure.Count == 0)
            {
                if (loadingDefaults)
                {
                    deflectionAtPressure.Add(new AeroPair(0, 100));
                    defaults.AddValue("key", "0,100");
                }
                else
                {
                    defaults = defaults ?? new ConfigNode(EditorWindow.nodeName);
                    LoadConfig(defaults, true);
                }
            }
        }


        public void Start()
        {
            if (AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name.Equals("FerramAerospaceResearch", StringComparison.InvariantCultureIgnoreCase)))
            {
                aeroModule = new FARModule(part);
            }
            else
            {
                aeroModule = new StockModule(part.Modules.GetModule<ModuleControlSurface>());
            }

            if (deflectionAtPressure == null)
            {
                deflectionAtPressure = new List<AeroPair>();
                defaults = defaults ?? new ConfigNode(EditorWindow.nodeName);
                LoadConfig(defaults, true);
            }
            loaded = true;
        }

        public void Update()
        {
            if (EditorWindow.Instance.moduleToDraw != this)
            {
                return;
            }
            foreach (Part p in part.symmetryCounterparts)
            {
                if (p != null)
                {
                    EditorWindow.CopyToModule(p.Modules.GetModule<ModuleDynamicDeflection>(), deflectionAtPressure);
                }
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel.HoldPhysics)
            {
                return;
            }
            currentDeflection = Mathf.Clamp(Evaluate(deflectionAtPressure, (float)vessel.dynamicPressurekPa) * aeroModule.GetDefaultMaxDeflect(), -89, 89);
            aeroModule.SetMaxDeflect(Math.Abs(currentDeflection));
        }

        private void OnMouseOver()
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                EditorWindow.Instance.SelectNewPart(this);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if (!loaded)
            {
                return;
            }
            try
            {
                node = EditorWindow.ToConfigNode(deflectionAtPressure, node, false);
                base.OnSave(node);
            }
            catch (Exception ex)
            {
                Log("Onsave failed");
                Log(ex.InnerException);
                Log(ex.StackTrace);
            }
        }

        #region logging

        private void Log(object objectToLog)
        {
            Debug.Log($"[Dynamic Deflection] {objectToLog.ToString()}");
        }
        #endregion

        float lastLow = -1, lastHigh = -1, lowVal, highVal;
        /// <summary>
        /// Linear interpolation between points
        /// </summary>
        /// <param name="listToEvaluate">List of (x,y) pairs to interpolate between</param>
        /// <param name="x">the x-value to interpolate to</param>
        /// <returns>the fraction the value interpolates to</returns>
        public float Evaluate(List<AeroPair> listToEvaluate, float x)
        {
            int maxLerpIndex = listToEvaluate.Count - 1;
            if (listToEvaluate[maxLerpIndex - 1].Q > listToEvaluate[maxLerpIndex].Q) // the last value may be a freshly added value. Can ignore in that case
            {
                --maxLerpIndex;
            }
            switch (x)
            {
                // still within previous bounds
                case var test when test < lastHigh && test > lastLow:
                    x = lowVal + (highVal - lowVal) * (x - lastLow) / (lastHigh - lastLow);
                    break;
                // less than min val
                case var test when test < listToEvaluate[0].Q:
                    x = listToEvaluate[0].deflection;
                    break;
                // if currently exceeding the max val
                case var test when test > listToEvaluate[maxLerpIndex].Q:
                    x = listToEvaluate[maxLerpIndex].deflection;
                    break;
                // search for new bounds
                default:
                    int minLerpIndex = 0;
                    while (minLerpIndex < maxLerpIndex - 1)
                    {
                        int midIndex = minLerpIndex + (maxLerpIndex - minLerpIndex) / 2;
                        if (listToEvaluate[midIndex].Q > x)
                        {
                            maxLerpIndex = midIndex;
                        }
                        else
                        {
                            minLerpIndex = midIndex;
                        }
                    }
                    x = listToEvaluate[minLerpIndex].deflection + 
                        (x - listToEvaluate[minLerpIndex].Q) / (listToEvaluate[maxLerpIndex].Q - listToEvaluate[minLerpIndex].Q) * (listToEvaluate[maxLerpIndex].deflection - listToEvaluate[minLerpIndex].deflection);
                    lastHigh = listToEvaluate[maxLerpIndex].Q;
                    highVal = listToEvaluate[maxLerpIndex].deflection;
                    lastLow = listToEvaluate[minLerpIndex].Q;
                    lowVal = listToEvaluate[minLerpIndex].deflection;
                    break;
            }
            return x / 100;
        }
    }
}
