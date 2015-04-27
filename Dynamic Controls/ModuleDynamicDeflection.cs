using System;
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

        public float deflection;
        public float currentDeflection;
        public float Q;

        public override void OnLoad(ConfigNode node) // dynamic deflection node with actions and events subnodes
        {
            try
            {
                if (deflectionAtPressure == null)
                    deflectionAtPressure = new List<List<float>>();
                LoadConfig(node);
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
            usingFAR = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name.Equals("FerramAerospaceResearch", StringComparison.InvariantCultureIgnoreCase)
                                                                || a.assembly.GetName().Name.Equals("NEAR", StringComparison.InvariantCultureIgnoreCase));
            if (part.Modules.Contains("FARControllableSurface"))
                module = part.Modules["FARControllableSurface"];
            else if (part.Modules.Contains("ModuleControlSurface"))
                module = part.Modules["ModuleControlSurface"];

            if (usingFAR)
                farValToSet = module.GetType().GetField("maxdeflect");

            if (deflectionAtPressure == null)
            {
                deflectionAtPressure = new List<List<float>>();

                if (defaults == null)
                    defaults = new ConfigNode(EditorWindow.nodeName);
                LoadConfig(defaults, true);
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (usingFAR)
                    deflection = (float)farValToSet.GetValue(module);
                else
                    deflection = (module as ModuleControlSurface).ctrlSurfaceRange;
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
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            Q = getQ(); // kPa

            currentDeflection = Mathf.Clamp(Evaluate(deflectionAtPressure, Q, deflection), 0.01f, 89);

            if (usingFAR)
                farValToSet.SetValue(module, currentDeflection);
            else
                (module as ModuleControlSurface).ctrlSurfaceRange = currentDeflection;
        }

        private void OnMouseOver()
        {
            if (Input.GetKeyDown(KeyCode.K))
                EditorWindow.Instance.selectNewPart(this);
        }

        public override void OnSave(ConfigNode node)
        {
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

        private float getQ() // roughly. no compensation for mach or temperature
        {
            //if (usingFAR)
            //    return (float)(getCurrentDensity() * vessel.srf_velocity.magnitude * vessel.srf_velocity.magnitude * 0.5);
            //else
            return (float)(FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(vessel.altitude, vessel.mainBody), FlightGlobals.getExternalTemperature(vessel.altitude, vessel.mainBody)) * vessel.srf_velocity.magnitude * vessel.srf_velocity.magnitude * 0.5) / 1000f;
        }

        /// <summary>
        /// FAR rho calculation
        /// </summary>
        /// <returns></returns>
        //private double getCurrentDensity()
        //{
        //    double altitude = vessel.mainBody.GetAltitude(part.transform.position);
        //    double temp = Math.Max(0.1, 273.15 + FlightGlobals.getExternalTemperature((float)altitude, vessel.mainBody));
        //    double currentBodyAtmPressureOffset = 0;
        //    if (vessel.mainBody.useLegacyAtmosphere && vessel.mainBody.atmosphere)
        //        currentBodyAtmPressureOffset = vessel.mainBody.atmosphereMultiplier * 1e-6;

        //    double pressure = FlightGlobals.getStaticPressure(part.transform.position, vessel.mainBody);
        //    if (pressure > 0)
        //        pressure = (pressure - currentBodyAtmPressureOffset) * 101.3;     //Need to convert atm to kPa

        //    return pressure / (temp * 287);
        //}

        public float Evaluate(List<List<float>> listToEvaluate, float x, float max)
        {
            List<float> minLerp = null, maxLerp = null;
            foreach (List<float> l in listToEvaluate)
            {
                if (l[0] < x)
                    minLerp = l;
                else
                {
                    maxLerp = l;
                    break;
                }
            }
            float y = 0;
            if (minLerp == null)
                y = maxLerp[1] * max / 100; // dynamic pressure less than first checkpoint
            else if (maxLerp == null)
                y = minLerp[1] * max / 100; // dynamic pressure more than last checkpoint
            else
                y = (minLerp[1] + (x - minLerp[0]) / (maxLerp[0] - minLerp[0]) * (maxLerp[1] - minLerp[1])) * max / 100;
            return y;
        }
    }
}
