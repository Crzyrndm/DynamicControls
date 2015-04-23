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

        const string nodeName = "DynamicDeflection";
        const string savePath = "GameData/Dynamic Controls/Defaults.cfg";

        public ModuleDynamicDeflection moduleToDraw;
        string dynPressure = "";
        string deflection = "";

        public static Rect windowRect = new Rect(300, 300, 0, 0);

        public void Awake()
        {
            ModuleDynamicDeflection.defaults = GameDatabase.Instance.GetConfigNodes(nodeName).FirstOrDefault();
        }

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

            moduleToDraw.deflectionAtPressure.RemoveAll(l => l[0] < 0 || l[1] < 0); // set less than zero to remove from list
        }

        public void OnGUI()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;

            if (moduleToDraw != null)
                windowRect = GUILayout.Window(7463908, windowRect, drawWindow, "");
        }

        public void OnDestroy()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
                return;
            writeDefaultsToFile();
        }

        public void writeDefaultsToFile()
        {
            if (ModuleDynamicDeflection.defaults == null)
                return;
            ConfigNode dummyNode = new ConfigNode();
            dummyNode.AddValue("dummy", "do not delete me");
            dummyNode.AddNode(ModuleDynamicDeflection.defaults);
            dummyNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + savePath);
        }

        int focus = -1;
        private void drawWindow(int id)
        {
            if (moduleToDraw.deflectionAtPressure == null)
                moduleToDraw.deflectionAtPressure = new List<List<float>>();

            GUILayout.Label("100% deflection: " + moduleToDraw.deflection.ToString("0.#") + " degrees");
            if (HighLogic.LoadedSceneIsFlight)
                GUILayout.Label("Deflection @ Q(" + moduleToDraw.Q.ToString("0.0") + ") = " + moduleToDraw.currentDeflection.ToString("0.0"));
            GUILayout.Box("", GUILayout.Height(10));
            GUILayout.BeginHorizontal();
            GUILayout.Space(77);
            GUILayout.Label("Q (kPa)", GUILayout.Width(53));
            GUILayout.Label("% Deflect", GUILayout.Width(57));
            GUILayout.EndHorizontal();

            for (int i = 0; i <= moduleToDraw.deflectionAtPressure.Count; i++)
            {
                if (i < moduleToDraw.deflectionAtPressure.Count)
                {
                    List<float> list = moduleToDraw.deflectionAtPressure[i];
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
                    if (moduleToDraw.deflectionAtPressure.Count - 1 == i)
                        GUI.SetNextControlName("deflection");
                    string tmp = GUILayout.TextField(list[1].ToString("0.0") == "0.0" ? "" : list[1].ToString("0.0"), GUILayout.Width(60));
                    Debug.Log(tmp);
                    if (tmp != "")
                        list[1] = float.Parse(tmp);
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(70);
                    GUI.SetNextControlName("dynPress");
                    dynPressure = GUILayout.TextField(dynPressure, GUILayout.Width(60));
                    
                    deflection = GUILayout.TextField(deflection, GUILayout.Width(60));
                    GUILayout.EndHorizontal();
                    if (focus == 0)
                    {
                        GUI.FocusControl("deflection");
                        focus = -1;
                    }
                    else if (focus > 0)
                        focus--;
                    if (dynPressure != "" && GUI.GetNameOfFocusedControl() != "dynPress")
                    {
                        List<float> newEntry = new List<float>();
                        newEntry.Add(Mathf.Abs(float.Parse(dynPressure)));
                        newEntry.Add(Mathf.Abs(deflection != "" ? float.Parse(deflection) : 0));
                        moduleToDraw.deflectionAtPressure.Add(newEntry);
                        dynPressure = deflection = "";
                        focus = 5;
                    }
                }
            }

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
                ModuleDynamicDeflection.defaults = toConfigNode(moduleToDraw.deflectionAtPressure, node);
                writeDefaultsToFile();
            }
            if (GUILayout.Button("Restore"))
            {
                moduleToDraw.LoadConfig(ModuleDynamicDeflection.defaults, true);
                windowRect.height = 0;
            }
            GUILayout.EndHorizontal();
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

        public static ConfigNode toConfigNode(List<List<float>> list, ConfigNode node)
        {
            foreach (List<float> l in list)
            {
                if (l.Count < 2)
                    continue;

                node.AddValue("key", l[0].ToString() + "," + l[1].ToString());
            }
            return node;
        }
    }
}
