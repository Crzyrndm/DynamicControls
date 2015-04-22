using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    class EditorWindow : MonoBehaviour
    {
        public static ModuleDynamicDeflection moduleToDraw;
        string dynPressure = "";
        string deflection = "";

        Rect windowRect = new Rect(300, 300, 0, 0);
        public void Start()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;

            moduleToDraw = null;
        }

        public void Update()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;

            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                if (!windowRect.Contains(mouse))
                    moduleToDraw = null;
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
            foreach (List<float> list in moduleToDraw.deflectionAtPressure)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Remove Key", GUILayout.Width(100)))
                {
                    list[1] = -1;
                    windowRect.height = 0;
                }
                GUILayout.Label("\tQ (kPa)", GUILayout.Width(80));
                list[0] = float.Parse(GUILayout.TextField(list[0].ToString("0.##"), GUILayout.Width(100)));
                GUILayout.Label("\t% Deflect", GUILayout.Width(100));
                list[1] = float.Parse(GUILayout.TextField(list[1].ToString("0.#"), GUILayout.Width(100)));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(40);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Key", GUILayout.Width(100)))
            {
                List<float> newEntry = new List<float>();
                newEntry.Add(Mathf.Abs(float.Parse(dynPressure)));
                newEntry.Add(Mathf.Abs(float.Parse(deflection)));
                moduleToDraw.deflectionAtPressure.Add(newEntry);
                dynPressure = deflection = "";
            }
            GUILayout.Label("\tQ (kPa)", GUILayout.Width(80));
            dynPressure = GUILayout.TextField(dynPressure, GUILayout.Width(100));
            GUILayout.Label("\t % deflect", GUILayout.Width(100));
            deflection = GUILayout.TextField(deflection, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            moduleToDraw.deflectionAtPressure = moduleToDraw.deflectionAtPressure.OrderBy(x => x[0]).ToList();
            moduleToDraw.deflectionAtPressure.RemoveAll(l => l[1] < 0); // set deflection less than zero to remove from list

            if (GUILayout.Button("Copy to all"))
            {
                foreach (Part p in moduleToDraw.vessel.Parts)
                {
                    if (p == null)
                        continue;

                    if (p.Modules.Contains("ModuleDynamicDeflection"))
                        (p.Modules["ModuleDynamicDeflection"] as ModuleDynamicDeflection).deflectionAtPressure = moduleToDraw.deflectionAtPressure;
                }
            }

            GUI.DragWindow();
        }
    }
}
