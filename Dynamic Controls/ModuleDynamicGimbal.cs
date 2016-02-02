using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    class ModuleDynamicGimbal : DynamicModule
    {
        ModuleEngines engine;

        public static ConfigNode defaults;
        public override ConfigNode defaultSetup
        {
            get { return defaults; }
            set { defaults = value; }
        }

        public override string nodeName
        {
            get { return "DynamicGimbal"; }
        }
        public void Awake()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                ModuleDynamicGimbal.defaults = GameDatabase.Instance.GetConfigNodes(nodeName).FirstOrDefault();
        }

        public void Start()
        {
            module = part.Modules.OfType<ModuleGimbal>().FirstOrDefault();
            engine = part.Modules.OfType<ModuleEngines>().FirstOrDefault();

            if (deflectionAtValue == null)
            {
                deflectionAtValue = new List<List<float>>();

                if (defaults == null)
                    defaults = new ConfigNode(nodeName);
                LoadConfig(defaults, true);
            }

            if (!loaded)
            {
                deflection = ((ModuleGimbal)module).gimbalRange;
                loaded = true;
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
                deflection = ((ModuleGimbal)module).gimbalRange;

            if (windowInstance.moduleToDraw != this)
                return;

            foreach (Part p in part.symmetryCounterparts)
            {
                if (p != null)
                    copyToModule(p.Modules.OfType<ModuleDynamicGimbal>().FirstOrDefault(), deflectionAtValue);
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel.HoldPhysics)
                return;

            currentDeflection = Mathf.Clamp(Evaluate(deflectionAtValue, engine.GetCurrentThrust() / engine.maxThrust) * deflection, 0, 30);

            ((ModuleGimbal)module).gimbalRange = currentDeflection;
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

        // copy to every gimbal on the vessel, not just the sym counterparts
        public override void copyToAll()
        {
            foreach (Part p in (HighLogic.LoadedSceneIsEditor ? EditorLogic.fetch.getSortedShipList() : vessel.parts))
            {
                if (p != null && p.Modules.Contains("ModuleDynamicGimbal"))
                    copyToModule(p.Modules.OfType<ModuleDynamicGimbal>().FirstOrDefault(), deflectionAtValue);
            }
        }

        public override void UpdateDefaults(ConfigNode node)
        {
            defaults = node;
        }
    }
}
