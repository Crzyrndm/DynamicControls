using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace Dynamic_Controls
{
    public class ModuleDynamicDeflection : PartModule
    {
        public List<List<float>> deflectionAtPressure; // int[0] = q, int[1] = deflection
        private bool usingFAR;

        private PartModule module;
        private FieldInfo farValToSet;

        [KSPField(guiActive = true, guiName = "Max Deflect", guiActiveEditor = false)]
        public float deflection;

        [KSPField(guiActive = true, guiName = "Deflection", guiActiveEditor = false)]
        public float currentDeflection;

        public override void OnLoad(ConfigNode node) // dynamic deflection node with actions and events subnodes
        {
            try
            {
                if (deflectionAtPressure == null)
                    deflectionAtPressure = new List<List<float>>();

                foreach (string s in node.GetValues("key"))
                {
                    string[] kvp = s.Split(',');
                    List<float> val = new List<float>() { Mathf.Abs(float.Parse(kvp[0].Trim())), Mathf.Abs(float.Parse(kvp[1].Trim())) };
                    deflectionAtPressure.Add(val);
                }
                if (deflectionAtPressure.Count == 0)
                    deflectionAtPressure.Add(new List<float>() { 0, 100 });
            }
            catch
            {
                Log("OnLoad failed");
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
            {
                farValToSet = module.GetType().GetField("maxdeflect");
                deflection = (float)farValToSet.GetValue(module);
            }
            else
                deflection = (module as ModuleControlSurface).ctrlSurfaceRange;
        }

        public void Update()
        {
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

            if (deflectionAtPressure.Count == 1)
                return;

            float dynPres = getQ() / 1000; // kPa
            List<float> minLerp = null, maxLerp = null;
            foreach (List<float> x in deflectionAtPressure)
            {
                if (x[0] < dynPres)
                    minLerp = x;
                else
                {
                    maxLerp = x;
                    break;
                }
            }
            if (minLerp == null)
                currentDeflection = maxLerp[1] * deflection / 100; // dynamic pressure less than first checkpoint
            else if (maxLerp == null)
                currentDeflection = minLerp[1] * deflection / 100; // dynamic pressure more than last checkpoint
            else
                currentDeflection = (minLerp[1] + (dynPres - minLerp[0]) / (maxLerp[0] - minLerp[0]) * (maxLerp[1] - minLerp[1])) * deflection / 100;

            currentDeflection = Mathf.Clamp(currentDeflection, 0.01f, 89);

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
                base.OnSave(node);
                if (deflectionAtPressure == null)
                    return;

                foreach (List<float> kvp in deflectionAtPressure)
                    node.AddValue("key", kvp[0].ToString() + "," + kvp[1].ToString());
            }
            catch
            {
                Log("Onsave failed");
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
                s += o.ToString() + "\r\n";
            Debug.Log(s);

            toLog.Clear();
        }

        private float getQ() // roughly. no compensation for mach or temperature
        {
            return (float)(FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(vessel.altitude, vessel.mainBody)) * vessel.srf_velocity.magnitude * vessel.srf_velocity.magnitude * 0.5);
        }
        #endregion
    }
}
