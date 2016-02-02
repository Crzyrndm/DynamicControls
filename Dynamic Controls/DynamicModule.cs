using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    public abstract class DynamicModule : PartModule
    {
        public static EditorWindow windowInstance;

        public List<List<float>> deflectionAtValue; // float[0] = thrust%, float[1] = %deflection

        public abstract ConfigNode defaultSetup { get; set; }

        public PartModule module;

        /// <summary>
        /// The 100% deflection level
        /// </summary>
        public float deflection;

        /// <summary>
        /// The current max deflection angle
        /// </summary>
        public float currentDeflection;
        public bool loaded = false;

        public abstract string nodeName { get; }

        public override void OnLoad(ConfigNode node) // dynamic deflection node with actions and events subnodes
        {
            try
            {
                if (deflectionAtValue == null)
                    deflectionAtValue = new List<List<float>>();
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
            if (node.HasValue("deflection"))
                float.TryParse(node.GetValue("deflection"), out deflection);
            deflectionAtValue.Clear();
            foreach (string s in node.GetValues("key"))
            {
                string[] kvp = s.Split(',');
                List<float> val = new List<float>() { Mathf.Abs(float.Parse(kvp[0].Trim())), Mathf.Abs(float.Parse(kvp[1].Trim())) };
                deflectionAtValue.Add(val);
            }
            if (deflectionAtValue.Count == 0)
            {
                if (loadingDefaults)
                {
                    deflectionAtValue.Add(new List<float>() { 0, 100 });
                    defaultSetup.AddValue("key", "0,100");
                }
                else
                {
                    if (defaultSetup == null)
                        defaultSetup = new ConfigNode(nodeName);

                    LoadConfig(defaultSetup, true);
                }
            }
        }

        public void OnMouseOver()
        {
            if (Input.GetKeyDown(KeyCode.K))
                windowInstance.selectNewPart(this);
        }

        public override void OnSave(ConfigNode node)
        {
            if (!loaded)
                return;
            try
            {
                node = EditorWindow.toConfigNode(deflectionAtValue, node, false, deflection);
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
        private void LateUpdate()
        {
            dumpLog();
        }

        // list of everything to log in the next update from all loaded modules
        static List<object> toLog = new List<object>();
        public void Log(object objectToLog)
        {
            toLog.Add(objectToLog);
        }

        private void dumpLog()
        {
            if (toLog.Count == 0)
                return;

            string s = "";
            foreach (object o in toLog)
            {
                if (o == null)
                    continue;
                s += o.ToString() + "\r\n";
            }
            Debug.Log(s);

            toLog.Clear();
        }
        #endregion

        float lastLow = -1, lastHigh = -1, lowVal, highVal;
        /// <summary>
        /// Linear interpolation between points
        /// </summary>
        /// <param name="listToEvaluate">List of (x,y) pairs to interpolate between</param>
        /// <param name="x">the x-value to interpolate to</param>
        /// <returns>the fraction the value interpolates to</returns>
        public float Evaluate(List<List<float>> listToEvaluate, float x)
        {
            float val;
            int minLerpIndex = 0, maxLerpIndex = listToEvaluate.Count - 1;
            if (listToEvaluate[listToEvaluate.Count - 2][0] > listToEvaluate[maxLerpIndex][0]) // the last value may be a freshly added value. Can ignore in that case
                --maxLerpIndex;

            if (x < lastHigh && x > lastLow)
                val = lowVal + (highVal - lowVal) * (x - lastLow) / (lastHigh - lastLow);
            else if (x < listToEvaluate[0][0]) // clamp to minimum dyn pressure on the list
                val = listToEvaluate[0][1];
            else if (x > listToEvaluate[maxLerpIndex][0]) // clamp to max dyn pressure on the list
                val = listToEvaluate[maxLerpIndex][1];
            else // binary search the list
            {
                while (minLerpIndex < maxLerpIndex - 1)
                {
                    int midIndex = minLerpIndex + (maxLerpIndex - minLerpIndex) / 2;
                    if (listToEvaluate[midIndex][0] > x)
                        maxLerpIndex = midIndex;
                    else
                        minLerpIndex = midIndex;
                }
                val = listToEvaluate[minLerpIndex][1] + (x - listToEvaluate[minLerpIndex][0]) / (listToEvaluate[maxLerpIndex][0] - listToEvaluate[minLerpIndex][0]) * (listToEvaluate[maxLerpIndex][1] - listToEvaluate[minLerpIndex][1]);
                lastHigh = listToEvaluate[maxLerpIndex][0];
                highVal = listToEvaluate[maxLerpIndex][1];
                lastLow = listToEvaluate[minLerpIndex][0];
                lowVal = listToEvaluate[minLerpIndex][1];
            }
            return val / 100;
        }

        // copy to every control surface on the vessel, not just the sym counterparts
        public abstract void copyToAll();

        // need to make a copy of all subelements of the lists to prevent them permanently linking
        public static void copyToModule(DynamicModule m, List<List<float>> list)
        {
            m.deflectionAtValue = new List<List<float>>();
            foreach (List<float> kvp in list)
                m.deflectionAtValue.Add(new List<float>(kvp));
        }

        public abstract void UpdateDefaults(ConfigNode node);
    }
}
