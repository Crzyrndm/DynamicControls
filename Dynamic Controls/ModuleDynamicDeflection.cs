﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace Dynamic_Controls
{
    public class ModuleDynamicDeflection : PartModule
    {
        public List<List<float>> deflectionAtPressure; // int[0] = q, int[1] = deflection
        private bool usingFAR;

        public static ConfigNode defaults;

        private PartModule module;
        private FieldInfo farValToSet;

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
                if (deflectionAtPressure == null)
                    deflectionAtPressure = new List<List<float>>();
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
            deflectionAtPressure.Clear();
            foreach (string s in node.GetValues("key"))
            {
                string[] kvp = s.Split(',');
                List<float> val = new List<float>() { Mathf.Abs(float.Parse(kvp[0].Trim())), Mathf.Abs(float.Parse(kvp[1].Trim())) };
                deflectionAtPressure.Add(val);
            }
            if (deflectionAtPressure.Count == 0)
            {
                if (loadingDefaults)
                {
                    deflectionAtPressure.Add(new List<float>() { 0, 100 });
                    defaults.AddValue("key", "0,100");
                }
                else
                {
                    if (defaults == null)
                        defaults = new ConfigNode(EditorWindow.nodeName);

                    LoadConfig(defaults, true);
                }
            }
        }

        public void Start()
        {
            usingFAR = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name.Equals("FerramAerospaceResearch", StringComparison.InvariantCultureIgnoreCase));
            if (part.Modules.Contains("FARControllableSurface"))
                module = part.Modules["FARControllableSurface"];
            else if (part.Modules.OfType<ModuleControlSurface>().Any())
                module = part.Modules.OfType<ModuleControlSurface>().FirstOrDefault();

            if (usingFAR)
                farValToSet = module.GetType().GetField("maxdeflect");

            if (deflectionAtPressure == null)
            {
                deflectionAtPressure = new List<List<float>>();

                if (defaults == null)
                    defaults = new ConfigNode(EditorWindow.nodeName);
                LoadConfig(defaults, true);
            }

            if (!loaded)
            {
                if (usingFAR)
                    deflection = (float)farValToSet.GetValue(module);
                else
                    deflection = ((ModuleControlSurface)module).ctrlSurfaceRange;
                loaded = true;
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (usingFAR)
                    deflection = (float)farValToSet.GetValue(module);
                else
                    deflection = ((ModuleControlSurface)module).ctrlSurfaceRange;
            }

            if (EditorWindow.Instance.moduleToDraw != this)
                return;

            foreach (Part p in part.symmetryCounterparts)
            {
                if (p != null)
                    EditorWindow.copyToModule(p.Modules["ModuleDynamicDeflection"] as ModuleDynamicDeflection, deflectionAtPressure);
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel.HoldPhysics)
                return;

            currentDeflection = Mathf.Clamp(Evaluate(deflectionAtPressure, (float)vessel.dynamicPressurekPa) * deflection, -89, 89);

            if (usingFAR)
                farValToSet.SetValue(module, currentDeflection);
            else
                ((ModuleControlSurface)module).ctrlSurfaceRange = currentDeflection;
        }

        private void OnMouseOver()
        {
            if (Input.GetKeyDown(KeyCode.K))
                EditorWindow.Instance.selectNewPart(this);
        }

        public override void OnSave(ConfigNode node)
        {
            if (!loaded)
                return;
            try
            {
                node = EditorWindow.toConfigNode(deflectionAtPressure, node, false, deflection);
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
