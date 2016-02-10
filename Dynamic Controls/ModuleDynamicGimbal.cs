﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace Dynamic_Controls
{
    public class ModuleDynamicGimbal : PartModule
    {
        public List<List<float>> deflectionAtThrust; // int[0] = q, int[1] = deflection
        public static ConfigNode defaults;
        public ModuleGimbal gimbalModule;

        public List<ModuleEngines> engineModules;
        public ModuleEngines engineModule;

        /// <summary>
        /// The 100% deflection level
        /// </summary>
        public float deflection;

        /// <summary>
        /// The current max delfection angle
        /// </summary>
        public float currentDeflection;
        bool loaded = false;

        public override void OnLoad(ConfigNode node) // dynamic deflection node with actions and events subnodes
        {
            try
            {
                if (deflectionAtThrust == null)
                    deflectionAtThrust = new List<List<float>>();
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
            deflectionAtThrust.Clear();
            foreach (string s in node.GetValues("key"))
            {
                string[] kvp = s.Split(',');
                List<float> val = new List<float>() { Mathf.Abs(float.Parse(kvp[0].Trim())), Mathf.Abs(float.Parse(kvp[1].Trim())) };
                deflectionAtThrust.Add(val);
            }
            if (deflectionAtThrust.Count == 0)
            {
                if (loadingDefaults)
                {
                    deflectionAtThrust.Add(new List<float>() { 0, 100 });
                    defaults.AddValue("key", "0,100");
                }
                else
                {
                    if (defaults == null)
                        defaults = new ConfigNode(EditorWindowGimbal.nodeName);

                    LoadConfig(defaults, true);
                }
            }
        }

        public void Start()
        {
            gimbalModule = part.Modules.OfType<ModuleGimbal>().FirstOrDefault();
            engineModules = part.Modules.OfType<ModuleEngines>().ToList();
            engineModule = engineModules.FirstOrDefault();

            if (deflectionAtThrust == null)
            {
                deflectionAtThrust = new List<List<float>>();

                if (defaults == null)
                    defaults = new ConfigNode(EditorWindowGimbal.nodeName);
                LoadConfig(defaults, true);
            }

            if (!loaded)
            {
                deflection = gimbalModule.gimbalRange;
                loaded = true;
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
                deflection = gimbalModule.gimbalRange;

            if (part.isActiveAndEnabled && engineModule.finalThrust == 0 && engineModules.Count > 1 && vessel.ctrlState.mainThrottle > 0)
            {
                ModuleEngines maxThrustModule = engineModules[0];
                for (int i = 1; i < engineModules.Count; i++)
                {
                    if (engineModules[i].finalThrust > maxThrustModule.finalThrust)
                        maxThrustModule = engineModules[i];
                }
                engineModule = maxThrustModule;
            }

            if (EditorWindowGimbal.Instance.moduleToDraw != this)
                return;

            foreach (Part p in part.symmetryCounterparts)
            {
                if (p != null)
                    EditorWindowGimbal.copyToModule(p.Modules["ModuleDynamicGimbal"] as ModuleDynamicGimbal, deflectionAtThrust);
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel.HoldPhysics)
                return;

            currentDeflection = Mathf.Clamp(Evaluate(deflectionAtThrust, 100 * engineModule.finalThrust / engineModule.maxThrust) * deflection, 0, 45);

            gimbalModule.gimbalRange = currentDeflection;
        }

        private void OnMouseOver()
        {
            if (Input.GetKeyDown(KeyCode.K))
                EditorWindowGimbal.Instance.selectNewPart(this);
        }

        public override void OnSave(ConfigNode node)
        {
            if (!loaded)
                return;
            try
            {
                node = EditorWindowGimbal.toConfigNode(deflectionAtThrust, node, false, deflection);
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
        private void Log(object objectToLog)
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
    }
}
