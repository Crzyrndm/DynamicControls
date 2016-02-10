﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    class EditorWindowGimbal : MonoBehaviour
    {
        public static EditorWindowGimbal Instance { get; private set; }

        public const string nodeName = "DynamicGimbal";
        const string savePath = "GameData/Dynamic Controls/DefaultsGimbal.cfg";

        public ModuleDynamicGimbal moduleToDraw;
        string thrustPct = "";
        string deflection = "";

        public static Rect windowRect = new Rect(300, 300, 0, 0);
        int maxX, maxY;

        Display display;
        bool showDisplay = false;

        public void Awake()
        {
            ModuleDynamicGimbal.defaults = GameDatabase.Instance.GetConfigNodes(nodeName).FirstOrDefault();
        }

        public void Start()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;
            Instance = this;

            moduleToDraw = null;

            display = new Display(160, 200);
            StartCoroutine(slowUpdate());
        }

        public void Update()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) || moduleToDraw == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                if (!windowRect.Contains(mouse))
                {
                    moduleToDraw.deflectionAtThrust = moduleToDraw.deflectionAtThrust.OrderBy(x => x[0]).ToList();
                    moduleToDraw = null;
                }
            }
        }

        IEnumerator slowUpdate()
        {
            yield return new WaitForSeconds(2f); // make sure everything is initialised...
            while (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                yield return new WaitForSeconds(0.5f);

                if (moduleToDraw == null)
                    continue;

                moduleToDraw.deflectionAtThrust.RemoveAll(l => l[0] < 0 || l[1] < 0); // set less than zero to remove from list
                maxX = (int)(getMaxX(moduleToDraw.deflectionAtThrust) + 10);
                maxY = (int)Math.Min((getMaxY(moduleToDraw.deflectionAtThrust) + 10), 150);
                if (showDisplay)
                {
                    if (HighLogic.LoadedSceneIsEditor)
                        display.drawPoints(moduleToDraw.deflectionAtThrust, maxX, maxY, false);
                    else if (HighLogic.LoadedSceneIsFlight)
                    {
                        List<List<float>> listToDraw = new List<List<float>>(moduleToDraw.deflectionAtThrust);
                        listToDraw.Add(new List<float>() { 100 * moduleToDraw.engineModule.finalThrust / moduleToDraw.engineModule.maxThrust, 100 * moduleToDraw.currentDeflection / moduleToDraw.deflection });
                        display.drawPoints(listToDraw, maxX, maxY, true);
                    }
                }
            }
        }

        public void OnGUI()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;

            if (moduleToDraw == null)
                return;

            windowRect = GUILayout.Window(7463908, windowRect, drawWindow, "");
            if (HighLogic.LoadedSceneIsEditor)
            {
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                if (windowRect.Contains(mouse))
                    EditorLogic.fetch.Lock(false, false, false, nodeName);
                else
                    EditorLogic.fetch.Unlock(nodeName);
            }
        }

        public void writeDefaultsToFile()
        {
            if (ModuleDynamicGimbal.defaults == null)
                return;
            ConfigNode dummyNode = new ConfigNode();
            dummyNode.AddValue("dummy", "do not delete me");
            dummyNode.AddNode(ModuleDynamicGimbal.defaults);
            dummyNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + savePath);
        }

        int focus = -1;
        private void drawWindow(int id)
        {
            if (moduleToDraw.deflectionAtThrust == null)
                moduleToDraw.deflectionAtThrust = new List<List<float>>();

#warning dynPressure references
            GUILayout.Label("100% deflection: " + Math.Abs(moduleToDraw.deflection).ToString("0.0") + " degrees");
            if (HighLogic.LoadedSceneIsFlight)
                GUILayout.Label("Deflect @ T%(" + (100 * moduleToDraw.engineModule.finalThrust / moduleToDraw.engineModule.maxThrust).ToString("0") + ") = " + Math.Abs(moduleToDraw.currentDeflection).ToString("0.0") + "(" + (moduleToDraw.currentDeflection * 100 / moduleToDraw.deflection).ToString("0") + "%)");
            GUILayout.Box("", GUILayout.Height(10));
            GUILayout.BeginHorizontal();
            GUILayout.Space(77);
            GUILayout.Label("T%", GUILayout.Width(53));
            GUILayout.Label("% Deflect", GUILayout.Width(57));
            GUILayout.EndHorizontal();

            for (int i = 0; i < moduleToDraw.deflectionAtThrust.Count; i++)
            {
                List<float> list = moduleToDraw.deflectionAtThrust[i];
                if (list.Count < 2)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Remove", GUILayout.Width(65)))
                {
                    list[1] = -1;
                    windowRect.height = 0;
                    removeFocus();
                }
                list[0] = float.Parse(GUILayout.TextField(list[0].ToString("0.0#"), GUILayout.Width(60)));
                if (moduleToDraw.deflectionAtThrust.Count - 1 == i)
                    GUI.SetNextControlName("deflection");
                string tmp = GUILayout.TextField(list[1] == 0 ? "" : list[1].ToString("0.0"), GUILayout.Width(60));
                if (tmp != "")
                    list[1] = float.Parse(tmp);
                GUILayout.EndHorizontal();
            }

            // ==================== The new entry fields
            GUILayout.BeginHorizontal();
            GUILayout.Space(70);
            GUI.SetNextControlName("thrustPct");
            thrustPct = GUILayout.TextField(thrustPct, GUILayout.Width(60));

            deflection = GUILayout.TextField(deflection, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            if (focus == 0)
            {
                GUI.FocusControl("deflection");
                focus = -1;
            }
            else if (focus > 0)
                focus--;
            if (thrustPct != "" && GUI.GetNameOfFocusedControl() != "thrustPct")
            {
                List<float> newEntry = new List<float>();
                newEntry.Add(Mathf.Abs(float.Parse(thrustPct)));
                newEntry.Add(Mathf.Abs(deflection != "" ? float.Parse(deflection) : 0));
                moduleToDraw.deflectionAtThrust = moduleToDraw.deflectionAtThrust.OrderBy(x => x[0]).ToList(); // sort exisint entries
                moduleToDraw.deflectionAtThrust.Add(newEntry); // add new entry to end
                thrustPct = deflection = "";
                focus = 5;
            }
            // ===================== End new entry fields

            if (GUILayout.Button("Copy to all"))
            {
                copyToAll();
                removeFocus();
            }
            GUILayout.Box("", GUILayout.Height(10));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Defaults: ");
            if (GUILayout.Button("Update"))
            {
                ConfigNode node = new ConfigNode(nodeName);
                ModuleDynamicGimbal.defaults = toConfigNode(moduleToDraw.deflectionAtThrust, node, true);
                writeDefaultsToFile();
            }
            if (GUILayout.Button("Restore"))
            {
                moduleToDraw.LoadConfig(ModuleDynamicGimbal.defaults, true);
                windowRect.height = 0;
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("", GUILayout.Height(10)))
            {
                showDisplay = !showDisplay;
                windowRect.height = 0;
            }

            // ======================== draw graph thing
            if (showDisplay)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label(maxY.ToString());
                GUILayout.Space(67);
                GUILayout.Label(" %");
                GUILayout.Space(67);
                GUILayout.Label("0");
                GUILayout.EndVertical();
                GUILayout.Label(display.Image);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label("0");
                GUILayout.Space(47);
                GUILayout.Label("T%");
                GUILayout.Space(47);
                GUILayout.Label(maxX.ToString());
                GUILayout.EndHorizontal();
            }

            GUI.DragWindow();
        }

        // copy to every control surface on the vessel, not just the sym counterparts
        private void copyToAll()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (Part p in EditorLogic.fetch.getSortedShipList())
                {
                    if (p == null)
                        continue;
                    if (p.Modules.Contains("ModuleDynamicGimbal"))
                        copyToModule(p.Modules["ModuleDynamicGimbal"] as ModuleDynamicGimbal, moduleToDraw.deflectionAtThrust);
                }
            }
            else
            {
                foreach (Part p in moduleToDraw.vessel.parts)
                {
                    if (p == null)
                        continue;
                    if (p.Modules.Contains("ModuleDynamicGimbal"))
                        copyToModule(p.Modules["ModuleDynamicGimbal"] as ModuleDynamicGimbal, moduleToDraw.deflectionAtThrust);
                }
            }
        }

        // need to make a copy of all subelements of the lists to prevent them permanently linking
        public static void copyToModule(ModuleDynamicGimbal m, List<List<float>> list)
        {
            m.deflectionAtThrust = new List<List<float>>();
            foreach (List<float> kvp in list)
                m.deflectionAtThrust.Add(new List<float>(kvp));
        }

        private void removeFocus()
        {
            GUI.FocusControl("Copy to all");
            GUI.UnfocusWindow();
        }

        public void selectNewPart(ModuleDynamicGimbal module)
        {
            moduleToDraw = module;
            deflection = thrustPct = "";
            windowRect.height = 0;
        }

        public static ConfigNode toConfigNode(List<List<float>> list, ConfigNode node, bool defaults, float fullDeflect = 0)
        {
            list.RemoveAll(l => l.Count != 2); // they're all meant to be 2 element entries
            if (!defaults)
                node.AddValue("deflection", fullDeflect); // defaults can't save 100% deflection
            foreach (List<float> l in list)
            {
                node.AddValue("key", l[0].ToString() + "," + l[1].ToString());
            }
            return node;
        }

        public float getMaxX(List<List<float>> list)
        {
            return list.Max(l => l[0]);
        }

        public float getMaxY(List<List<float>> list)
        {
            return list.Max(l => l[1]);
        }
    }
}
