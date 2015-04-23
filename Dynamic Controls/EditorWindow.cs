using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    class EditorWindow : MonoBehaviour
    {
        public static EditorWindow Instance { get; private set; }

        public ModuleDynamicDeflection moduleToDraw;
        string dynPressure = "";
        string deflection = "";

        public static Rect windowRect = new Rect(300, 300, 0, 0);

        public void Start()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;
            Instance = this;

            moduleToDraw = null;
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
                    moduleToDraw.deflectionAtPressure = moduleToDraw.deflectionAtPressure.OrderBy(x => x[0]).ToList();
                    moduleToDraw = null;
                }
            }
        }

        public void OnGUI()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;

            if (moduleToDraw != null)
                windowRect = GUILayout.Window(7463908, windowRect, drawWindow, "");
        }

        private void drawWindow(int id)
        {
            if (moduleToDraw.deflectionAtPressure == null)
                moduleToDraw.deflectionAtPressure = new List<List<float>>();

            GUILayout.BeginHorizontal();
            GUILayout.Space(98);
            GUILayout.Label("Q (kPa)", GUILayout.Width(53));
            GUILayout.Label("% Deflect", GUILayout.Width(57));
            GUILayout.EndHorizontal();

            foreach (List<float> list in moduleToDraw.deflectionAtPressure)
            {
                if (list.Count < 2)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Remove Key", GUILayout.Width(88)))
                {
                    list[1] = -1;
                    windowRect.height = 0;
                    removeFocus();
                }
                list[0] = float.Parse(GUILayout.TextField(list[0].ToString("0.0#"), GUILayout.Width(60)));
                list[1] = float.Parse(GUILayout.TextField(list[1].ToString("0.0#"), GUILayout.Width(60)));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Key", GUILayout.Width(88)))
            {
                List<float> newEntry = new List<float>();
                newEntry.Add(Mathf.Abs(float.Parse(dynPressure)));
                newEntry.Add(Mathf.Abs(float.Parse(deflection)));
                moduleToDraw.deflectionAtPressure.Add(newEntry);
                dynPressure = deflection = "";

                removeFocus();
            }
            dynPressure = GUILayout.TextField(dynPressure, GUILayout.Width(60));
            deflection = GUILayout.TextField(deflection, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Copy to all"))
            {
                copyToAll();
                removeFocus();
            }

            moduleToDraw.deflectionAtPressure.RemoveAll(l => l[1] < 0); // set deflection less than zero to remove from list
            GUI.DragWindow();
        }

        private void copyToAll()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (Part p in EditorLogic.fetch.getSortedShipList())
                {
                    if (p == null)
                        continue;
                    if (p.Modules.Contains("ModuleDynamicDeflection"))
                        copyToModule(p.Modules["ModuleDynamicDeflection"] as ModuleDynamicDeflection, moduleToDraw.deflectionAtPressure);
                }
            }
            else
            {
                foreach (Part p in moduleToDraw.vessel.parts)
                {
                    if (p == null)
                        continue;
                    if (p.Modules.Contains("ModuleDynamicDeflection"))
                        copyToModule(p.Modules["ModuleDynamicDeflection"] as ModuleDynamicDeflection, moduleToDraw.deflectionAtPressure);
                }
            }
        }

        public static void copyToModule(ModuleDynamicDeflection m, List<List<float>> list)
        {
            m.deflectionAtPressure = new List<List<float>>();
            foreach (List<float> kvp in list)
                m.deflectionAtPressure.Add(new List<float>(kvp));
        }

        private void removeFocus()
        {
            GUI.FocusControl("Copy to all");
            GUI.UnfocusWindow();
        }

        public void selectNewPart(ModuleDynamicDeflection module)
        {
            moduleToDraw = module;
            deflection = dynPressure = "";
            windowRect.height = 0;
        }
    }
}
